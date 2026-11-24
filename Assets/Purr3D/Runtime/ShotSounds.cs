using System;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// Placeholder sounds, synthesised in code so shooting has feedback before the art pass brings SoundCraft's.
    /// Each is a few hundred samples of maths: a falling zap, a dull thunk and an error buzz.
    /// </summary>
    public static class ShotSounds
    {
        private const int Rate = 44100;

        private static AudioClip _zap, _thunk, _buzz;

        /// <summary>The shot: a quick downward sweep.</summary>
        public static AudioClip Zap => _zap != null ? _zap : _zap = Make("Zap (placeholder)", 0.14f, (t, d) =>
        {
            var frequency = Mathf.Lerp(1800f, 300f, t / d);
            return Mathf.Sin(2f * Mathf.PI * frequency * t) * Envelope(t, d, 0.004f) * 0.35f;
        });

        /// <summary>The task has nowhere to go: a low, quickly dying knock.</summary>
        public static AudioClip Thunk => _thunk != null ? _thunk : _thunk = Make("Thunk (placeholder)", 0.22f, (t, d) =>
        {
            var frequency = Mathf.Lerp(140f, 70f, t / d);
            return Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 22f) * 0.8f;
        });

        /// <summary>Purr refused the change: two short square-wave buzzes.</summary>
        public static AudioClip Buzz => _buzz != null ? _buzz : _buzz = Make("Buzz (placeholder)", 0.36f, (t, d) =>
        {
            var on = t < 0.15f || t > 0.2f;
            var square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 110f * t));
            return on ? square * Envelope(t, d, 0.01f) * 0.18f : 0f;
        });

        /// <summary>A linear fade in over <paramref name="attack"/> seconds and out over the last fifth.</summary>
        private static float Envelope(float t, float duration, float attack)
        {
            var fadeIn = Mathf.Clamp01(t / attack);
            var fadeOut = Mathf.Clamp01((duration - t) / (duration * 0.2f));
            return Mathf.Min(fadeIn, fadeOut);
        }

        private static AudioClip Make(string name, float seconds, Func<float, float, float> sample)
        {
            var count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            for (var i = 0; i < count; i++) data[i] = sample(i / (float)Rate, seconds);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
