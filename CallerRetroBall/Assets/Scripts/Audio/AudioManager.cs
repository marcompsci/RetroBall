using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Audio
{
    /// <summary>
    /// Plays the procedurally generated sounds (see <see cref="AudioSynth"/>). Two volume buses —
    /// Music and SFX — driven by Settings. SFX use a small pool of sources (no per-play allocation).
    /// No third-party audio is bundled.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SfxVoices = 6;

        private static AudioManager _instance;

        private readonly Dictionary<SfxId, AudioClip> _clips = new Dictionary<SfxId, AudioClip>();
        private AudioSource _music;
        private AudioSource _ambience;
        private bool _ambienceOn;
        private AudioSource[] _sfx;
        private int _next;
        private float _musicVolume = 0.6f;
        private float _sfxVolume = 0.9f;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[AudioManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioManager>();
            _instance.Build();
        }

        private void Build()
        {
            foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
                _clips[id] = MakeClip("sfx." + id, AudioSynth.Sfx(id));

            _music = gameObject.AddComponent<AudioSource>();
            _music.clip = MakeClip("music.loop", AudioSynth.MusicLoop());
            _music.loop = true;
            _music.playOnAwake = false;

            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.clip = MakeClip("ambience.crowd", AudioSynth.CrowdAmbience());
            _ambience.loop = true;
            _ambience.playOnAwake = false;

            _sfx = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
            {
                _sfx[i] = gameObject.AddComponent<AudioSource>();
                _sfx[i].playOnAwake = false;
            }
            ApplySettings();
            _music.Play();
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, AudioSynth.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Re-reads Music/SFX volumes from the career settings.</summary>
        public static void ApplySettings()
        {
            if (_instance == null) return;
            var s = App.Career?.settings;
            _instance._musicVolume = s != null ? s.musicVolume : 0.6f;
            _instance._sfxVolume = s != null ? s.sfxVolume : 0.9f;
            _instance._music.volume = _instance._musicVolume * (_instance._ambienceOn ? 0.3f : 0.5f);
            _instance._ambience.volume = _instance._sfxVolume * 0.22f;
        }

        /// <summary>Crowd murmur under a match (follows the SFX volume). Music ducks while it plays.</summary>
        public static void SetAmbience(bool on)
        {
            if (_instance == null || _instance._ambienceOn == on) return;
            _instance._ambienceOn = on;
            if (on) _instance._ambience.Play();
            else _instance._ambience.Stop();
            _instance._music.volume = _instance._musicVolume * (on ? 0.3f : 0.5f);
        }

        public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            if (_instance == null || _instance._sfxVolume <= 0f) return;
            var src = _instance._sfx[_instance._next];
            _instance._next = (_instance._next + 1) % SfxVoices;
            src.pitch = pitch;
            src.PlayOneShot(_instance._clips[id], volume * _instance._sfxVolume);
        }

        public static void Click() => Play(SfxId.Click, 0.6f);

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
