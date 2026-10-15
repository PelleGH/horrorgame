using UnityEngine;

namespace HorrorAudio
{
    [AddComponentMenu("Horror Audio/Existing AudioSource Adapter")]
    public class LegacySoundSource : MonoBehaviour
    {
        public AudioSource originalSource;
        public SoundEmitter emitter;
        public bool startAutomatically;
        void Reset()
        {
            originalSource = GetComponent<AudioSource>();
            if (originalSource != null) startAutomatically = originalSource.playOnAwake;
        }
        void Awake()
        {
            if (originalSource == null) originalSource = GetComponent<AudioSource>();
            if (emitter == null)
            {
                GameObject root = new GameObject("Imported audio"); root.transform.SetParent(transform, false);
                emitter = root.AddComponent<SoundEmitter>();
                emitter.ImportSourceSettings(originalSource);
            }
            if (originalSource != null)
            { originalSource.Stop(); originalSource.playOnAwake = false; originalSource.enabled = false; }
        }
        void Start() { if (startAutomatically) Play(); }
        public void Play()
        {
            if (emitter != null) emitter.PlayDefault();
        }
        public void Stop() { if (emitter != null) emitter.StopAll(); }
        public void PlayOneShot(AudioClip clip) { if (emitter != null) emitter.PlayClip(clip); }
        void OnDisable() { Stop(); }
    }
}
