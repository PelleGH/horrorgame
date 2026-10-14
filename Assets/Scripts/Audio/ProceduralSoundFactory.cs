using UnityEngine;

namespace HorrorAudio
{
    // Small cached sample buffers, generated on the main thread; no audio-thread allocations.
    public static class ProceduralSoundFactory
    {
        public static AudioClip Create(SoundLayer layer, bool loop, int seed)
        {
            const int rate = 22050;
            float duration = Mathf.Clamp(layer.duration, 0.05f, 5f);
            int count = Mathf.CeilToInt(duration * rate);
            float[] data = new float[count];
            var random = new System.Random(seed);
            float filtered = 0f;
            float frequency = Mathf.Clamp(layer.frequency, 20f, 4000f);
            if (loop) frequency = Mathf.Max(1f, Mathf.Round(frequency * duration)) / duration;
            float peak = 0.001f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                filtered += (layer.generator == GeneratedSound.Wind ? 0.025f : 0.18f) * (noise - filtered);
                float tone = Mathf.Sin(2f * Mathf.PI * frequency * t);
                float sample;
                switch (layer.generator)
                {
                    case GeneratedSound.Wind: sample = filtered * 4f; break;
                    case GeneratedSound.Hum:
                        sample = (tone + 0.3f * Mathf.Sin(4f * Mathf.PI * frequency * t)) * (1f - layer.noise) + filtered * layer.noise;
                        break;
                    case GeneratedSound.Creak:
                        sample = Mathf.Sin(2f * Mathf.PI * frequency * t + 2f * Mathf.Sin(t * 23f)) * (1f - layer.noise) + filtered * layer.noise;
                        break;
                    default: sample = tone * (1f - layer.noise) + noise * layer.noise; break;
                }
                if (!loop)
                    sample *= Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * (layer.generator == GeneratedSound.Impact ? 18f : 4f) / duration)
                        * Mathf.Clamp01((duration - t) / 0.025f);
                data[i] = sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }
            for (int i = 0; i < count; i++) data[i] *= 0.8f / peak;
            if (loop)
            {
                int overlap = Mathf.Min(count / 4, rate / 20);
                float[] seamless = new float[count - overlap];
                for (int i = 0; i < overlap; i++)
                    seamless[i] = Mathf.Lerp(data[count - overlap + i], data[i], (float)i / overlap);
                System.Array.Copy(data, overlap, seamless, overlap, count - overlap * 2);
                data = seamless;
            }
            AudioClip clip = AudioClip.Create("Generated " + layer.generator + " " + seed, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
