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



    public bool IsGrounded { get; private set; }
    public float MovementInputMagnitude { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrouching => isCrouching;

    private CharacterController controller;
    private Vector3 velocity;
    private float pitch = 0f;

    private float standingCameraLocalY;
    private float crouchingCameraLocalY;
    private float normalHeight;
    private float crouchHeight;
    private Vector3 normalCenter;
    private Vector3 crouchCenter;
    public bool isInspecting;
    private bool isCrouching;

    /// <summary>Vertical look angle in degrees (positive = looking down).</summary>
    public float Pitch
    {
        get => pitch;
        set => pitch = Mathf.Clamp(value, -maxLookAngle, maxLookAngle);
    }
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

    }

    void Update()
    {
        if (!isInspecting)
        {
            Look();
            Move();
        }

        if (isInspecting) ResetMovementState();
        UpdateCameraHeight();
    }

    void OnDisable() => ResetMovementState();

    void ResetMovementState()
    {
        MovementInputMagnitude = 0f;
        IsSprinting = false;
        IsGrounded = false;
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

        IsGrounded = grounded;
        MovementInputMagnitude = move.magnitude;
        IsSprinting = sprinting;
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

}
