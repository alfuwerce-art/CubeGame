using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.Presentation.Visual;
using UnityEngine;

namespace CubeGame.Gameplay
{
    /// <summary>One local, bounded playback owner for a running world.</summary>
    internal sealed class GameAudio : MonoBehaviour
    {
        private const int MaxVoices = 8;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private readonly HashSet<string> _playedThisFrame = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _frame = -1;
        private AlphaSoundLibrary _library;

        public void Bind(string contentRoot) => _library = new AlphaSoundLibrary(contentRoot);

        public bool Play(string sound)
        {
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                _playedThisFrame.Clear();
            }
            if (!_playedThisFrame.Add(sound))
            {
                return false;
            }
            if (!_clips.TryGetValue(sound, out AudioClip clip))
            {
                if (_library == null || !_library.TryResolve(sound, out string path))
                {
                    return false;
                }
                try
                {
                    clip = AlphaWave.Decode(File.ReadAllBytes(path), sound);
                }
                catch (Exception error) when (error is IOException || error is InvalidDataException
                    || error is UnauthorizedAccessException || error is ArgumentException)
                {
                    Debug.LogWarning($"[Audio] {sound}: {error.Message}");
                }
                _clips.Add(sound, clip);
            }
            if (clip == null)
            {
                return false;
            }

            AudioSource voice = _voices.Find(source => !source.isPlaying);
            if (voice == null && _voices.Count < MaxVoices)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                voice.volume = 0.65f;
                _voices.Add(voice);
            }
            if (voice == null)
            {
                return false;
            }
            voice.clip = clip;
            voice.Play();
            return true;
        }

        public void Release()
        {
            foreach (AudioSource voice in _voices)
            {
                if (voice != null)
                {
                    voice.Stop();
                    voice.clip = null;
                }
            }
            foreach (AudioClip clip in _clips.Values)
            {
                if (clip != null) Destroy(clip);
            }
            _clips.Clear();
            _playedThisFrame.Clear();
            _library = null;
        }

        private void OnDestroy() => Release();
    }
}
