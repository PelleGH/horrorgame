using UnityEngine;

[AddComponentMenu("Train/Stationary Train Passenger")]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class StationaryTrainPassenger : MonoBehaviour
{
    [Tooltip("Optional fixed simulation. Leave empty to detect the box containing the player.")]
    public StationaryTrainSimulation simulation;
    [Min(0f)] public float forceMultiplier = 1f;
    [Tooltip("Footing resistance in m/s². Forces weaker than this do not slide the grounded player.")]
    [Min(0f)] public float footingResistance = 3.5f;
    [Min(1f)] public float crouchGripMultiplier = 1.6f;
    [Min(0f)] public float slipDamping = 0.4f;
    [Min(0.1f)] public float maximumSlipSpeed = 5f;
    [Header("Subtle camera feedback")]
    [Min(0f)] public float leanDegreesPerAcceleration = 0.6f;
    [Min(0f)] public float maximumLeanDegrees = 3f;
    [Min(0f)] public float cameraResponse = 5f;
    [Range(0f, 1f)] public float cameraBounceMultiplier = 0.35f;
    public Vector3 ExternalVelocity { get; private set; }
    public Vector3 CameraAngles { get; private set; }
    public float CameraHeightOffset { get; private set; }
    public StationaryTrainSimulation CurrentSimulation { get; private set; }
    private CharacterController controller;

    void Awake() => controller = GetComponent<CharacterController>();

    public void Step(float dt, bool grounded, bool crouching, Vector3 walkingVelocity)
    {
        if (!isActiveAndEnabled)
        {
            ResetMotion();
            CurrentSimulation = null;
            return;
        }
        if (controller == null) controller = GetComponent<CharacterController>();
        Vector3 center = transform.TransformPoint(controller.center);
        CurrentSimulation = simulation != null && simulation.Contains(center)
            ? simulation : StationaryTrainSimulation.FindAt(center);
        Vector3 acceleration = CurrentSimulation != null
            ? CurrentSimulation.GetInertialAcceleration(center, walkingVelocity + ExternalVelocity) * forceMultiplier
            : Vector3.zero;
        Vector3 next = ExternalVelocity;
        if (grounded)
        {
            next.y = 0f;
            next += Vector3.ProjectOnPlane(acceleration, Vector3.up) * dt;
            next = Vector3.MoveTowards(next, Vector3.zero,
                footingResistance * (crouching ? crouchGripMultiplier : 1f) * dt);
            next *= Mathf.Exp(-Mathf.Max(0f, slipDamping) * dt);
        }
        else next += acceleration * dt;
        ExternalVelocity = Vector3.ClampMagnitude(next, Mathf.Max(0.1f, maximumSlipSpeed));
        Vector3 local = transform.InverseTransformDirection(acceleration);
        Vector3 targetAngles = new Vector3(
            Mathf.Clamp(-local.z * leanDegreesPerAcceleration, -maximumLeanDegrees, maximumLeanDegrees),
            0f, Mathf.Clamp(local.x * leanDegreesPerAcceleration, -maximumLeanDegrees, maximumLeanDegrees));
        float blend = 1f - Mathf.Exp(-Mathf.Max(0f, cameraResponse) * dt);
        CameraAngles = Vector3.Lerp(CameraAngles, targetAngles, blend);
        float bounce = CurrentSimulation != null ? -CurrentSimulation.VerticalOffset * cameraBounceMultiplier : 0f;
        CameraHeightOffset = Mathf.Lerp(CameraHeightOffset, bounce, blend);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (Vector3.Dot(ExternalVelocity, hit.normal) < 0f)
            ExternalVelocity = Vector3.ProjectOnPlane(ExternalVelocity, hit.normal);
    }

    public void ResetMotion()
    {
        ExternalVelocity = CameraAngles = Vector3.zero;
        CameraHeightOffset = 0f;
    }
}
