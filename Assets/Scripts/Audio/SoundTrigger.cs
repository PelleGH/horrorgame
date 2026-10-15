using System.Collections.Generic;
using UnityEngine;

namespace HorrorAudio
{
    [DisallowMultipleComponent, RequireComponent(typeof(SoundSequencePlayer))]
    [AddComponentMenu("Horror Audio/Sound Trigger")]
    public class SoundTrigger : MonoBehaviour
    {
        public SoundSequencePlayer player;
        public SoundEvent entered = new SoundEvent();
        public SoundEvent exited = new SoundEvent();
        [Tooltip("Empty accepts any tag. Player accepts our FPS controller as well as the Player tag.")]
        public string requiredTag = "Player";
        public LayerMask layers = ~0;
        public bool playOnce;
        public bool stopOnExit;
        [Min(0f)] public float cooldown = 0.2f;
        readonly Dictionary<Collider, GameObject> occupants = new Dictionary<Collider, GameObject>();
        bool used;
        float nextPlay;
        void Awake() { if (player == null) player = GetComponent<SoundSequencePlayer>(); }
        public void Trigger()
        {
            if (!isActiveAndEnabled || (playOnce && used) || Time.time < nextPlay) return;
            if (player == null) player = GetComponent<SoundSequencePlayer>();
            player.PlayEvent(entered); used = true; nextPlay = Time.time + cooldown;
        }
        public void ResetTrigger() { used = false; nextPlay = 0f; }
        public void Stop() { if (player != null) player.Stop(); }
        void OnTriggerEnter(Collider other)
        {
            if ((layers.value & (1 << other.gameObject.layer)) == 0) return;
            var controller = other.GetComponentInParent<SimpleFPSControllerNewInput>();
            GameObject actor = controller != null ? controller.gameObject : other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!string.IsNullOrEmpty(requiredTag) && actor.tag != requiredTag && !(requiredTag == "Player" && controller != null)) return;
            bool alreadyInside = occupants.ContainsValue(actor);
            occupants[other] = actor;
            if (!alreadyInside) Trigger();
        }
        void OnTriggerExit(Collider other)
        {
            if (!occupants.TryGetValue(other, out GameObject actor)) return;
            occupants.Remove(other);
            if (occupants.ContainsValue(actor)) return;
            if (stopOnExit) Stop();
            if (player != null && exited != null && exited.HasSound) player.PlayEvent(exited);
        }
        void OnDisable() { occupants.Clear(); Stop(); }
    }
}
