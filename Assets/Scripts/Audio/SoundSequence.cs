using System;
using UnityEngine;

namespace HorrorAudio
{
    [Serializable]
    public class SoundStep
    {
        [Tooltip("Seconds from the beginning of the sequence; equal times play together.")]
        [Min(0f)] public float delay;
        public AudioClip clip;
        public GameSoundProfile profile;
        [Tooltip("Use this emitter's configured sound when no clip or profile is assigned.")]
        public bool useEmitterSound;
        [Range(0f, 2f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;

        public void Play(SoundEmitter emitter)
        {
            if (emitter == null) return;
            if (clip != null) emitter.PlayClip(clip, volume, pitch);
            else if (profile != null) emitter.Play(profile, volume, pitch);
            else if (useEmitterSound) emitter.PlayDefaultSound(volume, pitch, true);
        }
    }

    [Serializable]
    public class SoundEvent
    {
        public bool HasSound => sequence != null || clip != null || profile != null || useEmitterSound;
        [Tooltip("Sequence takes precedence over Clip and Profile. Clips use the emitter's EQ/effects; profiles own their effects.")]
        public SoundSequence sequence;
        public AudioClip clip;
        public GameSoundProfile profile;
        public bool useEmitterSound;
        [Range(0f, 2f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
    }

    [CreateAssetMenu(menuName = "Horror Audio/Sound Sequence")]
    public class SoundSequence : ScriptableObject
    {
        [Tooltip("Steps can be entered in any order. Delay is measured from sequence start.")]
        public SoundStep[] steps = Array.Empty<SoundStep>();
        public bool useUnscaledTime;
    }
}
