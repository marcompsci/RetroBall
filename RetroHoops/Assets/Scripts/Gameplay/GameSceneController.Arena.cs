using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    // Phase 35 arena polish: camera flashes after big plays, the wave during a run, the crowd on its feet for a close
    // finish, and the crowd's sound following all of it (see ArenaFx and AudioMix). Looks and sound only.
    public sealed partial class GameSceneController
    {
        private readonly List<ArenaFx.Flash> _flashes = new List<ArenaFx.Flash>();
        private float _flashStart;
        private float _waveStart = -99f;
        private int _runTeam = -1, _run;
        private float[] _fanX;
        private int _bigPlays;

        /// <summary>Phase 36: the replay theater jumped: no flashes, wave or run carried over.</summary>
        private void ResetArena()
        {
            _flashes.Clear();
            _waveStart = -99f;
            _run = 0;
            _runTeam = -1;
            _crowdMood = CrowdMood.Idle;
        }

        /// <summary>After a basket: maybe flashes, maybe the wave.</summary>
        private void ArenaOnBasket(int team, int points, bool dunk)
        {
            bool deep = points >= (_match.Setup.FullCourt ? 3 : 2);
            int n = ArenaFx.FlashCount(dunk, deep, Clutch);
            if (n > 0 && !_reduceMotion && !Core.PowerMonitor.SavingPower && _fans.Length > 0)
            {
                _flashes.Clear();
                _flashes.AddRange(ArenaFx.Flashes(_request.Seed + (uint)(++_bigPlays * 7919), _fans.Length, n));
                _flashStart = Time.unscaledTime;
            }
            int before = _run;
            _run = ArenaFx.Run(_runTeam, _run, team, points, out _runTeam);
            if (before < ArenaFx.WaveRun && _run >= ArenaFx.WaveRun && Time.unscaledTime - _waveStart > ArenaFx.WaveSeconds + 4f)
                _waveStart = Time.unscaledTime;
        }

        private bool Clutch => _match != null && ArenaFx.Clutch(_match.Score[0], _match.Score[1], _match.GameClock,
                                                                _match.Setup.Rules.useGameClock, _match.Setup.Rules.targetScore);

        /// <summary>Extra lift for fan <paramref name="i"/> from the wave or a close finish (0 when neither is on).</summary>
        private int ArenaLift(int i, float now, out bool armsUp)
        {
            armsUp = false;
            if (_fanX == null || _fanX.Length != _fans.Length) MeasureFans();
            float t = now - _waveStart;
            if (t >= 0f && t <= ArenaFx.WaveSeconds)
            {
                int lift = ArenaFx.WaveLift(_fanX[i], t);
                armsUp = lift == 2;
                return lift;
            }
            if (Clutch && !_match.IsOver) return ArenaFx.ClutchBob(now, i);
            return 0;
        }

        private void MeasureFans()
        {
            _fanX = new float[_fans.Length];
            if (_fans.Length == 0) return;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var f in _fans) { min = Mathf.Min(min, f.Base.x); max = Mathf.Max(max, f.Base.x); }
            float span = Mathf.Max(0.001f, max - min);
            for (int i = 0; i < _fans.Length; i++) _fanX[i] = (_fans[i].Base.x - min) / span;
        }

        /// <summary>Pops the camera flashes that are due (a white sparkle over that fan's head).</summary>
        private void SyncFlashes(float now)
        {
            if (_flashes.Count == 0) return;
            float since = now - _flashStart;
            for (int k = _flashes.Count - 1; k >= 0; k--)
            {
                var f = _flashes[k];
                if (since < f.Delay) continue;
                _flashes.RemoveAt(k);
                if (f.Fan < 0 || f.Fan >= _fans.Length) continue;
                var at = _fans[f.Fan].Base + new Vector3(0f, 9f / CourtSpace.PixelsPerUnit, 0f);
                _bursts.Spawn(at, Color.white, 3, 0.6f, 0.12f);
            }
        }

        private void SyncCrowdSound()
        {
            Audio.AudioManager.SetCrowdLevel(AudioMix.CrowdLevel(_crowdMood, Clutch && !_match.IsOver));
        }
    }
}
