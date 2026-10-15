using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace HorrorAudio
{
    [DisallowMultipleComponent, AddComponentMenu("Horror Audio/Sound Emitter")]
    public class SoundEmitter : MonoBehaviour
    {
        public GameSoundProfile defaultProfile;
        [Header("Your own audio (overrides Default Profile for default playback)")]
        public AudioClip audioClip;
        public AudioClip[] clipVariations;
        public SoundLayer[] extraLayers;
        public ObjectSoundSettings soundSettings = new ObjectSoundSettings();
        [Tooltip("Original source retained for rolloff curves and specialised playback flags; it does not play alongside the engine.")]
        public AudioSource legacyTemplate;
        public bool playOnEnable;
        [Range(1, 24)] public int maxVoices = 8;
        [Tooltip("Lower numbers are more important. -1 uses the original source or profile priority.")]
        [Range(-1, 256)] public int priority = -1;
        [Tooltip("Prepare reusable voice slots when enabled. This is not a concurrent playback limit.")]
        [Range(0, 24)] public int prewarmVoices;
        [Tooltip("Base wall-check interval. Distant sounds are checked less frequently.")]
        [Min(0.03f)] public float occlusionInterval = 0.15f;
        [Range(0f, 2f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Min(0.1f)] public float effectSmoothing = 8f;
        [Header("Environment")]
        public bool useEnvironment = true;
        public bool occlusion;
        public LayerMask occlusionMask = ~0;
        [Range(0f, 1f)] public float occludedVolume = 0.35f;
        [Range(10f, 22000f)] public float occludedLowPass = 1600f;
        [Header("Stationary train simulation (optional)")]
        public bool respondToTrainMotion;
        public Vector2 rideVolume = new Vector2(0.2f, 1f);
        public Vector2 ridePitch = new Vector2(0.8f, 1.3f);
        public bool useSimulatedDoppler;
        [Tooltip("Positive means approaching the listener, in metres/second.")]
        public float relativeSpeed;
        public bool HasDirectSound
        {
            get
            {
                if (audioClip != null) return true;
                if (clipVariations != null) foreach (AudioClip clip in clipVariations) if (clip != null) return true;
                if (extraLayers != null) foreach (SoundLayer layer in extraLayers) if (layer != null && layer.HasSound) return true;
                return false;
            }
        }
        public string LastPlaybackStatus { get; private set; } = "No playback requested.";
        internal bool WorldLoop;
        internal float WorldSpatialBlend = -1f;
        internal float WorldGain = 1f;
        public int ActiveVoiceCount
        {
            get { int count = 0; foreach (Voice voice in voices) if (voice.Busy) count++; return count; }
        }
        public int PooledVoiceCount => voices.Count;
        public int DeferredLoopVoiceCount
        {
            get { int count = 0; foreach (Voice voice in voices) if (voice.suspended) count++; return count; }
        }
        public int VirtualVoiceCount
        {
            get { int count = 0; foreach (Voice voice in voices) if (voice.Busy && voice.source.isVirtual) count++; return count; }
        }
        class Voice
        {
            public AudioSource source;
            public AudioDistortionFilter distortion;
            public AudioLowPassFilter low;
            public AudioHighPassFilter high;
            public AudioReverbFilter reverb;
            public AudioEchoFilter echo;
            public SoundToneFilter tone;
            public GameSoundProfile profile;
            public SoundLayer layer;
            public float gain, basePitch;
            public float bass, treble;
            public float eventDistortion, eventLowPass = 22000f;
            public AudioSource appliedTemplate;
            public long order;
            public float nextControlUpdate, lastControlUpdate;
            public bool suspended;
            public float suspendedPosition, suspendedPitch;
            public double suspendedAt;
            public AudioReverbPreset appliedReverb = AudioReverbPreset.Off;
            public bool Busy => source != null && source.isPlaying;
        }
        readonly List<Voice> voices = new List<Voice>();
        readonly RaycastHit[] hits = new RaycastHit[16];
        bool blocked;
        float transmissionGain = 1f, transmissionCutoff = 22000f;
        float nextOcclusionCheck;
        GameSoundProfile directProfile, directEventProfile;
        readonly Dictionary<AudioClip, GameSoundProfile> clipProfiles = new Dictionary<AudioClip, GameSoundProfile>();
        static readonly ProfilerMarker PlaybackMarker = new ProfilerMarker("HorrorAudio.Playback");
        static readonly ProfilerMarker ControlMarker = new ProfilerMarker("HorrorAudio.ControlUpdate");
        static readonly ProfilerMarker OcclusionMarker = new ProfilerMarker("HorrorAudio.Occlusion");
        long playOrder;
        float nextLoopResume;
        readonly HashSet<GameSoundProfile> objectProfiles = new HashSet<GameSoundProfile>();
        readonly HashSet<GameSoundProfile> updatedProfiles = new HashSet<GameSoundProfile>();
        void OnEnable()
        {
            SoundEngine.Emitters.Add(this);
            SoundEngine.Ensure();
            nextOcclusionCheck = Time.unscaledTime + (Mathf.Abs(GetInstanceID() % 997) / 997f) * occlusionInterval;
            Prewarm();
            if (playOnEnable) PlayDefault();
        }
        void OnDisable() { StopAll(); SoundEngine.Emitters.Remove(this); }
        public void PlayDefault() { PlayDefaultSound(); }
        public bool PlayDefaultSound(float gain = 1f, float pitchMultiplier = 1f, bool forceOneShot = false)
        {
            if (!HasDirectSound) return Play(defaultProfile, gain, pitchMultiplier);
            GameSoundProfile selected = forceOneShot ? directEventProfile : directProfile;
            if (selected == null)
            {
                selected = ScriptableObject.CreateInstance<GameSoundProfile>();
                selected.name = name + " object sound"; selected.hideFlags = HideFlags.DontSave;
                selected.layers = System.Array.Empty<SoundLayer>();
                if (forceOneShot) directEventProfile = selected; else directProfile = selected;
                objectProfiles.Add(selected);
            }
            soundSettings.Apply(selected);
            if (forceOneShot) selected.loop = false;
            ConfigureObjectLayers(selected, audioClip, clipVariations, true);
            return Play(selected, gain, pitchMultiplier);
        }
        public bool PlayClip(AudioClip clip, float gain = 1f, float pitchMultiplier = 1f)
        {
            if (clip == null) return false;
            if (!clipProfiles.TryGetValue(clip, out GameSoundProfile profile))
            {
                profile = ScriptableObject.CreateInstance<GameSoundProfile>();
                profile.name = clip.name + " object event"; profile.hideFlags = HideFlags.DontSave;
                profile.layers = new[] { new SoundLayer { clips = new[] {clip}, volume = 1f, pitch = soundSettings.pitchVariation } };
                clipProfiles.Add(clip, profile); objectProfiles.Add(profile);
            }
            soundSettings.Apply(profile); profile.loop = false;
            ConfigureObjectLayers(profile, clip, null, true);
            return Play(profile, gain, pitchMultiplier);
        }
        // Rebuild arrays only when their actual contents change, including Inspector edits.
        void ConfigureObjectLayers(GameSoundProfile profile, AudioClip first, AudioClip[] variations, bool includeExtras)
        {
            SoundLayer primary = profile.layers != null && profile.layers.Length > 0 ? profile.layers[0] : null;
            if (primary == null) primary = new SoundLayer { name = "Your audio", volume = 1f };
            int count = first != null ? 1 : 0;
            if (variations != null) foreach (AudioClip clip in variations) if (clip != null) count++;
            bool changed = primary.clips == null || primary.clips.Length != count;
            int index = 0;
            if (!changed)
            {
                if (first != null) changed = primary.clips[index++] != first;
                if (variations != null) foreach (AudioClip clip in variations)
                    if (clip != null) { if (primary.clips[index] != clip) changed = true; index++; }
            }
            if (changed)
            {
                primary.clips = new AudioClip[count]; index = 0;
                if (first != null) primary.clips[index++] = first;
                if (variations != null) foreach (AudioClip clip in variations) if (clip != null) primary.clips[index++] = clip;
            }
            primary.pitch = soundSettings.pitchVariation;
            count = 1;
            if (includeExtras && extraLayers != null) foreach (SoundLayer layer in extraLayers) if (layer != null) count++;
            changed = profile.layers == null || profile.layers.Length != count || profile.layers[0] != primary;
            index = 1;
            if (!changed && includeExtras && extraLayers != null) foreach (SoundLayer layer in extraLayers)
                if (layer != null) { if (profile.layers[index] != layer) changed = true; index++; }
            if (changed)
            {
                profile.layers = new SoundLayer[count]; profile.layers[0] = primary; index = 1;
                if (includeExtras && extraLayers != null) foreach (SoundLayer layer in extraLayers) if (layer != null) profile.layers[index++] = layer;
            }
        }

        internal int GetPlaybackPriority(GameSoundProfile profile)
            => priority >= 0 ? priority : legacyTemplate != null ? legacyTemplate.priority : profile.priority;

        internal float EstimateAudibility(GameSoundProfile profile, float gain, bool includeOcclusion = true)
        {
            float result = Mathf.Clamp01(profile.volume * gain * volume * WorldGain);
            var engine = SoundEngine.Instance;
            if (engine == null || engine.listener == null) return result;
            float spatial = WorldSpatialBlend >= 0f ? WorldSpatialBlend : profile.spatialBlend;
            float distance = Vector3.Distance(transform.position, engine.listener.transform.position);
            float attenuation = Mathf.Clamp01(Mathf.Max(0.01f, profile.minDistance) / Mathf.Max(0.01f, distance));
            return result * Mathf.Lerp(1f, attenuation, spatial) * (includeOcclusion && blocked && spatial > 0f ? transmissionGain : 1f);
        }
        internal void CollectVoiceCandidates(List<SoundVoiceCandidate> candidates)
        {
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (voice.Busy && voice.profile != null)
                    candidates.Add(new SoundVoiceCandidate(this, i, voice.source.priority,
                        EstimateAudibility(voice.profile, voice.gain * Mathf.Clamp01(voice.layer.volume)), voice.order));
            }
        }
        internal void StopBudgetVoice(int index)
        {
            Voice voice = voices[index];
            if (voice.source.loop)
            {
                voice.suspended = true; voice.suspendedPosition = voice.source.time;
                voice.suspendedPitch = voice.source.pitch; voice.suspendedAt = AudioSettings.dspTime;
            }
            voice.source.Stop();
            LastPlaybackStatus = "Lower-priority voice replaced by a more important sound.";
        }

        public void StopAll()
        {
            foreach (Voice voice in voices) { voice.suspended = false; if (voice.source != null) voice.source.Stop(); }
            LastPlaybackStatus = "Stopped.";
        }
        public void SetIntensity(float value) { volume = Mathf.Clamp(value, 0f, 2f); }
        public void SetSimulatedRelativeSpeed(float metresPerSecond) { relativeSpeed = metresPerSecond; useSimulatedDoppler = true; }

        public void ImportSourceSettings(AudioSource source, bool includeClip = true, bool includeVolume = true)
        {
            if (source == null) return;
            legacyTemplate = source;
            if (includeClip && !HasDirectSound && source.clip != null) audioClip = source.clip;
            soundSettings.volume = includeVolume ? source.volume : 1f;
            soundSettings.pitchVariation = Vector2.one;
            pitch = includeVolume ? source.pitch : 1f;
            soundSettings.loop = source.loop; soundSettings.spatialBlend = source.spatialBlend;
            soundSettings.minDistance = source.minDistance; soundSettings.maxDistance = source.maxDistance;
            soundSettings.doppler = source.dopplerLevel; soundSettings.output = source.outputAudioMixerGroup;
            var distortion = source.GetComponent<AudioDistortionFilter>();
            if (distortion != null && distortion.enabled) soundSettings.distortion = distortion.distortionLevel;
            var low = source.GetComponent<AudioLowPassFilter>();
            if (low != null && low.enabled) soundSettings.lowPass = low.cutoffFrequency;
            var high = source.GetComponent<AudioHighPassFilter>();
            if (high != null && high.enabled) soundSettings.highPass = high.cutoffFrequency;
            var echo = source.GetComponent<AudioEchoFilter>();
            if (echo != null && echo.enabled)
            { soundSettings.echoWet = echo.wetMix; soundSettings.echoDelay = echo.delay; soundSettings.echoDecay = echo.decayRatio; }
            var reverb = source.GetComponent<AudioReverbFilter>();
            if (reverb != null && reverb.enabled)
            { soundSettings.reverb = reverb.reverbPreset; soundSettings.reverbLevel = reverb.reverbLevel; }
        }
        public bool PlayVoiceClip(AudioClip clip, float gain, float pitchMultiplier, float distortion, float cutoff)
        {
            if (clip == null) return false;
            if (!clipProfiles.TryGetValue(clip, out GameSoundProfile profile))
            {
                profile = ScriptableObject.CreateInstance<GameSoundProfile>(); profile.hideFlags = HideFlags.DontSave;
                profile.layers = new[] {new SoundLayer {clips = new[] {clip}, volume = 1f, pitch = Vector2.one}};
                clipProfiles.Add(clip, profile); objectProfiles.Add(profile);
            }
            soundSettings.Apply(profile); profile.loop = false;
            ConfigureObjectLayers(profile, clip, null, false); profile.layers[0].pitch = Vector2.one;
            return PlayInternal(profile, gain, pitchMultiplier, distortion, cutoff, true);
        }

        public bool Play(GameSoundProfile profile, float gain = 1f, float pitchMultiplier = 1f)
            => PlayInternal(profile, gain, pitchMultiplier);
        bool PlayInternal(GameSoundProfile profile, float gain, float pitchMultiplier, float eventDistortion = 0f,
            float eventLowPass = 22000f, bool replaceOldest = false)
        {
            using var playbackSample = PlaybackMarker.Auto();
            if (!isActiveAndEnabled) { LastPlaybackStatus = "Emitter is disabled."; return false; }
            if (profile == null || profile.layers == null) { LastPlaybackStatus = "Assign an Audio Clip or Sound Profile."; return false; }
            if (profile.loop || WorldLoop)
                foreach (Voice voice in voices)
                    if ((voice.Busy || voice.suspended) && voice.profile == profile) { LastPlaybackStatus = "This loop is already playing."; return true; }
            int required = 0;
            foreach (SoundLayer layer in profile.layers)
                if (layer != null && layer.HasSound) required++;
            if (required == 0) { LastPlaybackStatus = "No playable clips or generators in this sound."; return false; }
            if (required > maxVoices)
            { LastPlaybackStatus = "Too many layers for this emitter's Max Voices."; return false; }
            if (!SoundEngine.Ensure().TryAcquireVoices(this, required, profile, gain, replaceOldest))
            { LastPlaybackStatus = "Voice budget kept more important or equally audible sounds."; return false; }
            bool played = false;
            foreach (SoundLayer layer in profile.layers)
            {
                if (layer == null) continue;
                AudioClip clip = layer.NextClip(profile.loop || WorldLoop);
                if (clip == null) continue;
                Voice voice = FreeVoice();
                voice.suspended = false;
                voice.order = ++playOrder;
                voice.profile = profile;
                voice.layer = layer;
                voice.eventDistortion = eventDistortion; voice.eventLowPass = eventLowPass;
                voice.gain = Mathf.Max(0f, gain);
                voice.basePitch = Mathf.Clamp(Random.Range(Mathf.Min(layer.pitch.x, layer.pitch.y), Mathf.Max(layer.pitch.x, layer.pitch.y)) * pitchMultiplier, 0.1f, 3f);
                AudioSource source = voice.source;
                source.clip = clip;
                source.loop = profile.loop || WorldLoop;
                source.spatialBlend = WorldSpatialBlend >= 0f ? WorldSpatialBlend : profile.spatialBlend;
                source.minDistance = Mathf.Max(0.01f, profile.minDistance);
                source.maxDistance = Mathf.Max(source.minDistance, profile.maxDistance);
                source.priority = GetPlaybackPriority(profile);
                source.outputAudioMixerGroup = profile.output;
                if (legacyTemplate != null && voice.appliedTemplate != legacyTemplate)
                {
                    source.rolloffMode = legacyTemplate.rolloffMode;
                    foreach (AudioSourceCurveType curve in System.Enum.GetValues(typeof(AudioSourceCurveType)))
                        source.SetCustomCurve(curve, legacyTemplate.GetCustomCurve(curve));
                    source.panStereo = legacyTemplate.panStereo; source.spread = legacyTemplate.spread;
                    source.reverbZoneMix = legacyTemplate.reverbZoneMix;
                    source.mute = legacyTemplate.mute; source.ignoreListenerPause = legacyTemplate.ignoreListenerPause;
                    source.ignoreListenerVolume = legacyTemplate.ignoreListenerVolume;
                    source.velocityUpdateMode = legacyTemplate.velocityUpdateMode;
                    source.spatialize = legacyTemplate.spatialize; source.spatializePostEffects = legacyTemplate.spatializePostEffects;
                    voice.appliedTemplate = legacyTemplate;
                }
                voice.lastControlUpdate = Time.unscaledTime; voice.nextControlUpdate = 0f;
                Apply(voice, 1f);
                voice.tone.ResetState();
                source.Play();
                played = true;
            }
            LastPlaybackStatus = played ? "Playing " + required + " layer(s)." : "No playable audio.";
            return played;
        }
        public void Prewarm(int count = -1)
        {
            int target = Mathf.Clamp(count < 0 ? prewarmVoices : count, 0, maxVoices);
            while (voices.Count < target) CreateVoice();
        }
        Voice FreeVoice()
        {
            foreach (Voice voice in voices) if (!voice.Busy && !voice.suspended) return voice;
            if (voices.Count >= maxVoices)
                foreach (Voice voice in voices) if (!voice.Busy) { voice.suspended = false; return voice; }
            return CreateVoice();
        }
        Voice CreateVoice()
        {
            GameObject slot = new GameObject("Sound voice " + voices.Count);
            slot.transform.SetParent(transform, false);
            var created = new Voice
            {
                source = slot.AddComponent<AudioSource>(), tone = slot.AddComponent<SoundToneFilter>(), distortion = slot.AddComponent<AudioDistortionFilter>(),
                low = slot.AddComponent<AudioLowPassFilter>(), high = slot.AddComponent<AudioHighPassFilter>(),
                reverb = slot.AddComponent<AudioReverbFilter>(), echo = slot.AddComponent<AudioEchoFilter>()
            };
            created.source.playOnAwake = false;
            voices.Add(created);
            return created;
        }
        void ResumeDeferredLoops()
        {
            if (Time.unscaledTime < nextLoopResume) return;
            nextLoopResume = Time.unscaledTime + 0.35f + Mathf.Abs(GetInstanceID() % 101) * 0.002f;
            var engine = SoundEngine.Instance;
            if (engine == null) return;
            foreach (Voice voice in voices)
            {
                // Resume only in free capacity; background retries must not evict other sounds.
                if (!voice.suspended || voice.source.clip == null || voice.profile == null) continue;
                if (ActiveVoiceCount >= maxVoices || !engine.HasVoiceBudget(1)) break;
                double elapsed = System.Math.Max(0, AudioSettings.dspTime - voice.suspendedAt);
                float position = (float)((voice.suspendedPosition + elapsed * voice.suspendedPitch) % voice.source.clip.length);
                Apply(voice, 1f); voice.tone.ResetState(); voice.source.time = position;
                voice.source.Play(); voice.suspended = false;
                voice.lastControlUpdate = Time.unscaledTime; voice.nextControlUpdate = 0f;
            }
        }
        void Update()
        {
            ResumeDeferredLoops();
            if (ActiveVoiceCount == 0) return;
            using var controlSample = ControlMarker.Auto();
            updatedProfiles.Clear();
            bool spatialActive = false;
            float nearestSpatial = float.PositiveInfinity;
            SoundEngine engine = SoundEngine.Ensure();
            foreach (Voice voice in voices)
            {
                if (!voice.Busy || voice.profile == null) continue;
                if (objectProfiles.Contains(voice.profile) && updatedProfiles.Add(voice.profile))
                {
                    soundSettings.Apply(voice.profile);
                    if (voice.profile != directProfile) voice.profile.loop = false;
                }
                float spatial = WorldSpatialBlend >= 0f ? WorldSpatialBlend : voice.profile.spatialBlend;
                // A wall-muted virtual voice still needs occasional checks so opening the wall can restore it.
                if (spatial > 0f && (!voice.source.isVirtual || blocked) && engine.listener != null)
                {
                    float distance = Vector3.Distance(transform.position, engine.listener.transform.position);
                    if (distance <= voice.profile.maxDistance && EstimateAudibility(voice.profile, voice.gain, false) > 0.001f)
                    { spatialActive = true; nearestSpatial = Mathf.Min(nearestSpatial, distance); }
                }
            }
            if (occlusion && spatialActive && engine.listener != null && Time.unscaledTime >= nextOcclusionCheck && engine.CanCheckOcclusion())
            {
                using var occlusionSample = OcclusionMarker.Auto();
                float distanceRate = nearestSpatial > 60f ? 6f : nearestSpatial > 20f ? 3f : 1f;
                float stagger = 1f + (Mathf.Abs(GetInstanceID() % 101) / 101f) * 0.15f;
                nextOcclusionCheck = Time.unscaledTime + Mathf.Max(0.03f, occlusionInterval) * distanceRate * stagger;
                Vector3 direction = engine.listener.transform.position - transform.position;
                int count = Physics.RaycastNonAlloc(transform.position, direction.normalized, hits, direction.magnitude,
                    occlusionMask, QueryTriggerInteraction.Ignore);
                blocked = false;
                transmissionGain = 1f; transmissionCutoff = 22000f;
                CharacterController listenerBody = engine.listener.GetComponentInParent<CharacterController>();
                Transform listenerRoot = listenerBody != null ? listenerBody.transform : engine.listener.transform;
                for (int i = 0; i < count; i++)
                    if (!hits[i].transform.IsChildOf(transform) && !engine.listener.transform.IsChildOf(hits[i].transform)
                        && !hits[i].transform.IsChildOf(listenerRoot))
                    {
                        blocked = true;
                        var material = hits[i].collider.GetComponentInParent<SoundAcousticMaterial>();
                        if (material != null)
                        {
                            float thickness = material.MeasureThickness(hits[i], engine.listener.transform.position, direction.normalized, direction.magnitude);
                            transmissionGain *= material.TransmissionGain(thickness);
                            transmissionCutoff = Mathf.Min(transmissionCutoff, material.TransmissionCutoff(thickness));
                        }
                        else
                        {
                            transmissionGain *= occludedVolume;
                            transmissionCutoff = Mathf.Min(transmissionCutoff, occludedLowPass);
                        }
                    }
            }
            else if (!occlusion) blocked = false;
            foreach (Voice voice in voices)
            {
                if (!voice.Busy || voice.profile == null || Time.unscaledTime < voice.nextControlUpdate) continue;
                float distance = engine.listener != null ? Vector3.Distance(transform.position, engine.listener.transform.position) : 0f;
                float interval = voice.source.isVirtual ? 0.25f : distance > 25f && voice.source.spatialBlend > 0f ? 0.1f : 0f;
                float elapsed = Mathf.Max(Time.unscaledDeltaTime, Time.unscaledTime - voice.lastControlUpdate);
                voice.lastControlUpdate = Time.unscaledTime; voice.nextControlUpdate = Time.unscaledTime + interval;
                Apply(voice, 1f - Mathf.Exp(-effectSmoothing * elapsed));
            }
        }

        void Apply(Voice voice, float blend)
        {
            SoundEngine engine = SoundEngine.Ensure();
            GameSoundProfile profile = voice.profile;
            float spatialBlend = WorldSpatialBlend >= 0f ? WorldSpatialBlend : profile.spatialBlend;
            voice.source.spatialBlend = spatialBlend;
            voice.source.priority = GetPlaybackPriority(profile);
            float rideGain = respondToTrainMotion ? Mathf.Lerp(rideVolume.x, rideVolume.y, Mathf.Max(engine.RideSpeed, engine.RideStress)) : 1f;
            float rideTone = respondToTrainMotion ? Mathf.Lerp(ridePitch.x, ridePitch.y, engine.RideSpeed) : 1f;
            float doppler = useSimulatedDoppler ? Mathf.Clamp(343f / (343f - Mathf.Clamp(relativeSpeed, -150f, 150f)), 0.5f, 2f) : 1f;
            voice.source.dopplerLevel = useSimulatedDoppler ? 0f : profile.dopplerLevel;
            float gain = profile.volume * voice.gain * Mathf.Clamp01(voice.layer.volume) * volume * WorldGain * engine.masterVolume * rideGain
                * (useEnvironment ? engine.EnvironmentVolume : 1f) * (blocked && spatialBlend > 0f ? transmissionGain : 1f);
            voice.source.volume = Mathf.Lerp(voice.source.volume, Mathf.Clamp01(gain), blend);
            voice.source.pitch = Mathf.Lerp(voice.source.pitch, Mathf.Clamp(voice.basePitch * pitch * rideTone * doppler, 0.1f, 3f), blend);
            voice.bass = Mathf.Lerp(voice.bass, profile.bassDb, blend);
            voice.treble = Mathf.Lerp(voice.treble, profile.trebleDb, blend);
            voice.tone.Configure(voice.bass, voice.treble);
            float distortion = Mathf.Clamp01(profile.distortion + voice.eventDistortion);
            voice.distortion.enabled = distortion > 0.001f;
            voice.distortion.distortionLevel = Mathf.Lerp(voice.distortion.distortionLevel, distortion, blend);
            float cutoff = Mathf.Min(Mathf.Min(profile.lowPass, voice.eventLowPass), useEnvironment ? engine.EnvironmentLowPass : 22000f,
                blocked && spatialBlend > 0f ? transmissionCutoff : 22000f);
            voice.low.cutoffFrequency = Mathf.Lerp(voice.low.cutoffFrequency, cutoff, blend);
            voice.low.enabled = voice.low.cutoffFrequency < 21900f;
            voice.high.enabled = profile.highPass > 10f;
            voice.high.cutoffFrequency = profile.highPass;
            SoundEnvironmentZone room = useEnvironment ? engine.CurrentRoom : null;
            bool calculatedRoom = room != null && room.calculateFromRoomSize;
            AudioReverbPreset preset = calculatedRoom ? AudioReverbPreset.User : useEnvironment && engine.EnvironmentReverb != AudioReverbPreset.Off ? engine.EnvironmentReverb : profile.reverb;
            voice.reverb.enabled = preset != AudioReverbPreset.Off;
            if (voice.appliedReverb != preset)
            {
                voice.reverb.reverbPreset = preset;
                voice.appliedReverb = preset;
            }
            if (calculatedRoom) room.ApplyRoomReverb(voice.reverb, blend);
            else if (preset != AudioReverbPreset.Off) voice.reverb.reverbLevel = profile.reverbLevel;
            bool roomEcho = calculatedRoom && room.roomEchoWet > profile.echoWet;
            float echoWet = roomEcho ? room.roomEchoWet : profile.echoWet;
            voice.echo.enabled = echoWet > 0.001f;
            voice.echo.wetMix = echoWet;
            voice.echo.delay = roomEcho ? room.EchoDelayMs : profile.echoDelay;
            voice.echo.decayRatio = roomEcho ? room.roomEchoDecay : profile.echoDecay;
        }
        void OnDestroy()
        {
            if (directProfile != null) Destroy(directProfile);
            if (directEventProfile != null) Destroy(directEventProfile);
            foreach (GameSoundProfile profile in clipProfiles.Values) if (profile != null) Destroy(profile);
            if (extraLayers != null) foreach (SoundLayer layer in extraLayers) if (layer != null) layer.ReleaseGenerated();
        }
    }
}
