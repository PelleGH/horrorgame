using UnityEngine;

// Optional extension for CharacterController players. Never parents the player.
[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("Train/Moving Surface Passenger")]
[DisallowMultipleComponent]
public class MovingSurfacePassenger : MonoBehaviour
{
    [Tooltip("Layers containing rideable floor colliders. Exclude the player's own layer.")]
    public LayerMask surfaceLayers = ~0;
    public bool followSurfaceYaw = true;
    [Range(0f, 2f)] public float inheritedMomentumMultiplier = 1f;
    [Min(0.01f)] public float groundProbeDistance = 0.25f;
    private CharacterController controller;
    private Rigidbody surface;
    private Vector3 localAnchor;
    private Vector3 worldAnchor;
    private Quaternion surfaceRotation;
    private Vector3 surfaceVelocity;
    public Vector3 AirVelocity { get; private set; }
    public bool RidingSurface => surface != null;

    void Awake() => controller = GetComponent<CharacterController>();

    public void BeginFrame()
    {
        if (surface == null) return;
        if (!controller.isGrounded)
        {
            Detach();
            return;
        }
        Vector3 nextAnchor = surface.transform.TransformPoint(localAnchor);
        Vector3 delta = nextAnchor - worldAnchor;
        // Physics velocity avoids spikes from render interpolation or multiple physics ticks.
        surfaceVelocity = surface.GetPointVelocity(transform.position);
        Quaternion rotationDelta = surface.transform.rotation * Quaternion.Inverse(surfaceRotation);
        float yaw = Mathf.DeltaAngle(0f, rotationDelta.eulerAngles.y);
        if (followSurfaceYaw) transform.Rotate(0f, yaw, 0f, Space.World);
        // Keep the ground contact flag valid for the player's subsequent movement/jump.
        controller.Move(delta + Vector3.down * 0.02f);
        worldAnchor = nextAnchor;
        surfaceRotation = surface.transform.rotation;
    }

    public void Detach()
    {
        if (surface == null) return;
        AirVelocity = surfaceVelocity * inheritedMomentumMultiplier;
        surface = null;
    }

    public void EndFrame()
    {
        if (!controller.isGrounded)
        {
            Detach();
            return;
        }
        AirVelocity = Vector3.zero;
        Vector3 center = transform.TransformPoint(controller.center);
        float radius = controller.radius * 0.85f;
        Vector3 origin = center - Vector3.up * (controller.height * 0.5f - controller.radius - 0.05f);
        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit,
            Mathf.Max(0.01f, groundProbeDistance) + controller.skinWidth, surfaceLayers, QueryTriggerInteraction.Ignore)
            && hit.normal.y > 0.5f && hit.rigidbody != null)
        {
            if (surface != hit.rigidbody)
                surfaceVelocity = hit.rigidbody.GetPointVelocity(transform.position);
            surface = hit.rigidbody;
            localAnchor = surface.transform.InverseTransformPoint(transform.position);
            worldAnchor = surface.transform.TransformPoint(localAnchor);
            surfaceRotation = surface.transform.rotation;
        }
        else
        {
            surface = null;
            surfaceVelocity = Vector3.zero;
        }
    }

    public void ResetMotion()
    {
        surface = null;
        surfaceVelocity = Vector3.zero;
        AirVelocity = Vector3.zero;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (AirVelocity.sqrMagnitude > 0f && Vector3.Dot(AirVelocity, hit.normal) < 0f)
            AirVelocity = Vector3.ProjectOnPlane(AirVelocity, hit.normal);
    }
}
