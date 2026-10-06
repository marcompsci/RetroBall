using CallerRetroBall.Logic;

namespace CallerRetroBall.Gameplay
{
    // Phase 35 COACH MODE (Franchise): your players are all AI and you coach from the sideline panel.
    public sealed partial class GameSceneController
    {
        private CoachPanel _coach;

        private bool Coaching => _match != null && _match.Coaching;

        /// <summary>The player you steer (never anyone in a coached game).</summary>
        private bool Mine(int index) => _match != null && index == _match.ControlledIndex && !_match.Coaching;

        private void StartCoach()
        {
            if (!Coaching) return;
            SetTouchVisible(false);
            _coach = CoachPanel.Create(_match);
            _coach.Said += line => _hud.Toast(line, 1.4f);
        }

        /// <summary>A coach's list is open: the game waits for the call.</summary>
        private bool CoachHolding() => _coach != null && _coach.Holding && !_match.IsOver;

        private int _coachSubsSeen;

        private void SyncCoach()
        {
            if (_coach == null) return;
            _coach.SetVisible(!_finalShown && !_paused && _photo == null);
            if (_match.PendingSubs != _coachSubsSeen)
            {
                _coachSubsSeen = _match.PendingSubs;
                _coach.Refresh();
            }
        }
    }
}
