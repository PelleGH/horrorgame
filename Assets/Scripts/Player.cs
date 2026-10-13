using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSControllerNewInput : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Movement Weight")]
    [Min(0f)] public float acceleration = 12f;
    [Min(0f)] public float braking = 18f;
    [Min(0f)] public float directionChangeAcceleration = 20f;
    [Range(0f, 1f)] public float airControl = 0.2f;

    [Header("Hidden Sprint Stamina")]
    [Min(0.1f)] public float sprintDuration = 3f;
    [Min(0.1f)] public float staminaRecoveryDuration = 6f;
    [Min(0f)] public float staminaRecoveryDelay = 1f;
    [Range(0.1f, 1f)] public float exhaustedSpeedMultiplier = 0.55f;
    [Range(0.01f, 1f)] public float lowStaminaThreshold = 0.35f;
    [Min(0f)] public float fatigueShakeDegrees = 0.35f;
    [Min(0f)] public float fatigueShakeFrequency = 8f;

    [Header("Heavy Steps")]
    [Min(0f)] public float stepCameraDip = 0.035f;
    [Min(0f)] public float landingCameraDip = 0.08f;
    [Min(0.01f)] public float impactRecoveryTime = 0.12f;
    [Min(0f)] public float movementCameraLean = 1.2f;

    [Header("Crouch")]
    public float crouchSpeed = 2.5f;
    [Range(0.2f, 0.8f)]
    public float crouchHeightMultiplier = 0.4f;
    public LayerMask crouchBlockLayers = ~0;
    public float headCheckPadding = 0.02f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.1f;
    public Transform cameraTransform;
    public float maxLookAngle = 80f;

    [Header("Camera Crouch")]
    public float crouchCameraDrop = 0.5f;
    public float cameraCrouchSmooth = 12f;
    public float cameraHeightMultiplier = 0.85f;



    [Header("Footsteps")]
    public AudioSource footstepSource;
    public AudioClip[] footstepClips;
    public float walkStepInterval = 0.5f;
    public float sprintStepInterval = 0.35f;
    public float minMoveThreshold = 0.1f;
    public Vector2 footstepPitchRange = new Vector2(0.95f, 1.05f);
    public Vector2 footstepVolumeRange = new Vector2(0.9f, 1f);

    [Header("Crouch Footsteps")]
    public float crouchStepInterval = 0.7f;
    public Vector2 crouchVolumeRange = new Vector2(0.4f, 0.6f);
    public Vector2 crouchPitchRange = new Vector2(0.85f, 0.95f);

    private CharacterController controller;
    private MovingSurfacePassenger movingSurface;
    private StationaryTrainPassenger trainPassenger;
    private Vector3 velocity;
    private Vector3 horizontalVelocity;
    private Vector3 actualHorizontalVelocity;
    private float cameraBaseY;
    private float cameraImpact;
    private float impactVelocity;
    private float cameraRoll;
    private bool wasGrounded;
    private bool hasGroundSample;
    private float stamina = 1f;
    private float recoveryDelayRemaining;
    private bool exhausted;
    private float fatigueShakeStrength;
    private float fatigueShakeTime;
    private float pitch = 0f;

    private float stepTimer = 0f;
    private int lastFootstepIndex = -1;
    private bool wasMovingLastFrame = false;
    private float standingCameraLocalY;
    private float crouchingCameraLocalY;
    private float normalHeight;
    private float crouchHeight;
    private Vector3 normalCenter;
    private Vector3 crouchCenter;
    public bool isInspecting;
    private bool isCrouching;
    void Start()
    {
        controller = GetComponent<CharacterController>();
        movingSurface = GetComponent<MovingSurfacePassenger>();
        trainPassenger = GetComponent<StationaryTrainPassenger>();

        if (cameraTransform != null)
        {
            standingCameraLocalY = cameraTransform.localPosition.y;
            cameraBaseY = standingCameraLocalY;
        }

        normalHeight = controller.height;
        crouchHeight = normalHeight * crouchHeightMultiplier;
        normalCenter = controller.center;

        float bottom = normalCenter.y - (normalHeight * 0.5f);
        crouchCenter = new Vector3(
            normalCenter.x,
            bottom + (crouchHeight * 0.5f),
            normalCenter.z
        );
        if (cameraTransform != null)
        {
            crouchingCameraLocalY = crouchCenter.y + (crouchHeight * 0.5f * cameraHeightMultiplier);
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (footstepSource == null)
            footstepSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (movingSurface != null) movingSurface.BeginFrame();
        if (trainPassenger != null)
            trainPassenger.Step(Time.deltaTime, controller.isGrounded, isCrouching, horizontalVelocity);
        if (!isInspecting)
        {
            Look();
            Move();
        }
        else
        {
            horizontalVelocity = Vector3.zero;
            actualHorizontalVelocity = Vector3.zero;
            stepTimer = 0f;
            wasMovingLastFrame = false;
            UpdateStamina(false);
            if (trainPassenger != null) controller.Move(trainPassenger.ExternalVelocity * Time.deltaTime);
        }

        UpdateCameraHeight();
        if (movingSurface != null) movingSurface.EndFrame();
    }

    void Look()
    {
        Vector2 mouseDelta = Vector2.zero;
        if (Mouse.current != null)
        {
            mouseDelta = Mouse.current.delta.ReadValue();
        }

        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

        if (cameraTransform != null)
        {
            Vector3 euler = cameraTransform.localEulerAngles;
            euler.x = pitch;
            euler.y = 0f;
            euler.z = cameraRoll;
            cameraTransform.localEulerAngles = euler;
        }
    }

    void Move()
    {
        bool grounded = controller.isGrounded;
        if (hasGroundSample && grounded && !wasGrounded)
            cameraImpact = Mathf.Max(cameraImpact,
                landingCameraDip * Mathf.InverseLerp(2f, 12f, -velocity.y));
        wasGrounded = grounded;
        hasGroundSample = true;

        if (grounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        Vector2 moveInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;
        }

        Vector3 move = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;

        bool crouchHeld = Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;

        bool crouching = isCrouching;

        if (crouchHeld)
        {
            crouching = true;
        }
        else if (isCrouching)
        {
            // Only try to stand if we're currently crouched.
            if (CanStandUp())
                crouching = false;
            else
                crouching = true;
        }
        else
        {
            crouching = false;
        }

        SetCrouchState(crouching);

        bool sprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed
            && !crouching && !exhausted && stamina > 0f && move.sqrMagnitude > 0f;

        float walkingSpeed = exhausted ? moveSpeed * exhaustedSpeedMultiplier : moveSpeed;
        float currentSpeed = crouching ? Mathf.Min(crouchSpeed, walkingSpeed)
            : (sprinting ? sprintSpeed : walkingSpeed);

        Vector3 targetVelocity = move * currentSpeed;
        float response = move.sqrMagnitude < 0.001f ? braking : acceleration;
        if (move.sqrMagnitude > 0f && Vector3.Dot(horizontalVelocity, targetVelocity) < 0f)
            response = directionChangeAcceleration;
        if (!grounded) response *= airControl;

        // Momentum stays in world space when the player turns.
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity,
            Mathf.Max(0f, response) * Time.deltaTime);
        Vector3 beforeMove = transform.position;
        Vector3 inheritedVelocity = movingSurface != null ? movingSurface.AirVelocity : Vector3.zero;
        if (trainPassenger != null) inheritedVelocity += trainPassenger.ExternalVelocity;
        CollisionFlags collisions = controller.Move((horizontalVelocity + inheritedVelocity) * Time.deltaTime);
        Vector3 displacement = transform.position - beforeMove;
        actualHorizontalVelocity = Time.deltaTime > 0f
            ? new Vector3(displacement.x, 0f, displacement.z) / Time.deltaTime
            : Vector3.zero;
        if ((collisions & CollisionFlags.Sides) != 0)
        {
            Vector3 remainingInheritedVelocity = movingSurface != null ? movingSurface.AirVelocity : Vector3.zero;
            if (trainPassenger != null) remainingInheritedVelocity += trainPassenger.ExternalVelocity;
            horizontalVelocity = actualHorizontalVelocity
                - new Vector3(remainingInheritedVelocity.x, 0f, remainingInheritedVelocity.z);
        }

        UpdateStamina(sprinting && actualHorizontalVelocity.magnitude > minMoveThreshold);

        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (jumpPressed && grounded && !crouching)
        {
            if (movingSurface != null) movingSurface.Detach();
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        CollisionFlags verticalCollisions = controller.Move(velocity * Time.deltaTime);
        if ((verticalCollisions & CollisionFlags.Above) != 0 && velocity.y > 0f)
            velocity.y = 0f;

        bool isMoving = actualHorizontalVelocity.magnitude > minMoveThreshold;
        HandleFootsteps(controller.isGrounded, isMoving, sprinting, crouching);
    }

    public void ResetMotion()
    {
        velocity = Vector3.zero;
        horizontalVelocity = Vector3.zero;
        actualHorizontalVelocity = Vector3.zero;
        hasGroundSample = false;
        stepTimer = 0f;
        wasMovingLastFrame = false;
        if (movingSurface != null) movingSurface.ResetMotion();
        if (trainPassenger != null) trainPassenger.ResetMotion();
    }

    void UpdateStamina(bool spending)
    {
        if (spending)
        {
            stamina = Mathf.Max(0f, stamina - Time.deltaTime / Mathf.Max(0.1f, sprintDuration));
            recoveryDelayRemaining = Mathf.Max(0f, staminaRecoveryDelay);
            if (stamina <= 0f)
            {
                exhausted = true;
            }
        }
        else
        {
            // Recover only for the part of this frame after the delay expires.
            float recoveryTime = Mathf.Max(0f, Time.deltaTime - recoveryDelayRemaining);
            recoveryDelayRemaining = Mathf.Max(0f, recoveryDelayRemaining - Time.deltaTime);
            stamina = Mathf.Min(1f, stamina + recoveryTime / Mathf.Max(0.1f, staminaRecoveryDuration));
            // Latch exhaustion until fully recovered, even if Shift is held.
            if (stamina >= 1f)
                exhausted = false;
        }
    }

    void SetCrouchState(bool crouching)
    {
        if (isCrouching == crouching)
            return;

        isCrouching = crouching;
        controller.height = crouching ? crouchHeight : normalHeight;
        controller.center = crouching ? crouchCenter : normalCenter;
    }

    bool CanStandUp()
    {
        if (!isCrouching)
            return true;

        int mask = crouchBlockLayers & ~(1 << gameObject.layer);

        float radius = controller.radius + headCheckPadding;

        // Use the standing capsule we want to occupy.
        Vector3 worldCenter = transform.TransformPoint(normalCenter);

        float halfHeight = normalHeight * 0.5f;
        float sphereOffset = halfHeight - radius;

        Vector3 bottom = worldCenter + Vector3.down * sphereOffset;
        Vector3 top = worldCenter + Vector3.up * sphereOffset;

        return !Physics.CheckCapsule(
            bottom,
            top,
            radius,
            mask,
            QueryTriggerInteraction.Ignore
        );
    }

    void UpdateCameraHeight()
    {
        if (cameraTransform == null) return;

        Vector3 localPos = cameraTransform.localPosition;
        float targetY = isCrouching ? crouchingCameraLocalY : standingCameraLocalY;

        cameraBaseY = Mathf.Lerp(cameraBaseY, targetY,
            1f - Mathf.Exp(-Mathf.Max(0f, cameraCrouchSmooth) * Time.deltaTime));
        cameraImpact = Mathf.SmoothDamp(cameraImpact, 0f, ref impactVelocity,
            Mathf.Max(0.01f, impactRecoveryTime));
        localPos.y = cameraBaseY - cameraImpact;
        if (trainPassenger != null) localPos.y += trainPassenger.CameraHeightOffset;
        cameraTransform.localPosition = localPos;
        float sidewaysSpeed = Vector3.Dot(actualHorizontalVelocity, transform.right);
        float targetRoll = -sidewaysSpeed / Mathf.Max(0.01f, sprintSpeed) * movementCameraLean;
        cameraRoll = Mathf.Lerp(cameraRoll, targetRoll, 1f - Mathf.Exp(-8f * Time.deltaTime));
        Vector3 angles = cameraTransform.localEulerAngles;
        float fatigue = 1f - Mathf.Clamp01(stamina / Mathf.Max(0.01f, lowStaminaThreshold));
        float targetShake = isInspecting ? 0f : fatigue * fatigueShakeDegrees;
        fatigueShakeStrength = Mathf.Lerp(fatigueShakeStrength, targetShake,
            1f - Mathf.Exp(-6f * Time.deltaTime));
        fatigueShakeTime += Time.deltaTime * Mathf.Max(0f, fatigueShakeFrequency);
        // Apply offsets from the base look angles so shake never accumulates.
        float shakeX = (Mathf.PerlinNoise(fatigueShakeTime, 0.17f) - 0.5f) * 2f * fatigueShakeStrength;
        float shakeZ = (Mathf.PerlinNoise(0.73f, fatigueShakeTime) - 0.5f) * 2f * fatigueShakeStrength;
        angles.x = pitch + shakeX;
        angles.z = cameraRoll + shakeZ;
        if (trainPassenger != null) angles += trainPassenger.CameraAngles;
        cameraTransform.localEulerAngles = angles;
    }

    void HandleFootsteps(bool grounded, bool isMoving, bool sprinting, bool crouching)
    {
        if (!grounded || !isMoving)
        {
            stepTimer = 0f;
            wasMovingLastFrame = false;
            return;
        }

        float currentInterval;

        if (crouching)
        {
            currentInterval = crouchStepInterval;
        }
        else
        {
            currentInterval = sprinting ? sprintStepInterval : walkStepInterval;
        }

        if (!wasMovingLastFrame)
        {
            PlayRandomFootstep(crouching);
            stepTimer = 1f;
        }
        else
        {
            // Count step progress from distance traveled, including starts and stops.
            float referenceSpeed = crouching ? crouchSpeed : (sprinting ? sprintSpeed : moveSpeed);
            float stepDistance = Mathf.Max(0.05f, referenceSpeed * currentInterval);
            stepTimer -= actualHorizontalVelocity.magnitude * Time.deltaTime / stepDistance;

            if (stepTimer <= 0f)
            {
                PlayRandomFootstep(crouching);
                stepTimer = 1f;
            }
        }

        wasMovingLastFrame = true;
    }

    void PlayRandomFootstep(bool crouching)
    {
        float weight = crouching ? 0.45f : Mathf.Lerp(0.65f, 1.35f,
            Mathf.Clamp01(actualHorizontalVelocity.magnitude / Mathf.Max(0.01f, sprintSpeed)));
        cameraImpact = Mathf.Max(cameraImpact, stepCameraDip * weight);
        impactVelocity = 0f;
        if (footstepSource == null || footstepClips == null || footstepClips.Length == 0)
            return;

        int clipIndex = 0;

        if (footstepClips.Length == 1)
        {
            clipIndex = 0;
        }
        else
        {
            do
            {
                clipIndex = Random.Range(0, footstepClips.Length);
            }
            while (clipIndex == lastFootstepIndex);
        }

        lastFootstepIndex = clipIndex;

        if (crouching)
        {
            footstepSource.pitch = Random.Range(crouchPitchRange.x, crouchPitchRange.y);
            footstepSource.volume = Random.Range(crouchVolumeRange.x, crouchVolumeRange.y);
        }
        else
        {
            footstepSource.pitch = Random.Range(footstepPitchRange.x, footstepPitchRange.y);
            footstepSource.volume = Random.Range(footstepVolumeRange.x, footstepVolumeRange.y);
        }

        footstepSource.PlayOneShot(footstepClips[clipIndex]);
    }
}
