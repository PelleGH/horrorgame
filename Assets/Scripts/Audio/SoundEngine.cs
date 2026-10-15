using System.Collections.Generic;
using UnityEngine;

namespace HorrorAudio
{
    internal struct SoundVoiceCandidate
    {
        public SoundEmitter Emitter;
        public int Index;
        public int Priority;
        public float Audibility;
        public long Order;
        internal bool Selected;

        public SoundVoiceCandidate(SoundEmitter emitter, int index, int priority, float audibility, long order)
        {
            Emitter = emitter; Index = index; Priority = priority;
            Audibility = audibility; Order = order; Selected = false;
        }
    }

    [DisallowMultipleComponent]
    public class SoundEngine : MonoBehaviour
    {
        public static SoundEngine Instance { get; private set; }
        internal static readonly List<SoundEmitter> Emitters = new List<SoundEmitter>();
        internal static readonly List<SoundEnvironmentZone> Zones = new List<SoundEnvironmentZone>();
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(8, 128)] public int maxActiveVoices = 48;
        [Tooltip("Maximum emitter occlusion checks per frame, shared by all emitters. Waiting emitters retain their last result.")]
        [Range(0, 256)] public int maxOcclusionChecksPerFrame = 32;
        public AudioListener listener;
        public int ActiveVoiceCount
        {
            get { int count = 0; foreach (SoundEmitter emitter in Emitters) if (emitter != null) count += emitter.ActiveVoiceCount; return count; }
        }
        public int VirtualVoiceCount
        {
            get { int count = 0; foreach (SoundEmitter emitter in Emitters) if (emitter != null) count += emitter.VirtualVoiceCount; return count; }
        }
        public int PooledVoiceCount
        {
            get { int count = 0; foreach (SoundEmitter emitter in Emitters) if (emitter != null) count += emitter.PooledVoiceCount; return count; }
        }
        public int DeferredLoopVoiceCount
        {
            get { int count = 0; foreach (SoundEmitter emitter in Emitters) if (emitter != null) count += emitter.DeferredLoopVoiceCount; return count; }
        }
        public int RejectedPlaybackCount { get; private set; }
        public int StolenVoiceCount { get; private set; }
        public int OcclusionChecksThisFrame { get { RefreshOcclusionFrame(); return occlusionChecksThisFrame; } }
        public long OcclusionChecksTotal { get; private set; }
        public float RideSpeed { get; private set; }
        public float RideStress { get; private set; }
        internal float EnvironmentVolume { get; private set; } = 1f;
        internal float EnvironmentLowPass { get; private set; } = 22000f;
        internal AudioReverbPreset EnvironmentReverb { get; private set; } = AudioReverbPreset.Off;
        public SoundEnvironmentZone CurrentRoom { get; private set; }
        private float nextListenerSearch;
        private readonly List<SoundVoiceCandidate> candidates = new List<SoundVoiceCandidate>(128);
        private static readonly System.Comparison<SoundVoiceCandidate> CandidateComparison = CompareCandidates;
        private int occlusionFrame = -1, occlusionChecksThisFrame;

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
            EnforceCurrentLimits();
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
        void EnforceCurrentLimits()
        {
            int count = 0;
            bool localOverflow = false;
            foreach (SoundEmitter emitter in Emitters)
            {
                if (emitter == null) continue;
                int active = emitter.ActiveVoiceCount;
                count += active;
                localOverflow |= active > Mathf.Max(1, emitter.maxVoices);
            }
            int limit = Mathf.Max(1, maxActiveVoices);
            if (count <= limit && !localOverflow) return;
            candidates.Clear();
            foreach (SoundEmitter emitter in Emitters)
                if (emitter != null) emitter.CollectVoiceCandidates(candidates);
            candidates.Sort(CandidateComparison);
            foreach (SoundVoiceCandidate candidate in candidates)
            {
                if (count <= limit && candidate.Emitter.ActiveVoiceCount <= Mathf.Max(1, candidate.Emitter.maxVoices)) continue;
                candidate.Emitter.StopBudgetVoice(candidate.Index);
                StolenVoiceCount++;
                count--;
            }
        }
        internal bool HasVoiceBudget(int needed)
        {
            return ActiveVoiceCount + needed <= Mathf.Max(1, maxActiveVoices);
        }

        // Reserve the complete layered sound before stopping any existing voice. A rejected
        // request cannot cut off the first few voices of an otherwise important sound.
        internal bool TryAcquireVoices(SoundEmitter requester, int required, GameSoundProfile profile, float gain,
            bool localReplacement = false)
        {
            if (requester == null || profile == null || required < 1) return false;
            int globalLimit = Mathf.Max(1, maxActiveVoices);
            int localLimit = Mathf.Max(1, requester.maxVoices);
            if (required > globalLimit || required > localLimit) return RejectPlayback();

            int localShortage = Mathf.Max(0, requester.ActiveVoiceCount + required - localLimit);
            int globalShortage = Mathf.Max(0, ActiveVoiceCount + required - globalLimit);
            if (localShortage == 0 && globalShortage == 0) return true;

            int incomingPriority = requester.GetPlaybackPriority(profile);
            float loudestLayer = 0f;
            if (profile.layers != null)
                foreach (SoundLayer layer in profile.layers)
                    if (layer != null && layer.HasSound) loudestLayer = Mathf.Max(loudestLayer, Mathf.Clamp01(layer.volume));
            float incomingAudibility = requester.EstimateAudibility(profile, gain * loudestLayer);
            candidates.Clear();
            foreach (SoundEmitter emitter in Emitters)
                if (emitter != null) emitter.CollectVoiceCandidates(candidates);
            candidates.Sort(CandidateComparison);

            int selectedCount = 0;
            for (int remaining = localShortage; remaining > 0; remaining--)
            {
                int selectedIndex = -1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    SoundVoiceCandidate candidate = candidates[i];
                    if (candidate.Selected || candidate.Emitter != requester) continue;
                    if (!localReplacement && !CanReplace(candidate, incomingPriority, incomingAudibility)) continue;
                    if (selectedIndex < 0 || localReplacement && candidate.Order < candidates[selectedIndex].Order)
                        selectedIndex = i;
                    if (!localReplacement) break;
                }
                if (selectedIndex < 0) return RejectPlayback();
                SelectCandidate(selectedIndex);
                selectedCount++;
            }

            // Local victims also release global capacity; never count them twice.
            for (int i = 0; i < candidates.Count && selectedCount < globalShortage; i++)
            {
                SoundVoiceCandidate candidate = candidates[i];
                if (candidate.Selected) continue;
                if (!CanReplace(candidate, incomingPriority, incomingAudibility)
                    && !(localReplacement && candidate.Emitter == requester)) continue;
                SelectCandidate(i);
                selectedCount++;
            }
            if (selectedCount < globalShortage) return RejectPlayback();

            foreach (SoundVoiceCandidate candidate in candidates)
                if (candidate.Selected && candidate.Emitter != null)
                {
                    candidate.Emitter.StopBudgetVoice(candidate.Index);
                    StolenVoiceCount++;
                }
            return true;
        }

        private bool RejectPlayback() { RejectedPlaybackCount++; return false; }
        private void SelectCandidate(int index)
        {
            SoundVoiceCandidate candidate = candidates[index];
            candidate.Selected = true;
            candidates[index] = candidate;
        }
        private static bool CanReplace(SoundVoiceCandidate candidate, int incomingPriority, float incomingAudibility)
        {
            return candidate.Priority > incomingPriority || candidate.Priority == incomingPriority
                && incomingAudibility > candidate.Audibility * 1.2f + 0.02f;
        }
        private static int CompareCandidates(SoundVoiceCandidate a, SoundVoiceCandidate b)
        {
            int priority = b.Priority.CompareTo(a.Priority);
            if (priority != 0) return priority;
            int audibility = a.Audibility.CompareTo(b.Audibility);
            return audibility != 0 ? audibility : a.Order.CompareTo(b.Order);
        }
        private void RefreshOcclusionFrame()
        {
            if (occlusionFrame == Time.frameCount) return;
            occlusionFrame = Time.frameCount;
            occlusionChecksThisFrame = 0;
        }
        internal bool CanCheckOcclusion()
        {
            RefreshOcclusionFrame();
            if (occlusionChecksThisFrame >= Mathf.Max(0, maxOcclusionChecksPerFrame)) return false;
            occlusionChecksThisFrame++;
            OcclusionChecksTotal++;
            return true;
        }
        public void ResetStatistics()
        {
            RejectedPlaybackCount = 0;
            StolenVoiceCount = 0;
            OcclusionChecksTotal = 0;
            occlusionFrame = Time.frameCount;
            occlusionChecksThisFrame = 0;
        }
        public void SetTrainMotion(float speed, float turning, float braking)
        {
            RideSpeed = Mathf.Clamp01(speed);
            RideStress = Mathf.Clamp01(Mathf.Abs(turning) + Mathf.Abs(braking));
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
