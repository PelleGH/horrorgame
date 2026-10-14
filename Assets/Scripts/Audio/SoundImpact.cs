using UnityEngine;

namespace HorrorAudio
{
    [RequireComponent(typeof(SoundEmitter))]
    public class SoundImpact : MonoBehaviour
    {
        public SoundEmitter emitter;
        public GameSoundProfile impactProfile;
        public AudioClip impactClip;
        [Min(0f)] public float minimumSpeed = 0.5f;
        [Min(0.01f)] public float fullVolumeSpeed = 6f;
        [Min(0f)] public float cooldown = 0.1f;
        private float nextImpact;
        void Awake() { if (emitter == null) emitter = GetComponent<SoundEmitter>(); }
        void OnCollisionEnter(Collision collision)
        {
            if (Time.time < nextImpact) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minimumSpeed) return;
            SoundSurface surface = collision.collider.GetComponentInParent<SoundSurface>();
            GameSoundProfile profile = surface != null && surface.impacts != null ? surface.impacts : impactProfile;
            if (profile == null) profile = emitter.defaultProfile;
            float gain = Mathf.Clamp01(speed / fullVolumeSpeed);
            bool surfaceOverride = surface != null && surface.impacts != null;
            bool played = surfaceOverride ? emitter.Play(profile, gain)
                : impactClip != null ? emitter.PlayClip(impactClip, gain)
                : emitter.HasDirectSound ? emitter.PlayDefaultSound(gain, forceOneShot: true) : emitter.Play(profile, gain);
            if (played) nextImpact = Time.time + cooldown;
        }
    }
}
