using UnityEngine;

namespace HorrorAudio
{
    [RequireComponent(typeof(SimpleFPSControllerNewInput), typeof(SoundEmitter))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        [Header("Footstep cadence")]
        [HideInInspector] public AudioSource footstepSource;
        [HideInInspector] public AudioClip[] footstepClips;
        public float walkStepInterval = 0.5f;
        public float sprintStepInterval = 0.35f;
        public float minMoveThreshold = 0.1f;
        public Vector2 footstepPitchRange = new Vector2(0.95f, 1.05f);
        public Vector2 footstepVolumeRange = new Vector2(0.9f, 1f);

        [Header("Crouch Footsteps")]
        public float crouchStepInterval = 0.7f;
        public Vector2 crouchVolumeRange = new Vector2(0.4f, 0.6f);
        [Header("Horror Sound Engine")]
        public SoundEmitter soundEmitter;
        public GameSoundProfile footstepProfile;
        [Tooltip("Keep the original walk/crouch volume and pitch ranges when playing imported footstep files.")]
        public bool useOriginalFootstepDynamics = true;
        public LayerMask footstepSurfaceMask = ~0;
        [Range(0f, 2f)] public float sprintFootstepGain = 1.2f;
        [Range(0f, 1f)] public float crouchFootstepGain = 0.45f;
        public Vector2 crouchPitchRange = new Vector2(0.85f, 0.95f);

        private SimpleFPSControllerNewInput movement;
        private CharacterController controller;
        private float stepTimer;
        private bool wasMovingLastFrame;
        private bool wasSprintingLastFrame;
        private readonly RaycastHit[] surfaceHits = new RaycastHit[16];

        void Awake()
        {
            movement = GetComponent<SimpleFPSControllerNewInput>();
            controller = GetComponent<CharacterController>();
            ConnectExistingFootsteps();
        }

        void LateUpdate()
        {
            bool active = movement != null && movement.isActiveAndEnabled && !movement.isInspecting && Time.timeScale > 0f;
            HandleFootsteps(active && movement.IsGrounded,
                active && movement.MovementInputMagnitude > minMoveThreshold,
                active && movement.IsSprinting, active && movement.IsCrouching);
        }

        void OnDisable()
        {
            stepTimer = 0f;
            wasMovingLastFrame = false;
            wasSprintingLastFrame = false;
        }

        public void ConnectExistingFootsteps()
        {
            if (footstepSource == null) footstepSource = GetComponent<AudioSource>();
            if (soundEmitter == null) soundEmitter = GetComponent<SoundEmitter>();
            if (soundEmitter == null) soundEmitter = gameObject.AddComponent<SoundEmitter>();
            if (footstepSource != null && soundEmitter.legacyTemplate == null) soundEmitter.legacyTemplate = footstepSource;
            if (!soundEmitter.HasDirectSound && footstepProfile == null && footstepClips != null &&
                System.Array.Exists(footstepClips, clip => clip != null))
            {
                soundEmitter.ImportSourceSettings(footstepSource, false, false);
                soundEmitter.clipVariations = (AudioClip[])footstepClips.Clone();
                soundEmitter.soundSettings.volume = 1f;
                soundEmitter.soundSettings.loop = false;
            }
            if (footstepSource != null)
            {
                footstepSource.Stop(); footstepSource.playOnAwake = false;
                footstepSource.enabled = false;
            }
        }

        void HandleFootsteps(bool grounded, bool isMoving, bool sprinting, bool crouching)
        {
            if (soundEmitter == null) return;

            if (!grounded || !isMoving)
            {
                stepTimer = 0f;
                wasMovingLastFrame = false;
                wasSprintingLastFrame = sprinting;
                return;
            }

            float currentInterval;
            float previousInterval;

            if (crouching)
            {
                currentInterval = crouchStepInterval;
                previousInterval = crouchStepInterval;
            }
            else
            {
                currentInterval = sprinting ? sprintStepInterval : walkStepInterval;
                previousInterval = wasSprintingLastFrame ? sprintStepInterval : walkStepInterval;
            }

            if (!wasMovingLastFrame)
            {
                PlayRandomFootstep(crouching, sprinting);
                stepTimer = currentInterval;
            }
            else
            {
                if (sprinting != wasSprintingLastFrame && previousInterval > 0f)
                {
                    float progress = 1f - (stepTimer / previousInterval);
                    progress = Mathf.Clamp01(progress);
                    stepTimer = currentInterval * (1f - progress);
                }

                stepTimer -= Time.deltaTime;

                if (stepTimer <= 0f)
                {
                    PlayRandomFootstep(crouching, sprinting);
                    stepTimer = currentInterval;
                }
            }

            wasMovingLastFrame = true;
            wasSprintingLastFrame = sprinting;
        }

        void PlayRandomFootstep(bool crouching, bool sprinting)
        {
            GameSoundProfile profile = footstepProfile;
            bool surfaceOverride = false;
            Vector3 origin = new Vector3(transform.position.x, controller.bounds.min.y + 0.15f, transform.position.z);
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, surfaceHits, 0.5f,
                footstepSurfaceMask, QueryTriggerInteraction.Ignore);
            Collider ground = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = surfaceHits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                ground = hit.collider;
                nearest = hit.distance;
            }
            if (ground != null)
            {
                var surface = ground.GetComponentInParent<SoundSurface>();
                if (surface != null && surface.footsteps != null) { profile = surface.footsteps; surfaceOverride = true; }
            }
            float gain = crouching ? crouchFootstepGain : sprinting ? sprintFootstepGain : 1f;
            if (soundEmitter != null && !surfaceOverride && soundEmitter.HasDirectSound)
            {
                float legacyGain = useOriginalFootstepDynamics ? Random.Range(crouching ? crouchVolumeRange.x : footstepVolumeRange.x,
                    crouching ? crouchVolumeRange.y : footstepVolumeRange.y) : gain;
                float legacyPitch = useOriginalFootstepDynamics ? Random.Range(crouching ? crouchPitchRange.x : footstepPitchRange.x,
                    crouching ? crouchPitchRange.y : footstepPitchRange.y) : 1f;
                soundEmitter.PlayDefaultSound(legacyGain, legacyPitch, forceOneShot: true);
                return;
            }
            if (soundEmitter != null && profile != null)
            {
                soundEmitter.Play(profile, gain);
                return;
            }
        }
    }
}
