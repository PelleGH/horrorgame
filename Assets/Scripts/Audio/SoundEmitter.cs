using System.Collections.Generic;
using UnityEngine;

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
        public bool playOnEnable;
        [Range(1, 24)] public int maxVoices = 8;
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
        public int ActiveVoiceCount
        {
            get { int count = 0; foreach (Voice voice in voices) if (voice.Busy) count++; return count; }
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
        void OnEnable()
        {
            SoundEngine.Emitters.Add(this);
            SoundEngine.Ensure();
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
                if (forceOneShot) directEventProfile = selected; else directProfile = selected;
            }
            soundSettings.Apply(selected);
            if (forceOneShot) selected.loop = false;
            var clips = new List<AudioClip>();
            if (audioClip != null) clips.Add(audioClip);
            if (clipVariations != null) foreach (AudioClip clip in clipVariations) if (clip != null) clips.Add(clip);
            var layers = new List<SoundLayer>();
            if (clips.Count > 0) layers.Add(new SoundLayer { name = "Your audio", clips = clips.ToArray(), volume = 1f, pitch = soundSettings.pitchVariation });
            if (extraLayers != null) foreach (SoundLayer layer in extraLayers) if (layer != null) layers.Add(layer);
            selected.layers = layers.ToArray();
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
                clipProfiles.Add(clip, profile);
            }
            soundSettings.Apply(profile); profile.loop = false;
            profile.layers[0].pitch = soundSettings.pitchVariation;
            var layers = new List<SoundLayer> { profile.layers[0] };
            if (extraLayers != null) foreach (SoundLayer layer in extraLayers) if (layer != null) layers.Add(layer);
            profile.layers = layers.ToArray();
            return Play(profile, gain, pitchMultiplier);
        }
        public void StopAll() { foreach (Voice voice in voices) if (voice.source != null) voice.source.Stop(); }
        public void SetIntensity(float value) { volume = Mathf.Clamp(value, 0f, 2f); }
        public void SetSimulatedRelativeSpeed(float metresPerSecond) { relativeSpeed = metresPerSecond; useSimulatedDoppler = true; }

        public bool Play(GameSoundProfile profile, float gain = 1f, float pitchMultiplier = 1f)
        {
            if (!isActiveAndEnabled || profile == null || profile.layers == null) return false;
            if (profile.loop)
                foreach (Voice voice in voices) if (voice.Busy && voice.profile == profile) return true;
            int required = 0;
            foreach (SoundLayer layer in profile.layers)
                if (layer != null && layer.HasSound) required++;
            if (required == 0 || required > maxVoices || !SoundEngine.Ensure().HasVoiceBudget(required)) return false;
            int free = maxVoices - ActiveVoiceCount;
            if (free < required) return false;
            bool played = false;
            foreach (SoundLayer layer in profile.layers)
            {
                if (layer == null) continue;
                AudioClip clip = layer.NextClip(profile.loop);
                if (clip == null) continue;
                Voice voice = FreeVoice();
                voice.profile = profile;
                voice.layer = layer;
                voice.gain = Mathf.Max(0f, gain);
                voice.basePitch = Mathf.Clamp(Random.Range(Mathf.Min(layer.pitch.x, layer.pitch.y), Mathf.Max(layer.pitch.x, layer.pitch.y)) * pitchMultiplier, 0.1f, 3f);
                AudioSource source = voice.source;
                source.clip = clip;
                source.loop = profile.loop;
                source.spatialBlend = profile.spatialBlend;
                source.minDistance = Mathf.Max(0.01f, profile.minDistance);
                source.maxDistance = Mathf.Max(source.minDistance, profile.maxDistance);
                source.priority = profile.priority;
                source.outputAudioMixerGroup = profile.output;
                Apply(voice, 1f);
                voice.tone.ResetState();
                source.Play();
                played = true;
            }
            return played;
        }
        Voice FreeVoice()
        {
            foreach (Voice voice in voices) if (!voice.Busy) return voice;
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
        void Update()
        {
            if (directProfile != null) soundSettings.Apply(directProfile);
            if (directEventProfile != null) { soundSettings.Apply(directEventProfile); directEventProfile.loop = false; }
            foreach (GameSoundProfile profile in clipProfiles.Values) { soundSettings.Apply(profile); profile.loop = false; }
            SoundEngine engine = SoundEngine.Ensure();
            if (occlusion && engine.listener != null && Time.unscaledTime >= nextOcclusionCheck)
            {
                nextOcclusionCheck = Time.unscaledTime + 0.15f;
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
            float blend = 1f - Mathf.Exp(-effectSmoothing * Time.unscaledDeltaTime);
            foreach (Voice voice in voices) if (voice.Busy && voice.profile != null) Apply(voice, blend);
        }
        void Apply(Voice voice, float blend)
        {
            SoundEngine engine = SoundEngine.Ensure();
            GameSoundProfile profile = voice.profile;
            float rideGain = respondToTrainMotion ? Mathf.Lerp(rideVolume.x, rideVolume.y, Mathf.Max(engine.RideSpeed, engine.RideStress)) : 1f;
            float rideTone = respondToTrainMotion ? Mathf.Lerp(ridePitch.x, ridePitch.y, engine.RideSpeed) : 1f;
            float doppler = useSimulatedDoppler ? Mathf.Clamp(343f / (343f - Mathf.Clamp(relativeSpeed, -150f, 150f)), 0.5f, 2f) : 1f;
            voice.source.dopplerLevel = useSimulatedDoppler ? 0f : profile.dopplerLevel;
            float gain = profile.volume * voice.gain * Mathf.Clamp01(voice.layer.volume) * volume * engine.masterVolume * rideGain
                * (useEnvironment ? engine.EnvironmentVolume : 1f) * (blocked && profile.spatialBlend > 0f ? transmissionGain : 1f);
            voice.source.volume = Mathf.Lerp(voice.source.volume, Mathf.Clamp01(gain), blend);
            voice.source.pitch = Mathf.Lerp(voice.source.pitch, Mathf.Clamp(voice.basePitch * pitch * rideTone * doppler, 0.1f, 3f), blend);
            voice.bass = Mathf.Lerp(voice.bass, profile.bassDb, blend);
            voice.treble = Mathf.Lerp(voice.treble, profile.trebleDb, blend);
            voice.tone.Configure(voice.bass, voice.treble);
            voice.distortion.enabled = profile.distortion > 0.001f;
            voice.distortion.distortionLevel = Mathf.Lerp(voice.distortion.distortionLevel, profile.distortion, blend);
            float cutoff = Mathf.Min(profile.lowPass, useEnvironment ? engine.EnvironmentLowPass : 22000f,
                blocked && profile.spatialBlend > 0f ? transmissionCutoff : 22000f);
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
