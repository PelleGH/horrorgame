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
    private Vector3 velocity;
    private float pitch = 0f;

    private float stepTimer = 0f;
    private int lastFootstepIndex = -1;
    private bool wasMovingLastFrame = false;
    private bool wasSprintingLastFrame = false;
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

        if (cameraTransform != null)
        {
            standingCameraLocalY = cameraTransform.localPosition.y;
            crouchingCameraLocalY = crouchCenter.y + (crouchHeight * cameraHeightMultiplier * 0.5f);
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
        if (!isInspecting)
        {
            Look();
            Move();
        }

        UpdateCameraHeight();
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
            euler.z = 0f;
            cameraTransform.localEulerAngles = euler;
        }
    }

    void Move()
    {
        bool grounded = controller.isGrounded;

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

        bool sprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && !crouching;

        float currentSpeed = crouching ? crouchSpeed : (sprinting ? sprintSpeed : moveSpeed);

        controller.Move(move * currentSpeed * Time.deltaTime);

        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (jumpPressed && grounded && !crouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        bool isMoving = move.magnitude > minMoveThreshold;
        HandleFootsteps(grounded, isMoving, sprinting, crouching);
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

        localPos.y = Mathf.Lerp(localPos.y, targetY, Time.deltaTime * cameraCrouchSmooth);
        cameraTransform.localPosition = localPos;
    }

    void HandleFootsteps(bool grounded, bool isMoving, bool sprinting, bool crouching)
    {
        if (footstepSource == null || footstepClips == null || footstepClips.Length == 0)
            return;

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
            PlayRandomFootstep(crouching);
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
                PlayRandomFootstep(crouching);
                stepTimer = currentInterval;
            }
        }

        wasMovingLastFrame = true;
        wasSprintingLastFrame = sprinting;
    }

    void PlayRandomFootstep(bool crouching)
    {
        if (footstepClips.Length == 0)
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