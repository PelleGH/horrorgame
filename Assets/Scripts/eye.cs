using UnityEngine;

public class EyeLookAtPlayer2D : MonoBehaviour
{
    public enum LookMode
    {
        Always,
        WithinRadius
    }

    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Mode")]
    [SerializeField] private LookMode lookMode = LookMode.Always;
    [SerializeField] private float activationRadius = 5f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float rotationOffset = 0f;

    private Quaternion initialLocalRotation;
    private Quaternion initialWorldRotation;

    private void Awake()
    {
        initialLocalRotation = transform.localRotation;
        initialWorldRotation = transform.rotation;
    }

    private void Update()
    {
        if (player == null)
            return;

        if (!ShouldLookAtPlayer())
            return;

        Vector3 toPlayer = player.position - transform.position;

        if (toPlayer.sqrMagnitude < 0.0001f)
            return;

        // Convert the player direction into the eye's ORIGINAL local space
        Vector3 localDir = Quaternion.Inverse(initialWorldRotation) * toPlayer;

        // Find the angle in that local 2D plane
        float angle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;

        // Rotate ONLY around this object's local Z axis,
        // while preserving the original X/Y tilt
        Quaternion targetLocalRotation =
            initialLocalRotation * Quaternion.AngleAxis(angle + rotationOffset, Vector3.forward);

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetLocalRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private bool ShouldLookAtPlayer()
    {
        if (lookMode == LookMode.Always)
            return true;

        float sqrDistance = (player.position - transform.position).sqrMagnitude;
        return sqrDistance <= activationRadius * activationRadius;
    }

    private void OnDrawGizmosSelected()
    {
        if (lookMode != LookMode.WithinRadius)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }
}