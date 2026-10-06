using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    // Phase 35 REPLAY THEATER: watching a saved game tape with pause, slow motion, fast forward, scrubbing, cameras
    // and marks. Going forward simulates ahead right here; going back restarts the scene at that moment (a tape is
    // only inputs, so the game is simulated again from the tip-off up to it, before the first frame is drawn).
    public sealed partial class GameSceneController
    {
        private TapeTheater _theater;
        private TheaterBar _theaterBar;
        /// <summary>A jump waiting for the next frame (-1 = none).</summary>
        private int _seekTo = -1;

        private bool Theater => _theater != null;

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
                float step = FixedStep;
                float started = Time.realtimeSinceStartup;
                _theater.Index(() => new MatchSimulation(MatchSetup.FromRequest(request, App.Catalog)), step);
                Debug.Log("[Theater] Marked " + _theater.Marks.Count + " plays in " + Mathf.RoundToInt((Time.realtimeSinceStartup - started) * 1000f) + " ms");
            }
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

        /// <summary>Applies a waiting jump. Returns true if the scene is restarting (going back).</summary>
        private bool TheaterSeek()
        {
            if (_seekTo < 0) return false;
            int target = _seekTo;
            _seekTo = -1;
            int run = TapeTheater.SeekPlan(_watch.NextTick, target, _watch.Received, out bool restart);
            if (restart)
            {
                TapeStore.StartAt = target;
                if (TapeStore.PrepareToWatch(TapeStore.Playing, out _))
                {
                    _rematching = true;
                    SceneFlow.GoTo(SceneNames.Game);
                    return true;
                }
                TapeStore.StartAt = 0;
                return false;
            }
            // Forward: simulate ahead without the sights and sounds of everything skipped.
            for (int i = 0; i < run && !_match.IsOver && _watch.TryStep(out var a, out var b); i++)
                _match.Step(FixedStep, a, b);
            _leaper = -1;
            _celebrator = -1;
            for (int i = 0; i < _playerViews.Length; i++) RedrawPlayer(i); // substitutions may have happened
            SyncViews(0f, snapCamera: true);
            return false;
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
