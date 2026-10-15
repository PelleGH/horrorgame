using UnityEngine;

namespace HorrorAudio
{
    [DisallowMultipleComponent, RequireComponent(typeof(SoundEmitter))]
    [AddComponentMenu("Horror Audio/World Sound Emitter")]
    public class WorldSoundEmitter : MonoBehaviour
    {
        [Tooltip("Uses this object's SoundEmitter file, layers, profile and effects.")]
        public SoundEmitter emitter;
        [Tooltip("Global = audible everywhere. Disable for a positional 3D world source.")]
        public bool globalSound = true;
        [Min(0f)] public float fadeIn = 0.8f;
        [Min(0f)] public float fadeOut = 0.8f;
        [Tooltip("Keep this dedicated root object playing when scenes change.")]
        public bool persistAcrossScenes;
        public bool IsRunning => emitter != null && emitter.ActiveVoiceCount > 0;
        bool requested, originalAutoPlay;
        float envelope, nextAttempt;

        void OnEnable()
        {
            if (emitter == null) emitter = GetComponent<SoundEmitter>();
            originalAutoPlay = emitter.playOnEnable;
            emitter.playOnEnable = false;
            emitter.StopAll();
            emitter.WorldLoop = true;
            emitter.WorldSpatialBlend = globalSound ? 0f : 1f;
            envelope = fadeIn <= 0f ? 1f : 0f;
            emitter.WorldGain = envelope;
            requested = true; nextAttempt = 0f;
            if (persistAcrossScenes)
            {
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
                else Debug.LogWarning("World emitter persistence requires a dedicated root object.", this);
            }
        }
        void Update()
        {
            if (emitter == null) return;
            emitter.WorldSpatialBlend = globalSound ? 0f : 1f;
            float target = requested ? 1f : 0f;
            float duration = requested ? fadeIn : fadeOut;
            envelope = duration <= 0f ? target : Mathf.Lerp(envelope, target,
                1f - Mathf.Exp(-6f * Time.unscaledDeltaTime / duration));
            emitter.WorldGain = envelope;
            if (requested && emitter.isActiveAndEnabled && emitter.ActiveVoiceCount == 0 && Time.unscaledTime >= nextAttempt)
            {
                nextAttempt = Time.unscaledTime + 1f;
                emitter.PlayDefault();
            }
            if (!requested && envelope < 0.001f && (emitter.ActiveVoiceCount > 0 || emitter.DeferredLoopVoiceCount > 0)) emitter.StopAll();
        }
        public void StartWorldSound() { requested = true; nextAttempt = 0f; }
        public void StopWorldSound() { requested = false; }
        void OnDisable()
        {
            if (emitter == null) return;
            emitter.StopAll();
            emitter.WorldLoop = false; emitter.WorldSpatialBlend = -1f; emitter.WorldGain = 1f;
            emitter.playOnEnable = originalAutoPlay;
        }
    }
}
