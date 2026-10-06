using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    // Phase 35 REPLAY THEATER: watching a saved game tape with pause, slow motion, fast forward, scrubbing, cameras
    // and marks. A tape is only inputs, so going forward simulates ahead and going back (Phase 36) rebuilds the match
    // in place and simulates from the tip-off up to that moment. Both happen a slice at a time behind "SEEKING…",
    // as does the marking pass when a tape opens, so an older iPhone never freezes.
    public sealed partial class GameSceneController
    {
        private TapeTheater _theater;
        private TheaterBar _theaterBar;
        /// <summary>A jump waiting for the next frame (-1 = none).</summary>
        private int _seekTo = -1;
        /// <summary>Steps still to simulate for a jump in progress (0 = none).</summary>
        private int _seekLeft;
        private string[] _drawnIds;
        /// <summary>Per-frame budgets (ms) for the marking pass and for a jump.</summary>
        private const double IndexBudgetMs = 4.0, SeekBudgetMs = 22.0;
        private readonly System.Diagnostics.Stopwatch _sliceClock = new System.Diagnostics.Stopwatch();

        private bool Theater => _theater != null;

        /// <summary>For tests: the replay theater of the tape being watched (null otherwise), and a jump to a step.</summary>
        public TapeTheater TapeTheaterState => _theater;
        public int TapeTick => _watch != null ? _watch.NextTick : 0;
        public void TheaterJump(int tick) => _seekTo = Mathf.Max(0, tick);

        /// <summary>Called at the end of Start when the game is a tape.</summary>
        private void StartTheater()
        {
            var tape = TapeStore.Playing;
            if (_watch == null || !_watch.IsTape || tape == null) return;
            var theater = TapeStore.Theater;
            if (theater == null || theater.Tape != tape)
            {
                theater = new TapeTheater(tape);
                foreach (int t in tape.Marks) theater.AddUserMark(t);
                TapeStore.Theater = theater;
            }
            _theater = theater;
            if (!_theater.Indexed)
            {
                var request = _request;
                _theater.StartIndex(() => new MatchSimulation(MatchSetup.FromRequest(request, App.Catalog)));
            }
            _drawnIds = new string[_match.Players.Length];
            for (int i = 0; i < _drawnIds.Length; i++) _drawnIds[i] = _match.Players[i].Def.id;
            _theaterBar = TheaterBar.Create();
            _theaterBar.PlayPause += () => _theater.Paused = !_theater.Paused;
            _theaterBar.Slower += () => { _theater.Slower(); _hud.Toast(_theater.SpeedLabel, 0.7f); };
            _theaterBar.Faster += () => { _theater.Faster(); _hud.Toast(_theater.SpeedLabel, 0.7f); };
            _theaterBar.NextCamera += () =>
            {
                _theater.NextCamera(_match.Players.Length);
                _hud.Toast(CameraNote(), 0.9f);
            };
            _theaterBar.AddMark += AddTheaterMark;
            _theaterBar.PreviousMark += () => JumpToMark(_theater.PreviousMark(_watch.NextTick), "NO EARLIER MARKS");
            _theaterBar.NextMark += () => JumpToMark(_theater.NextMark(_watch.NextTick), "NO MORE MARKS");
            _theaterBar.MarkPicked += m => JumpToMark(m, null);
            _theaterBar.Scrubbed += f => _seekTo = _theater.TickAt(f);
            _cameraRig.SetCloseUp(_theater.Camera == TheaterCamera.CloseUp);
            if (TapeStore.StartAt > 0) _seekTo = TapeStore.StartAt;
            TapeStore.StartAt = 0;
        }

        private string CameraNote() =>
            _theater.Camera == TheaterCamera.Player
                ? "PLAYER CAM: " + _match.Players[_theater.CameraPlayer % _match.Players.Length].Def.DisplayName.ToUpperInvariant()
                : TapeTheater.CameraName(_theater.Camera);

        private void AddTheaterMark()
        {
            int tick = _watch.NextTick;
            var mark = _theater.AddUserMark(tick);
            if (mark == null)
            {
                _hud.Toast("THAT'S AS MANY MARKS AS A TAPE HOLDS", 1.4f);
                return;
            }
            var tape = _theater.Tape;
            if (tape.Marks.Count < Tapes.MaxMarks) tape.Marks.Add(tick);
            bool saved = TapeStore.Overwrite(TapeStore.PlayingPath, tape);
            _hud.Toast("MARKED " + TapeTheater.Clock(tick) + (saved ? "" : "  (FOR THIS VIEWING)"), 1.2f);
        }

        private void JumpToMark(TapeMark m, string none)
        {
            if (m == null)
            {
                if (none != null) _hud.Toast(none, 1f);
                return;
            }
            _seekTo = TapeTheater.StartOf(m);
            _hud.Toast(m.Label, 1.4f);
        }

        /// <summary>
        /// One frame of theater housekeeping: a slice of the marking pass, then any jump. Returns true while a jump is
        /// still running (the frame shows "SEEKING…" and nothing plays).
        /// </summary>
        private bool TheaterSeek()
        {
            if (!_theater.Indexed)
            {
                _sliceClock.Restart();
                while (!_theater.IndexSome(120, FixedStep) && _sliceClock.Elapsed.TotalMilliseconds < IndexBudgetMs) { }
            }
            if (_seekTo >= 0)
            {
                int target = _seekTo;
                _seekTo = -1;
                int run = TapeTheater.SeekPlan(_watch.NextTick, target, _watch.Received, out bool restart);
                if (restart) RebuildMatch();
                _seekLeft = run;
                if (_seekLeft <= 0) { FinishSeek(); return false; }
            }
            if (_seekLeft <= 0) return false;
            _sliceClock.Restart();
            while (_seekLeft > 0 && !_match.IsOver && _sliceClock.Elapsed.TotalMilliseconds < SeekBudgetMs)
            {
                for (int k = 0; k < 60 && _seekLeft > 0 && !_match.IsOver; k++)
                {
                    if (!_watch.TryStep(out var a, out var b)) { _seekLeft = 0; break; }
                    _match.Step(FixedStep, a, b);
                    _seekLeft--;
                }
            }
            if (_match.IsOver) _seekLeft = 0;
            _theaterBar.SetBusy(_seekLeft > 0 ? "SEEKING…" : null);
            if (_seekLeft > 0) return true;
            FinishSeek();
            return false;
        }

        /// <summary>Going back: a fresh match from the tip-off, with the same views following its players.</summary>
        private void RebuildMatch()
        {
            _match = new MatchSimulation(MatchSetup.FromRequest(_request, App.Catalog));
            _watch.Rewind();
            for (int i = 0; i < _playerViews.Length && i < _match.Players.Length; i++) _playerViews[i].Rebind(_match.Players[i]);
            if (_mic != null) _mic = new Commentary(_match.Setup.TeamA.nickname, _match.Setup.TeamB.nickname, _request.Seed);
        }

        /// <summary>After a jump: redraw any slot whose player changed, and drop what belonged to the skipped moments.</summary>
        private void FinishSeek()
        {
            _seekLeft = 0;
            _theaterBar.SetBusy(null);
            for (int i = 0; i < _playerViews.Length && i < _match.Players.Length; i++)
            {
                string id = _match.Players[i].Def.id;
                if (_drawnIds[i] == id) continue;
                RedrawPlayer(i);
                _drawnIds[i] = id;
            }
            _leaper = -1;
            _celebrator = -1;
            _shootPoseUntil = 0f;
            _lastHighlight = null;
            _recorder.Reset();
            ResetArena();
            _hud.HideMic();
            CourtSpace.Flip = _match.Flipped;
            SyncViews(0f, snapCamera: true);
        }

        /// <summary>Where the theater camera looks (court position), or null for the usual choice.</summary>
        private Vec2? TheaterFollow()
        {
            if (!Theater) return null;
            switch (_theater.Camera)
            {
                case TheaterCamera.Player: return _match.Players[_theater.CameraPlayer % _match.Players.Length].Position;
                case TheaterCamera.Rim: return _match.Setup.Court.Hoop;
                default: return _match.Ball.Position;
            }
        }

        private void SyncTheaterBar()
        {
            if (!Theater) return;
            _cameraRig.SetCloseUp(_theater.Camera == TheaterCamera.CloseUp);
            _theaterBar.SetVisible(!_finalShown && !_paused);
            if (!_finalShown) _theaterBar.Sync(_theater, _watch.NextTick, CameraNote());
        }
    }
}
