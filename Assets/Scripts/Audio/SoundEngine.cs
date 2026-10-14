using System.Collections.Generic;
using UnityEngine;

namespace HorrorAudio
{
    [DisallowMultipleComponent]
    public class SoundEngine : MonoBehaviour
    {
        public static SoundEngine Instance { get; private set; }
        internal static readonly List<SoundEmitter> Emitters = new List<SoundEmitter>();
        internal static readonly List<SoundEnvironmentZone> Zones = new List<SoundEnvironmentZone>();
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(8, 128)] public int maxActiveVoices = 48;
        public AudioListener listener;
        public float RideSpeed { get; private set; }
        public float RideStress { get; private set; }
        internal float EnvironmentVolume { get; private set; } = 1f;
        internal float EnvironmentLowPass { get; private set; } = 22000f;
        internal AudioReverbPreset EnvironmentReverb { get; private set; } = AudioReverbPreset.Off;
        public SoundEnvironmentZone CurrentRoom { get; private set; }
        private float nextListenerSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Emitters.Clear(); Zones.Clear(); }
        internal static SoundEngine Ensure()
        {
            if (Instance == null) Instance = FindFirstObjectByType<SoundEngine>();
            if (Instance == null) Instance = new GameObject("Sound Engine").AddComponent<SoundEngine>();
            return Instance;
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        void Update()
        {
            if ((listener == null || !listener.isActiveAndEnabled) && Time.unscaledTime >= nextListenerSearch)
            {
                nextListenerSearch = Time.unscaledTime + 1f;
                foreach (AudioListener candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (candidate.isActiveAndEnabled) { listener = candidate; break; }
            }
            SoundEnvironmentZone best = null;
            if (listener != null)
                foreach (SoundEnvironmentZone zone in Zones)
                    if (zone != null && zone.Contains(listener.transform.position) && (best == null || zone.priority > best.priority)) best = zone;
            EnvironmentVolume = best != null ? best.volume : 1f;
            EnvironmentLowPass = best != null ? best.lowPass : 22000f;
            EnvironmentReverb = best != null ? best.reverb : AudioReverbPreset.Off;
            CurrentRoom = best;
        }
        internal bool HasVoiceBudget(int needed)
        {
            int count = 0;
            foreach (SoundEmitter emitter in Emitters) if (emitter != null) count += emitter.ActiveVoiceCount;
            return count + needed <= maxActiveVoices;
        }
        public void SetTrainMotion(float speed, float turning, float braking)
        {
            RideSpeed = Mathf.Clamp01(speed);
            RideStress = Mathf.Clamp01(Mathf.Abs(turning) + Mathf.Abs(braking));
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
