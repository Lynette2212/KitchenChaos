using System;
using UnityEngine;

namespace KitchenChaos.Audio
{
    // Original Foley models; mono physical sources spatialize in-world, stereo cues stay in the HUD.
    internal static class AudioSynthesis
    {
        private const int Rate = 44100;
        private const float Tau = Mathf.PI * 2f;

        internal static bool IsFoley(SfxType type) => type == SfxType.Pickup || type == SfxType.Drop
            || type == SfxType.Chop || type == SfxType.Sizzle || type == SfxType.Dash
            || type == SfxType.Footstep || type == SfxType.PlatePickup || type == SfxType.PlatePlace
            || type == SfxType.Trash;

        internal static AudioClip Build(SfxType type, int variant)
        {
            var random = new System.Random(17239 + (int)type * 3571 + variant * 997);
            if (type == SfxType.Sizzle) return Sizzle(random, variant);
            if (IsFoley(type) || type == SfxType.Burned) return Foley(type, random, variant);
            return Cue(type, variant);
        }

        private static AudioClip Foley(SfxType type, System.Random random, int variant)
        {
            float seconds = type == SfxType.Burned ? 0.55f : type == SfxType.Trash ? 0.38f
                : type == SfxType.Dash ? 0.3f : 0.24f;
            float[] data = new float[Mathf.CeilToInt(seconds * Rate)];
            float size = Between(random, 0.94f, 1.06f);
            switch (type)
            {
                case SfxType.Chop:
                    // Blade on wood: body modes, a small delayed board rebound, then wet food grains.
                    Impact(data, 0.002f, 0.12f, 165f * size, 0.28f, 34f);
                    Impact(data, 0.003f, 0.09f, 470f * size, 0.13f, 52f);
                    Impact(data, 0.004f, 0.035f, 2140f * size, 0.035f, 120f);
                    Impact(data, Between(random, 0.023f, 0.036f), 0.07f, 255f * size, 0.09f, 58f);
                    Noise(data, random, 0.002f, 0.045f, 0.38f, 0.68f, 0.18f, 0.0015f);
                    for (int grain = 0; grain < 6; grain++)
                        Noise(data, random, Between(random, 0.013f, 0.095f), Between(random, 0.008f, 0.022f),
                            Between(random, 0.045f, 0.10f), 0.46f, 0.055f, 0.002f);
                    break;
                case SfxType.Pickup:
                    Noise(data, random, 0.003f, 0.17f, 0.36f, 0.09f, 0.006f, 0.02f);
                    Impact(data, 0.012f, 0.12f, 115f * size, 0.18f, 35f);
                    Impact(data, 0.016f, 0.075f, 310f * size, 0.04f, 55f);
                    Noise(data, random, 0.08f, 0.09f, 0.15f, 0.22f, 0.025f, 0.012f);
                    break;
                case SfxType.Drop:
                    Impact(data, 0.006f, 0.16f, 92f * size, 0.27f, 30f);
                    Impact(data, 0.007f, 0.12f, 223f * size, 0.09f, 42f);
                    Noise(data, random, 0.002f, 0.075f, 0.3f, 0.17f, 0.01f, 0.004f);
                    Noise(data, random, 0.034f, 0.15f, 0.22f, 0.08f, 0.012f, 0.012f);
                    break;
                case SfxType.Dash:
                    Noise(data, random, 0f, 0.28f, 0.65f, 0.14f, 0.028f, 0.055f);
                    Noise(data, random, 0.04f, 0.19f, 0.19f, 0.045f, 0.004f, 0.035f);
                    break;
                case SfxType.Trash:
                    Impact(data, 0.035f, 0.21f, 87f * size, 0.18f, 24f);
                    Noise(data, random, 0f, 0.32f, 0.44f, 0.12f, 0.008f, 0.025f);
                    Noise(data, random, 0.07f, 0.1f, 0.21f, 0.32f, 0.045f, 0.006f);
                    break;
                case SfxType.Burned:
                    Noise(data, random, 0.005f, 0.49f, 0.43f, 0.24f, 0.018f, 0.025f);
                    Impact(data, 0.016f, 0.46f, 130.81f, 0.24f, 10f);
                    Impact(data, 0.09f, 0.36f, 98f, 0.16f, 12f);
                    break;
            }
            return Finish(type + "_" + variant, data, 1, false);
        }

        private static void Impact(float[] data, float start, float seconds, float frequency, float gain, float decay)
        {
            int offset = Mathf.RoundToInt(start * Rate);
            int length = Mathf.Min(Mathf.CeilToInt(seconds * Rate), data.Length - offset);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Rate;
                float envelope = Attack(t, 0.0025f) * Mathf.Exp(-t * decay) * Release(i, length, 0.018f);
                data[offset + i] += Mathf.Sin(Tau * frequency * t) * envelope * gain;
            }
        }

        // Difference of two low-passes gives soft band-limited friction without shrill white noise.
        private static void Noise(float[] data, System.Random random, float start, float seconds,
            float gain, float upper, float lower, float attack)
        {
            int offset = Mathf.RoundToInt(start * Rate);
            int length = Mathf.Min(Mathf.CeilToInt(seconds * Rate), data.Length - offset);
            float fast = 0f, slow = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Rate;
                float white = Between(random, -1f, 1f);
                fast += upper * (white - fast);
                slow += lower * (white - slow);
                float envelope = Attack(t, attack) * Mathf.Pow(1f - i / (float)length, 1.65f);
                data[offset + i] += (fast - slow) * envelope * gain;
            }
        }

        private static AudioClip Sizzle(System.Random random, int variant)
        {
            const float seconds = 4.6f;
            int samples = Mathf.RoundToInt(seconds * Rate);
            int crossfade = Mathf.RoundToInt(0.18f * Rate);
            float[] raw = new float[samples + crossfade];
            float fast = 0f, slow = 0f;
            float phase = Between(random, 0f, Tau);
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)Rate;
                float white = Between(random, -1f, 1f);
                fast += 0.38f * (white - fast);
                slow += 0.035f * (white - slow);
                float simmer = 0.86f + 0.09f * Mathf.Sin(Tau * 0.65f * t + phase)
                    + 0.05f * Mathf.Sin(Tau * 2.17f * t);
                raw[i] = (fast - slow) * simmer * 0.19f;
            }
            // Sparse little oil bubbles, not a periodic hiss burst.
            for (int i = 0; i < 112; i++)
            {
                float start = Between(random, 0f, seconds + 0.13f);
                Noise(raw, random, start, Between(random, 0.008f, 0.028f),
                    Between(random, 0.055f, 0.17f), 0.62f, 0.12f, 0.0018f);
            }
            float[] data = new float[samples];
            Array.Copy(raw, data, samples);
            // Join the continuous tail to the head with equal-power overlap; no silent loop boundary.
            for (int i = 0; i < crossfade; i++)
            {
                float angle = i / (float)(crossfade - 1) * Mathf.PI * 0.5f;
                data[i] = raw[samples + i] * Mathf.Cos(angle) + raw[i] * Mathf.Sin(angle);
            }
            return Finish("SizzleLoop_" + variant, data, 1, true);
        }

        private static AudioClip Cue(SfxType type, int variant)
        {
            float[] notes;
            float step = 0.075f, tail = 0.4f, gain = 0.21f;
            switch (type)
            {
                case SfxType.Cooked: notes = new[] { 659.25f, 783.99f, 1046.5f }; step = 0.035f; gain = 0.16f; break;
                case SfxType.DeliverSuccess: notes = new[] { 523.25f, 659.25f, 783.99f, 1046.5f }; break;
                case SfxType.DeliverFail: notes = new[] { 392f, 293.66f, 261.63f }; step = 0.095f; gain = 0.18f; break;
                case SfxType.OrderExpire: notes = new[] { 440f, 329.63f }; step = 0.14f; gain = 0.19f; break;
                case SfxType.ButtonClick: notes = new[] { 783.99f }; tail = 0.075f; gain = 0.15f; break;
                case SfxType.GameOver: notes = new[] { 392f, 329.63f, 261.63f, 130.81f }; step = 0.16f; tail = 0.7f; break;
                case SfxType.Tick: notes = new[] { 880f }; tail = 0.055f; gain = 0.18f; break;
                case SfxType.Warning: notes = new[] { 659.25f, 659.25f }; step = 0.12f; tail = 0.12f; gain = 0.20f; break;
                case SfxType.Combo: notes = new[] { 783.99f, 1046.5f, 1318.51f }; step = 0.065f; gain = 0.16f; break;
                case SfxType.DayComplete: notes = new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.51f }; step = 0.11f; tail = 0.65f; break;
                case SfxType.Victory: notes = new[] { 523.25f, 659.25f, 783.99f, 1046.5f }; step = 0.16f; tail = 1.1f; break;
                case SfxType.Bell: notes = new[] { 1046.5f }; tail = 0.64f; gain = 0.22f; break;
                case SfxType.ChopDone: notes = new[] { 659.25f, 783.99f }; step = 0.055f; tail = 0.24f; gain = 0.15f; break;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "No sound model");
            }
            int frames = Mathf.CeilToInt(((notes.Length - 1) * step + tail) * Rate);
            float[] data = new float[frames * 2];
            // Variations change mallet brightness, never the tuning of musical signals.
            float brightness = 0.1f + variant * 0.018f;
            for (int n = 0; n < notes.Length; n++)
            {
                float pan = notes.Length == 1 ? 0f : Mathf.Lerp(-0.16f, 0.16f, n / (float)(notes.Length - 1));
                Note(data, notes[n], n * step, tail, gain, brightness, pan);
            }
            if (type == SfxType.Victory)
            {
                float start = (notes.Length - 1) * step;
                Note(data, 261.63f, start, tail, 0.10f, 0.08f, -0.22f);
                Note(data, 392f, start, tail, 0.09f, 0.08f, 0.22f);
            }
            return Finish(type + "_" + variant, data, 2, false);
        }

        private static void Note(float[] data, float frequency, float start, float seconds, float gain, float brightness, float pan)
        {
            int offset = Mathf.RoundToInt(start * Rate);
            int length = Mathf.Min(Mathf.CeilToInt(seconds * Rate), data.Length / 2 - offset);
            float left = Mathf.Sqrt((1f - pan) * 0.5f), right = Mathf.Sqrt((1f + pan) * 0.5f);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Rate;
                float envelope = Attack(t, 0.004f) * Mathf.Exp(-t * 4.8f / seconds) * Release(i, length, Mathf.Min(0.035f, seconds * 0.3f));
                float fundamental = Mathf.Sin(Tau * frequency * t);
                float overtone = Mathf.Sin(Tau * frequency * 2f * t) * brightness * Mathf.Exp(-t * 18f);
                float value = (fundamental + overtone) * envelope * gain;
                int index = (offset + i) * 2;
                data[index] += value * left;
                data[index + 1] += value * right;
            }
        }

        private static float Between(System.Random random, float min, float max)
            => min + (max - min) * (float)random.NextDouble();
        private static float Attack(float time, float duration)
        {
            float x = Mathf.Clamp01(time / duration);
            return x * x * (3f - 2f * x);
        }
        private static float Release(int sample, int length, float seconds)
        {
            float x = Mathf.Clamp01((length - 1 - sample) / (seconds * Rate));
            return x * x * (3f - 2f * x);
        }

        private static AudioClip Finish(string name, float[] data, int channels, bool loop)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            // Limit only; never normalize a quiet shoe/scuff up to a loud alert. Keep >4 dB headroom.
            float scale = peak > 0.6f ? 0.6f / peak : 1f;
            int frames = data.Length / channels;
            for (int frame = 0; frame < frames; frame++)
            {
                float envelope = loop ? 1f : Attack(frame / (float)Rate, 0.0015f) * Release(frame, frames, 0.018f);
                for (int channel = 0; channel < channels; channel++)
                    data[frame * channels + channel] *= scale * envelope;
            }
            AudioClip clip = AudioClip.Create(name, frames, channels, Rate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(data, 0);
            return clip;
        }
    }
}
