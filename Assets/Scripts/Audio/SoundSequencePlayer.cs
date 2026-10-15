using System.Collections;
using System.Linq;
using UnityEngine;

namespace HorrorAudio
{
    [DisallowMultipleComponent, RequireComponent(typeof(SoundEmitter))]
    [AddComponentMenu("Horror Audio/Sound Sequence Player")]
    public class SoundSequencePlayer : MonoBehaviour
    {
        public SoundEmitter emitter;
        public SoundEvent sound = new SoundEvent();
        Coroutine pending;
        void Awake() { if (emitter == null) emitter = GetComponent<SoundEmitter>(); }
        public void Play() => PlayEvent(sound);
        public void PlaySequence(SoundSequence sequence) => PlayEvent(new SoundEvent { sequence = sequence });
        public void PlayEvent(SoundEvent selected)
        {
            CancelPending();
            if (!isActiveAndEnabled || selected == null) return;
            if (emitter == null) emitter = GetComponent<SoundEmitter>();
            if (selected.sequence != null) pending = StartCoroutine(Run(selected));
            else new SoundStep {clip = selected.clip, profile = selected.profile,
                useEmitterSound = selected.useEmitterSound, volume = selected.volume, pitch = selected.pitch}.Play(emitter);
        }
        IEnumerator Run(SoundEvent selected)
        {
            var sequence = selected.sequence;
            var steps = (sequence.steps ?? System.Array.Empty<SoundStep>()).Where(step => step != null)
                .OrderBy(step => step.delay).ToArray();
            float elapsed = 0f;
            foreach (var step in steps)
            {
                while (elapsed < Mathf.Max(0f, step.delay))
                {
                    yield return null;
                    elapsed += sequence.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                }
                new SoundStep {clip = step.clip, profile = step.profile, useEmitterSound = step.useEmitterSound,
                    volume = step.volume * selected.volume, pitch = step.pitch * selected.pitch}.Play(emitter);
            }
            pending = null;
        }
        public void CancelPending()
        {
            if (pending != null) StopCoroutine(pending);
            pending = null;
        }
        public void Stop() { CancelPending(); if (emitter != null) emitter.StopAll(); }
        void OnDisable() => Stop();
    }
}
