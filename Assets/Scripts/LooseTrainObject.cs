using UnityEngine;

[AddComponentMenu("Train/Loose Train Object")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class LooseTrainObject : MonoBehaviour
{
    [Header("Physics")]
    [Min(0.01f)] public float mass = 0.6f;
    [Min(0f)] public float linearDamping = 0.02f;
    [Min(0f)] public float angularDamping = 0.05f;
    public bool overrideCenterOfMass;
    public Vector3 centerOfMass = new Vector3(0f, -0.05f, 0f);
    public PhysicsMaterial frictionMaterial;
    [Header("Placement")]
    [Tooltip("Author props under the train; detach them at Play time for world-space physics.")]
    public bool detachFromTrainOnStart = true;
    public bool inheritTrainVelocityOnStart = true;

    void Awake() => ApplySettings();

    [ContextMenu("Apply Physics Settings")]
    public void ApplySettings()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.mass = Mathf.Max(0.01f, mass);
        body.linearDamping = Mathf.Max(0f, linearDamping);
        body.angularDamping = Mathf.Max(0f, angularDamping);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.solverIterations = 12;
        body.solverVelocityIterations = 6;
        if (overrideCenterOfMass) body.centerOfMass = centerOfMass;
        else body.ResetCenterOfMass();
        if (frictionMaterial != null)
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                if (collider.attachedRigidbody == body) collider.sharedMaterial = frictionMaterial;
    }

    void Start()
    {
        Rigidbody carrier = transform.parent != null ? transform.parent.GetComponentInParent<Rigidbody>() : null;
        if (carrier == null) return;
        Rigidbody body = GetComponent<Rigidbody>();
        if (inheritTrainVelocityOnStart) body.linearVelocity = carrier.GetPointVelocity(body.worldCenterOfMass);
        if (detachFromTrainOnStart) transform.SetParent(null, true);
    }
}
