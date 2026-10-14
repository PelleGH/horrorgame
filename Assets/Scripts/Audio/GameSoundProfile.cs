using System;
using UnityEngine;
using UnityEngine.Audio;

namespace HorrorAudio
{
    public enum GeneratedSound { None, Impact, Wind, Hum, Creak }

    [Serializable]
    public class SoundLayer
    {
        public string name = "Layer";
        public AudioClip[] clips;
        public GeneratedSound generator;
        [Range(0f, 1f)] public float volume = 0.5f;
        public Vector2 pitch = new Vector2(0.95f, 1.05f);
        [Range(0.05f, 5f)] public float duration = 0.25f;
        [Range(20f, 4000f)] public float frequency = 120f;
        [Range(0f, 1f)] public float noise = 0.5f;
        public int seed = 42;
        [NonSerialized] private AudioClip[] generated;
        [NonSerialized] private int lastIndex = -1;

        public bool HasSound
        {
            get
            {
                if (clips == null || clips.Length == 0) return generator != GeneratedSound.None;
                foreach (AudioClip clip in clips) if (clip != null) return true;
                return false;
            }
        }

        public AudioClip NextClip(bool loop)
        {
            AudioClip[] choices = clips;
            if (choices == null || choices.Length == 0)
            {
                if (generator == GeneratedSound.None) return null;
                if (generated == null || generated.Length == 0 || generated[0] == null)
                {
                    generated = new AudioClip[3];
                    for (int i = 0; i < generated.Length; i++)
                        generated[i] = ProceduralSoundFactory.Create(this, loop, seed + i * 7919);
                }
                choices = generated;
            }
            int start = UnityEngine.Random.Range(0, choices.Length);
            if (choices.Length > 1 && start == lastIndex) start = (start + 1) % choices.Length;
            for (int i = 0; i < choices.Length; i++)
            {
                int index = (start + i) % choices.Length;
                if (choices[index] == null) continue;
                lastIndex = index;
                return choices[index];
            }
            return null;
        }

        public void ReleaseGenerated()
        {
            if (generated != null)
                foreach (AudioClip clip in generated)
                    if (clip != null)
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(clip);
                        else UnityEngine.Object.DestroyImmediate(clip);
                    }
            generated = null;
            lastIndex = -1;
        }
    }

    [CreateAssetMenu(menuName = "Horror Audio/Sound Profile")]
    public class GameSoundProfile : ScriptableObject
    {
        public SoundLayer[] layers = { new SoundLayer() };
        public AudioMixerGroup output;
        public bool loop;
        [Range(0f, 1f)] public float volume = 0.7f;
        [Range(0f, 1f)] public float spatialBlend = 1f;
        [Min(0.01f)] public float minDistance = 1f;
        [Min(0.1f)] public float maxDistance = 25f;
        [Range(0f, 5f)] public float dopplerLevel;
        [Range(0, 256)] public int priority = 128;
        [Header("Effects (live while playing)")]
        [Range(-12f, 12f)] public float bassDb;
        [Range(-12f, 12f)] public float trebleDb;
        [Range(0f, 1f)] public float distortion;
        [Range(10f, 22000f)] public float lowPass = 22000f;
        [Range(10f, 22000f)] public float highPass = 10f;
        public AudioReverbPreset reverb = AudioReverbPreset.Off;
        [Range(-10000f, 0f)] public float reverbLevel = -1500f;
        [Range(0f, 1f)] public float echoWet;
        [Range(10f, 5000f)] public float echoDelay = 180f;
        [Range(0f, 1f)] public float echoDecay = 0.25f;

        public void WarmUp()
        {
            if (layers == null) return;
            foreach (SoundLayer layer in layers) if (layer != null) layer.NextClip(loop);
        }

        void OnDisable() { ReleaseGenerated(); }
        public void ReleaseGenerated()
        {
            if (layers == null) return;
            foreach (SoundLayer layer in layers) if (layer != null) layer.ReleaseGenerated();
        }
    }
}
