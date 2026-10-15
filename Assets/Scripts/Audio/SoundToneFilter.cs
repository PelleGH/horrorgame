using System;
using UnityEngine;

namespace HorrorAudio
{
    // Two RBJ shelving filters. Coefficients are published atomically by the main
    // thread. The audio callback uses no Unity API, locks or heap allocations.
    [RequireComponent(typeof(AudioSource))]
    public class SoundToneFilter : MonoBehaviour
    {
        public struct Shelf
        {
            public double b0, b1, b2, a1, a2;
            public double Process(double input, double[] state, int offset)
            {
                double output = b0 * input + b1 * state[offset] + b2 * state[offset + 1]
                    - a1 * state[offset + 2] - a2 * state[offset + 3];
                if (Math.Abs(output) < 1e-20) output = 0;
                state[offset + 1] = state[offset]; state[offset] = input;
                state[offset + 3] = state[offset + 2]; state[offset + 2] = output;
                return output;
            }
        }
        class Coefficients
        {
            public readonly Shelf bass, treble;
            public readonly bool bypass;
            public Coefficients(float low, float high, int rate)
            {
                bass = CreateShelf(low, 250f, rate, false);
                treble = CreateShelf(high, 4000f, rate, true);
                bypass = Math.Abs(low) < 0.001f && Math.Abs(high) < 0.001f;
            }
        }
        private volatile Coefficients coefficients;
        private readonly double[] bassState = new double[32], trebleState = new double[32];
        private float cachedBass = float.NaN, cachedTreble = float.NaN;
        private int cachedRate;
        private volatile bool resetState;
        public void ResetState() { resetState = true; }
        public void Configure(float bassDb, float trebleDb)
        {
            int rate = Math.Max(8000, AudioSettings.outputSampleRate);
            bassDb = Mathf.Clamp(bassDb, -12f, 12f); trebleDb = Mathf.Clamp(trebleDb, -12f, 12f);
            if (cachedRate == rate && Mathf.Abs(cachedBass - bassDb) < 0.01f && Mathf.Abs(cachedTreble - trebleDb) < 0.01f) return;
            cachedBass = bassDb; cachedTreble = trebleDb; cachedRate = rate;
            coefficients = new Coefficients(bassDb, trebleDb, rate);
        }
        public static Shelf CreateShelf(float gainDb, float frequency, int sampleRate, bool high)
        {
            double a = Math.Pow(10, gainDb / 40.0);
            double w = 2 * Math.PI * Math.Min(frequency, sampleRate * 0.45) / sampleRate;
            double c = Math.Cos(w), s = Math.Sin(w), alpha = s / Math.Sqrt(2), beta = 2 * Math.Sqrt(a) * alpha;
            double b0, b1, b2, a0, a1, a2;
            if (!high)
            {
                b0 = a * ((a + 1) - (a - 1) * c + beta); b1 = 2 * a * ((a - 1) - (a + 1) * c);
                b2 = a * ((a + 1) - (a - 1) * c - beta); a0 = (a + 1) + (a - 1) * c + beta;
                a1 = -2 * ((a - 1) + (a + 1) * c); a2 = (a + 1) + (a - 1) * c - beta;
            }
            else
            {
                b0 = a * ((a + 1) + (a - 1) * c + beta); b1 = -2 * a * ((a - 1) + (a + 1) * c);
                b2 = a * ((a + 1) + (a - 1) * c - beta); a0 = (a + 1) - (a - 1) * c + beta;
                a1 = 2 * ((a - 1) - (a + 1) * c); a2 = (a + 1) - (a - 1) * c - beta;
            }
            return new Shelf { b0 = b0 / a0, b1 = b1 / a0, b2 = b2 / a0, a1 = a1 / a0, a2 = a2 / a0 };
        }
        void OnAudioFilterRead(float[] data, int channels)
        {
            Coefficients current = coefficients;
            if (resetState)
            {
                Array.Clear(bassState, 0, bassState.Length); Array.Clear(trebleState, 0, trebleState.Length);
                resetState = false;
            }
            if (current == null || current.bypass || channels < 1 || channels > 8) return;
            for (int i = 0; i < data.Length; i++)
            {
                int offset = (i % channels) * 4;
                double sample = current.treble.Process(current.bass.Process(data[i], bassState, offset), trebleState, offset);
                data[i] = (float)(sample / (1.0 + Math.Max(0.0, Math.Abs(sample) - 0.95)));
            }
        }
    }
}
