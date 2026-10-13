using UnityEngine;

[AddComponentMenu("Train/Train Path Follower")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class TrainPathFollower : MonoBehaviour
{
    [Header("Route")]
    public TrainRoute route;
    [Tooltip("Starting distance in metres along the route.")]
    [Min(0f)] public float startDistance;
    [Tooltip("Offset in the route's local right/up/forward directions. Route points mark the root pivot.")]
    public Vector3 routeOffset;
    public bool followSlopes = true;

    [Header("Motion (metres and seconds)")]
    [Min(0f)] public float cruiseSpeed = 8f;
    [Min(0.01f)] public float acceleration = 1.5f;
    [Min(0.01f)] public float deceleration = 2f;
    [Min(0.01f)] public float emergencyDeceleration = 6f;
    public bool running = true;
    public bool emergencyBrake;
    public float Speed { get; private set; }
    public float Distance { get; private set; }
    private Rigidbody body;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        ResetToRouteStart();
    }

    [ContextMenu("Reset To Route Start")]
    public void ResetToRouteStart()
    {
        if (route == null) return;
        route.Rebuild();
        if (!route.IsValid)
        {
            Debug.LogWarning("Train route needs distinct, assigned waypoints (2 open / 3 loop).", this);
            return;
        }
        if (body == null) body = GetComponent<Rigidbody>();
        Distance = route.loop ? Mathf.Repeat(startDistance, route.Length) : Mathf.Clamp(startDistance, 0f, route.Length);
        Speed = 0f;
        GetPose(Distance, out Vector3 position, out Quaternion rotation);
        transform.SetPositionAndRotation(position, rotation);
        body.position = position;
        body.rotation = rotation;
    }

    public void StartTrain() { running = true; emergencyBrake = false; }
    public void StopTrain() => running = false;
    public void EmergencyStop() => emergencyBrake = true;
    public void ReleaseBrake() => emergencyBrake = false;
    public void SetCruiseSpeed(float speed) => cruiseSpeed = Mathf.Max(0f, speed);

    void FixedUpdate()
    {
        if (route == null || !route.IsValid) return;
        float braking = Mathf.Max(0.01f, emergencyBrake ? emergencyDeceleration : deceleration);
        float target = running && !emergencyBrake ? Mathf.Max(0f, cruiseSpeed) : 0f;
        if (!route.loop)
        {
            float remaining = Mathf.Max(0f, route.Length - Distance);
            // Reduce speed early enough to stop at an open route's final point.
            target = Mathf.Min(target, Mathf.Sqrt(2f * braking * remaining));
        }
        float oldSpeed = Speed;
        Speed = Mathf.MoveTowards(Speed, target,
            (target < Speed ? braking : Mathf.Max(0.01f, acceleration)) * Time.fixedDeltaTime);
        float step = (oldSpeed + Speed) * 0.5f * Time.fixedDeltaTime;
        Distance = route.loop ? Mathf.Repeat(Distance + step, route.Length)
            : Mathf.Min(Distance + step, route.Length);
        if (!route.loop && Distance >= route.Length) Speed = 0f;
        GetPose(Distance, out Vector3 position, out Quaternion rotation);
        body.MovePosition(position);
        body.MoveRotation(rotation);
    }

    void GetPose(float distance, out Vector3 position, out Quaternion rotation)
    {
        route.Evaluate(distance, out position, out Vector3 forward);
        if (!followSlopes) forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
        rotation = Quaternion.LookRotation(forward, Vector3.up);
        position += rotation * routeOffset;
    }

    void OnDrawGizmosSelected()
    {
        if (route == null) return;
        if (!Application.isPlaying) route.Rebuild();
        if (!route.IsValid) return;
        GetPose(Application.isPlaying ? Distance : startDistance, out Vector3 position, out Quaternion rotation);
        Gizmos.color = Color.yellow;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(2f, 1f, 4f));
        Gizmos.matrix = previous;
        Gizmos.DrawRay(position, rotation * Vector3.forward * 3f);
    }
}
