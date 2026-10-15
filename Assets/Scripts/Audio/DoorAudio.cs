using UnityEngine;

namespace HorrorAudio
{
    [DisallowMultipleComponent, RequireComponent(typeof(Door), typeof(SoundSequencePlayer))]
    [AddComponentMenu("Horror Audio/Door Audio")]
    public class DoorAudio : MonoBehaviour
    {
        public SoundSequencePlayer player;
        public SoundEvent opening = new SoundEvent();
        public SoundEvent closing = new SoundEvent();
        public SoundEvent latched = new SoundEvent();
        Door door;
        void Awake() { door = GetComponent<Door>(); if (player == null) player = GetComponent<SoundSequencePlayer>(); }
        void OnEnable()
        {
            if (door == null) door = GetComponent<Door>();
            door.onOpening.AddListener(Opening);
            door.onClosing.AddListener(Closing);
            door.onClosed.AddListener(Latched);
            door.onMotionStopped.AddListener(Stop);
        }
        void Opening() => player.PlayEvent(opening);
        void Closing() => player.PlayEvent(closing);
        void Latched() => player.PlayEvent(latched);
        void Stop() { if (player != null) player.Stop(); }
        void OnDisable()
        {
            if (door != null)
            {
                door.onOpening.RemoveListener(Opening); door.onClosing.RemoveListener(Closing);
                door.onClosed.RemoveListener(Latched); door.onMotionStopped.RemoveListener(Stop);
            }
            Stop();
        }
    }
}
