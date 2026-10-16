using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Optional spatial steering shared with a stationary train's exterior renderer.
public interface IStationaryTrainPath
{
    void SampleMotion(double distance, float speed, out float turnDegreesPerSecond, out float uphillDegrees);
}

[AddComponentMenu("Train/Stationary Train Simulation")]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class StationaryTrainSimulation : MonoBehaviour
{
    public enum MotionMode { RandomJourney, Timetable, Manual, VirtualRoute }
    [Serializable]
    public class JourneyStage
    {
        public string label = "Cruise";
        [Min(0.1f)] public float duration = 15f;
        [Min(0f)] public float speed = 8f;
        public float turnDegreesPerSecond;
        [Range(-30f, 30f)] public float uphillDegrees;
        [Min(0f)] public float roughness = 1f;
    }

    [Header("Stationary carriage / influence box")]
    [Tooltip("Your stationary train root. Its +Z points forward. Neither it nor this box is moved.")]
    public Transform trainFrame;
    public LayerMask affectedBodyLayers = ~0;
    public bool affectRigidbodies = true;
    [Min(0f)] public float objectForceMultiplier = 1f;
    [Min(0.1f)] public float maximumAcceleration = 20f;

    [Header("Journey")]
    public MotionMode mode = MotionMode.RandomJourney;
    public bool running = true;
    public bool emergencyBrake;
    [Min(0f)] public float cruiseSpeed = 8f;
    [Min(0.01f)] public float acceleration = 1.5f;
    [Min(0.01f)] public float braking = 2f;
    [Min(0.01f)] public float emergencyBraking = 6f;
    [Min(0.01f)] public float turnResponse = 2f;
    [Min(0.01f)] public float gradeResponse = 1f;

    [Header("Random journey (repeatable seed)")]
    public int randomSeed = 42;
    public Vector2 randomSpeedRange = new Vector2(5f, 9f);
    public Vector2 driveDurationRange = new Vector2(20f, 40f);
    public Vector2 stationDwellRange = new Vector2(5f, 10f);
    public Vector2 bendDurationRange = new Vector2(5f, 12f);
    [Min(0f)] public float maximumRandomTurn = 8f;
    [Range(0f, 30f)] public float maximumRandomGrade = 4f;

    [Header("Timetable (stage duration includes acceleration/braking)")]
    public bool repeatTimetable = true;
    public JourneyStage[] timetable = {
        new JourneyStage { label = "Depart", duration = 12f, speed = 8f },
        new JourneyStage { label = "Right bend", duration = 12f, speed = 8f, turnDegreesPerSecond = 6f },
        new JourneyStage { label = "Hill", duration = 10f, speed = 6f, uphillDegrees = 4f },
        new JourneyStage { label = "Station", duration = 12f, speed = 0f }
    };

    [Header("Optional virtual path (train stays stationary)")]
    public TrainRoute virtualRoute;
    [Min(0f)] public float routeStartDistance;

    [Header("Manual motion / trigger targets")]
    public float manualTurnDegreesPerSecond;
    [Range(-30f, 30f)] public float manualUphillDegrees;

    [Header("Heave, vibration and banking")]
    [Min(0f)] public float bounceAmplitude = 0.02f;
    [Min(0f)] public float bounceFrequency = 0.7f;
    [Min(0f)] public float vibrationAmplitude = 0.0006f;
    [Min(0f)] public float vibrationFrequency = 7f;
    [Min(0f)] public float roughnessMultiplier = 1f;
    [Range(-1f, 1f)] public float bankDegreesPerTurnRate = 0.15f;
    public bool includeRotationalForces = true;
    [Header("Reusable ride settings")]
    public TrainRideProfile rideProfile;
    public bool applyProfileOnStart = true;
    [Header("Gameplay events")]
    public UnityEvent onDeparted = new UnityEvent();
    public UnityEvent onStopped = new UnityEvent();

    public float Speed { get; private set; }
    public float TravelDistance { get; private set; }
    public double ContinuousTravelDistance { get; private set; }
    private IStationaryTrainPath motionPath;
    private bool motionPathChanged;
    public IStationaryTrainPath MotionPath
    {
        get => motionPath;
        set { if (ReferenceEquals(motionPath, value)) return; motionPath = value; motionPathChanged = true; }
    }
    public void RefreshMotionPath() => motionPathChanged = true;
    public float ElapsedTime { get; private set; }
    public float TurnRate { get; private set; }
    public float Grade { get; private set; }
    public float VerticalOffset { get; private set; }
    public string CurrentStage { get; private set; }
    public Vector3 CenterAcceleration => centerAcceleration;
    private BoxCollider volume;
    private System.Random random;
    private float driveTimer, bendTimer, dwellTimer, randomSpeed, randomTurn, randomGrade;
    private bool randomStopping;
    private int stageIndex;
    private float stageTimer;
    private float bank, previousPitchRate, previousYawRate, previousBankRate;
    private Vector3 centerAcceleration, gravityCorrection, angularVelocity, angularAcceleration;
    private Collider[] overlaps = new Collider[64];
    private readonly HashSet<Rigidbody> processed = new HashSet<Rigidbody>();
    private static readonly List<StationaryTrainSimulation> active = new List<StationaryTrainSimulation>();
    private Transform Frame => trainFrame != null ? trainFrame : transform;

    void Awake()
    {
        volume = GetComponent<BoxCollider>();
        volume.isTrigger = true;
        Rigidbody carriage = Frame.GetComponentInParent<Rigidbody>();
        if (carriage != null) carriage.isKinematic = true;
        if (applyProfileOnStart && rideProfile != null) rideProfile.ApplyTo(this);
        ResetJourney();
    }
    void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    void OnDisable() => active.Remove(this);

    [ContextMenu("Reset Simulated Journey")]
    public void ResetJourney()
    {
        random = new System.Random(randomSeed);
        Speed = TravelDistance = ElapsedTime = TurnRate = Grade = VerticalOffset = 0f;
        bank = previousPitchRate = previousYawRate = previousBankRate = 0f;
        motionPathChanged = motionPath != null;
        centerAcceleration = gravityCorrection = angularVelocity = angularAcceleration = Vector3.zero;
        stageIndex = 0;
        stageTimer = 0f;
        randomStopping = false;
        driveTimer = Range(driveDurationRange, 0.1f);
        bendTimer = 0f;
        dwellTimer = Range(stationDwellRange, 0.1f);
        randomSpeed = Range(randomSpeedRange, 0f);
        if (virtualRoute != null)
        {
            virtualRoute.Rebuild();
            TravelDistance = virtualRoute.loop && virtualRoute.IsValid
                ? Mathf.Repeat(routeStartDistance, virtualRoute.Length)
                : Mathf.Clamp(routeStartDistance, 0f, virtualRoute.Length);
        }
        ContinuousTravelDistance = TravelDistance;
        CurrentStage = "Ready";
    }

    float Range(Vector2 range, float minimum)
    {
        float low = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float high = Mathf.Max(low, Mathf.Max(range.x, range.y));
        return Mathf.Lerp(low, high, (float)random.NextDouble());
    }

    public void StartTrain() { running = true; emergencyBrake = false; }
    public void StopTrain() => running = false;
    public void EmergencyStop() => emergencyBrake = true;
    public void SetCruiseSpeed(float speed) => cruiseSpeed = Mathf.Max(0f, speed);
    public void SetManualTurn(float degreesPerSecond) => manualTurnDegreesPerSecond = degreesPerSecond;
    public void SetManualGrade(float degrees) => manualUphillDegrees = Mathf.Clamp(degrees, -30f, 30f);

    void FixedUpdate()
    {
        SimulateStep(Time.fixedDeltaTime);
        if (affectRigidbodies) ApplyToObjects();
    }

    // Public deterministic step is also useful for tests/custom simulation drivers.
    public void SimulateStep(float dt)
    {
        if (dt <= 0f) return;
        if (random == null) ResetJourney();
        ElapsedTime += dt;
        float targetSpeed = cruiseSpeed, targetTurn = manualTurnDegreesPerSecond;
        float targetGrade = manualUphillDegrees, roughness = 1f;
        CurrentStage = "Manual";
        bool advanceJourney = running && !emergencyBrake;
        if (mode == MotionMode.RandomJourney)
        {
            if (advanceJourney)
            {
                if (!randomStopping)
                {
                    driveTimer -= dt;
                    bendTimer -= dt;
                    if (bendTimer <= 0f)
                    {
                        randomTurn = ((float)random.NextDouble() * 2f - 1f) * maximumRandomTurn;
                        randomGrade = ((float)random.NextDouble() * 2f - 1f) * maximumRandomGrade;
                        bendTimer = Range(bendDurationRange, 0.1f);
                    }
                    if (driveTimer <= 0f) randomStopping = true;
                }
                else if (Speed <= 0.01f)
                {
                    dwellTimer -= dt;
                    if (dwellTimer <= 0f)
                    {
                        randomStopping = false;
                        driveTimer = Range(driveDurationRange, 0.1f);
                        dwellTimer = Range(stationDwellRange, 0.1f);
                        randomSpeed = Range(randomSpeedRange, 0f);
                    }
                }
            }
            targetSpeed = randomStopping ? 0f : randomSpeed;
            targetTurn = randomStopping ? 0f : randomTurn;
            targetGrade = randomGrade;
            CurrentStage = randomStopping ? "Station stop" : "Random travel";
        }
        else if (mode == MotionMode.Timetable)
        {
            if (timetable == null || timetable.Length == 0)
                targetSpeed = targetTurn = targetGrade = 0f;
            else
            {
                stageIndex = Mathf.Clamp(stageIndex, 0, timetable.Length - 1);
                JourneyStage stage = timetable[stageIndex];
                if (advanceJourney) stageTimer += dt;
                if (stageTimer >= Mathf.Max(0.1f, stage != null ? stage.duration : 0.1f))
                {
                    stageTimer = 0f;
                    if (stageIndex < timetable.Length - 1) stageIndex++;
                    else if (repeatTimetable) stageIndex = 0;
                    else running = false;
                    stage = timetable[stageIndex];
                }
                targetSpeed = stage != null ? stage.speed : 0f;
                targetTurn = stage != null ? stage.turnDegreesPerSecond : 0f;
                targetGrade = stage != null ? stage.uphillDegrees : 0f;
                roughness = stage != null ? stage.roughness : 0f;
                CurrentStage = stage != null ? stage.label : "Empty stage";
            }
        }
        else if (mode == MotionMode.VirtualRoute)
        {
            CurrentStage = "Virtual route";
            if (virtualRoute == null || !virtualRoute.IsValid) targetSpeed = targetTurn = targetGrade = 0f;
            else
            {
                virtualRoute.Evaluate(TravelDistance, out _, out Vector3 tangent);
                float sampleDistance = Mathf.Max(0.05f, Speed * dt);
                virtualRoute.Evaluate(TravelDistance + sampleDistance, out _, out Vector3 nextTangent);
                Vector3 flat = Vector3.ProjectOnPlane(tangent, Vector3.up);
                Vector3 nextFlat = Vector3.ProjectOnPlane(nextTangent, Vector3.up);
                targetTurn = Vector3.SignedAngle(flat, nextFlat, Vector3.up) / sampleDistance * Speed;
                targetGrade = Mathf.Asin(Mathf.Clamp(tangent.y, -1f, 1f)) * Mathf.Rad2Deg;
                if (!virtualRoute.loop)
                    targetSpeed = Mathf.Min(targetSpeed, Mathf.Sqrt(2f * Mathf.Max(0.01f, braking)
                        * Mathf.Max(0f, virtualRoute.Length - TravelDistance)));
            }
        }
        if (!running || emergencyBrake) targetSpeed = 0f;
        float oldSpeed = Speed, oldGrade = Grade, oldBank = bank;
        float rate = targetSpeed < Speed ? (emergencyBrake ? emergencyBraking : braking) : acceleration;
        Speed = Mathf.MoveTowards(Speed, Mathf.Max(0f, targetSpeed), Mathf.Max(0.01f, rate) * dt);
        float distanceStep = (Speed + oldSpeed) * 0.5f * dt;
        TravelDistance += distanceStep;
        ContinuousTravelDistance += distanceStep;
        if (mode == MotionMode.VirtualRoute && virtualRoute != null && virtualRoute.IsValid)
            TravelDistance = virtualRoute.loop ? Mathf.Repeat(TravelDistance, virtualRoute.Length)
                : Mathf.Min(TravelDistance, virtualRoute.Length);
        float speedFactor = Mathf.Clamp01(Speed / Mathf.Max(0.1f, cruiseSpeed));
        if (MotionPath != null)
        {
            // A shared path already provides spatially smooth steering. Filtering it again
            // would make inertia lag behind the visible rails, especially while braking.
            MotionPath.SampleMotion(ContinuousTravelDistance, Speed, out targetTurn, out targetGrade);
            TurnRate = targetTurn;
            Grade = targetGrade;
        }
        else
        {
            TurnRate = Mathf.Lerp(TurnRate, targetTurn * (mode == MotionMode.VirtualRoute ? 1f : speedFactor),
                1f - Mathf.Exp(-Mathf.Max(0.01f, turnResponse) * dt));
            Grade = Mathf.Lerp(Grade, targetGrade, 1f - Mathf.Exp(-Mathf.Max(0.01f, gradeResponse) * dt));
        }
        bank = TurnRate * bankDegreesPerTurnRate;
        if (motionPathChanged)
        {
            // Binding/rebuilding an exterior is an authoring change, not a physical jolt.
            oldGrade = Grade; oldBank = bank;
            previousPitchRate = previousBankRate = 0f;
            previousYawRate = TurnRate * Mathf.Deg2Rad;
            motionPathChanged = false;
        }
        float yawRate = TurnRate * Mathf.Deg2Rad;
        float pitchRate = -(Grade - oldGrade) * Mathf.Deg2Rad / dt;
        float bankRate = (bank - oldBank) * Mathf.Deg2Rad / dt;
        angularVelocity = new Vector3(pitchRate, yawRate, bankRate);
        angularAcceleration = new Vector3(pitchRate - previousPitchRate,
            yawRate - previousYawRate, bankRate - previousBankRate) / dt;
        previousPitchRate = pitchRate; previousYawRate = yawRate; previousBankRate = bankRate;
        float bounceOmega = bounceFrequency * Mathf.PI * 2f;
        float vibrationOmega = vibrationFrequency * Mathf.PI * 2f;
        float bounce = Mathf.Sin(ElapsedTime * bounceOmega) * bounceAmplitude;
        float vibration = Mathf.Sin(ElapsedTime * vibrationOmega) * vibrationAmplitude;
        float surface = Mathf.Max(0f, roughness * roughnessMultiplier) * speedFactor;
        VerticalOffset = (bounce + vibration) * surface;
        float verticalAcceleration = (-bounce * bounceOmega * bounceOmega
            - vibration * vibrationOmega * vibrationOmega) * surface - Speed * pitchRate;
        centerAcceleration = new Vector3(Speed * yawRate, verticalAcceleration, (Speed - oldSpeed) / dt);
        Quaternion attitude = Quaternion.Euler(-Grade, 0f, bank);
        Vector3 localGravity = Frame.InverseTransformDirection(Physics.gravity);
        gravityCorrection = Quaternion.Inverse(attitude) * localGravity - localGravity;
        if (oldSpeed <= 0.01f && Speed > 0.01f) onDeparted.Invoke();
        if (oldSpeed > 0.01f && Speed <= 0.01f) onStopped.Invoke();
    }

    public bool Contains(Vector3 worldPosition)
    {
        if (volume == null) volume = GetComponent<BoxCollider>();
        if (!isActiveAndEnabled || !volume.enabled) return false;
        Vector3 local = transform.InverseTransformPoint(worldPosition) - volume.center;
        Vector3 half = volume.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    public static StationaryTrainSimulation FindAt(Vector3 worldPosition)
    {
        foreach (StationaryTrainSimulation simulation in active)
            if (simulation != null && simulation.Contains(worldPosition)) return simulation;
        return null;
    }

    public Vector3 GetInertialAcceleration(Vector3 worldPosition, Vector3 relativeWorldVelocity)
    {
        Vector3 localForce = -centerAcceleration + gravityCorrection;
        if (includeRotationalForces)
        {
            Vector3 radius = Frame.InverseTransformDirection(worldPosition - Frame.position);
            Vector3 relativeVelocity = Frame.InverseTransformDirection(relativeWorldVelocity);
            localForce -= Vector3.Cross(angularAcceleration, radius)
                + Vector3.Cross(angularVelocity, Vector3.Cross(angularVelocity, radius))
                + 2f * Vector3.Cross(angularVelocity, relativeVelocity);
        }
        return Vector3.ClampMagnitude(Frame.TransformDirection(localForce), Mathf.Max(0.1f, maximumAcceleration));
    }

    public void ApplyToObjects()
    {
        if (volume == null) volume = GetComponent<BoxCollider>();
        Vector3 scale = transform.lossyScale;
        Vector3 half = Vector3.Scale(volume.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        Vector3 center = transform.TransformPoint(volume.center);
        int count;
        do
        {
            count = Physics.OverlapBoxNonAlloc(center, half, overlaps, transform.rotation,
                affectedBodyLayers, QueryTriggerInteraction.Ignore);
            if (count < overlaps.Length) break;
            Array.Resize(ref overlaps, overlaps.Length * 2);
        } while (true);
        processed.Clear();
        for (int i = 0; i < count; i++)
        {
            Rigidbody body = overlaps[i].attachedRigidbody;
            if (body == null || body.isKinematic || !body.useGravity || !Contains(body.worldCenterOfMass)
                || !processed.Add(body)) continue;
            // Overlapping boxes choose one simulation, so carriage volumes cannot double forces.
            StationaryTrainSimulation owner = FindAt(body.worldCenterOfMass);
            if (owner != null && owner != this) continue;
            Vector3 force = GetInertialAcceleration(body.worldCenterOfMass, body.linearVelocity) * objectForceMultiplier;
            if (force.sqrMagnitude > 0.0001f) body.AddForce(force, ForceMode.Acceleration);
        }
    }
}
