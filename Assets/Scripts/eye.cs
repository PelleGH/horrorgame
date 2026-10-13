using System.Collections;
using UnityEngine;

public class EyeLookAtPlayer3D : MonoBehaviour
{
    public enum LookMode
    {
        Always,
        WithinRadius
    }

    public enum LocalAxis
    {
        XPositive,
        XNegative,
        YPositive,
        YNegative,
        ZPositive,
        ZNegative
    }

    public enum BlinkMode
    {
        None,
        BlendShape
    }

    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Look Mode")]
    [SerializeField] private LookMode lookMode = LookMode.Always;
    [SerializeField] private float activationRadius = 5f;
    [SerializeField] private bool returnToRestWhenInactive = true;

    [Header("Eye Direction")]
    [Tooltip("Which local axis of the eye mesh points forward.")]
    [SerializeField] private LocalAxis localForwardAxis = LocalAxis.ZPositive;

    [Tooltip("Which local axis of the eye mesh points upward.")]
    [SerializeField] private LocalAxis localUpAxis = LocalAxis.YPositive;

    [Header("Rotation Limits")]
    [SerializeField] private bool rotateWholeEye = true;
    [SerializeField] private float maxYaw = 45f;
    [SerializeField] private float maxPitch = 30f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Pupil Movement")]
    [Tooltip("Optional. Use this if the pupil is a separate child object.")]
    [SerializeField] private Transform pupil;

    [SerializeField] private bool movePupil = false;
    [SerializeField] private float pupilMaxHorizontalOffset = 0.025f;
    [SerializeField] private float pupilMaxVerticalOffset = 0.015f;
    [SerializeField] private float pupilMoveSpeed = 12f;

    [Header("Blink")]
    [SerializeField] private BlinkMode blinkMode = BlinkMode.None;

    [Tooltip("SkinnedMeshRenderer that contains the blink blendshape.")]
    [SerializeField] private SkinnedMeshRenderer blinkRenderer;

    [Tooltip("Blendshape index for blinking. Usually 0 if the mesh only has one blendshape.")]
    [SerializeField] private int blinkBlendShapeIndex = 0;

    [SerializeField] private bool autoBlink = true;
    [SerializeField] private Vector2 blinkIntervalRange = new Vector2(2.5f, 6f);
    [SerializeField] private float blinkCloseTime = 0.06f;
    [SerializeField] private float blinkHoldTime = 0.03f;
    [SerializeField] private float blinkOpenTime = 0.08f;
    [SerializeField] private float blinkMaxWeight = 100f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    private Quaternion restLocalRotation;
    private Vector3 pupilRestLocalPosition;
    private Coroutine blinkCoroutine;

    private void Awake()
    {
        restLocalRotation = transform.localRotation;

        if (pupil != null)
            pupilRestLocalPosition = pupil.localPosition;
    }

    private void OnEnable()
    {
        if (autoBlink && blinkMode != BlinkMode.None)
            blinkCoroutine = StartCoroutine(AutoBlinkRoutine());
    }

    private void OnDisable()
    {
        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);
    }

    private void Update()
    {
        if (player == null)
            return;

        bool shouldLook = ShouldLookAtPlayer();

        if (shouldLook)
        {
            LookAtPlayer();
        }
        else if (returnToRestWhenInactive)
        {
            ReturnToRest();
        }
    }

    private void LookAtPlayer()
    {
        Quaternion restWorldRotation = GetRestWorldRotation();

        Vector3 directionToPlayer = player.position - transform.position;

        if (directionToPlayer.sqrMagnitude < 0.0001f)
            return;

        Vector3 localDirection = Quaternion.Inverse(restWorldRotation) * directionToPlayer.normalized;

        Vector3 forward = GetAxisVector(localForwardAxis);
        Vector3 up = GetAxisVector(localUpAxis);
        Vector3 right = Vector3.Cross(up, forward).normalized;

        float x = Vector3.Dot(localDirection, right);
        float y = Vector3.Dot(localDirection, up);
        float z = Vector3.Dot(localDirection, forward);

        float yaw = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
        float pitch = Mathf.Atan2(y, Mathf.Sqrt(x * x + z * z)) * Mathf.Rad2Deg;

        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        if (rotateWholeEye)
        {
            Quaternion yawRotation = Quaternion.AngleAxis(yaw, up);
            Quaternion pitchRotation = Quaternion.AngleAxis(-pitch, right);

            Quaternion targetWorldRotation = restWorldRotation * yawRotation * pitchRotation;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetWorldRotation,
                1f - Mathf.Exp(-rotationSpeed * Time.deltaTime)
            );
        }

        if (movePupil && pupil != null)
        {
            MovePupil(yaw, pitch);
        }
    }

    private void ReturnToRest()
    {
        Quaternion restWorldRotation = GetRestWorldRotation();

        if (rotateWholeEye)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                restWorldRotation,
                1f - Mathf.Exp(-rotationSpeed * Time.deltaTime)
            );
        }

        if (movePupil && pupil != null)
        {
            pupil.localPosition = Vector3.Lerp(
                pupil.localPosition,
                pupilRestLocalPosition,
                1f - Mathf.Exp(-pupilMoveSpeed * Time.deltaTime)
            );
        }
    }

    private void MovePupil(float yaw, float pitch)
    {
        float yawNormalized = Mathf.InverseLerp(-maxYaw, maxYaw, yaw) * 2f - 1f;
        float pitchNormalized = Mathf.InverseLerp(-maxPitch, maxPitch, pitch) * 2f - 1f;

        Vector3 targetLocalPosition =
            pupilRestLocalPosition +
            Vector3.right * yawNormalized * pupilMaxHorizontalOffset +
            Vector3.up * pitchNormalized * pupilMaxVerticalOffset;

        pupil.localPosition = Vector3.Lerp(
            pupil.localPosition,
            targetLocalPosition,
            1f - Mathf.Exp(-pupilMoveSpeed * Time.deltaTime)
        );
    }

    private bool ShouldLookAtPlayer()
    {
        if (lookMode == LookMode.Always)
            return true;

        float sqrDistance = (player.position - transform.position).sqrMagnitude;
        return sqrDistance <= activationRadius * activationRadius;
    }

    private Quaternion GetRestWorldRotation()
    {
        if (transform.parent == null)
            return restLocalRotation;

        return transform.parent.rotation * restLocalRotation;
    }

    private Vector3 GetAxisVector(LocalAxis axis)
    {
        switch (axis)
        {
            case LocalAxis.XPositive: return Vector3.right;
            case LocalAxis.XNegative: return Vector3.left;
            case LocalAxis.YPositive: return Vector3.up;
            case LocalAxis.YNegative: return Vector3.down;
            case LocalAxis.ZPositive: return Vector3.forward;
            case LocalAxis.ZNegative: return Vector3.back;
            default: return Vector3.forward;
        }
    }

    private IEnumerator AutoBlinkRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(blinkIntervalRange.x, blinkIntervalRange.y);
            yield return new WaitForSeconds(waitTime);

            yield return BlinkOnce();
        }
    }

    private IEnumerator BlinkOnce()
    {
        if (blinkMode != BlinkMode.BlendShape)
            yield break;

        if (blinkRenderer == null)
            yield break;

        if (blinkBlendShapeIndex < 0 || blinkBlendShapeIndex >= blinkRenderer.sharedMesh.blendShapeCount)
            yield break;

        yield return AnimateBlinkWeight(0f, blinkMaxWeight, blinkCloseTime);
        yield return new WaitForSeconds(blinkHoldTime);
        yield return AnimateBlinkWeight(blinkMaxWeight, 0f, blinkOpenTime);
    }

    private IEnumerator AnimateBlinkWeight(float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            float weight = Mathf.Lerp(from, to, t);
            blinkRenderer.SetBlendShapeWeight(blinkBlendShapeIndex, weight);

            elapsed += Time.deltaTime;
            yield return null;
        }

        blinkRenderer.SetBlendShapeWeight(blinkBlendShapeIndex, to);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug)
            return;

        if (lookMode == LookMode.WithinRadius)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, activationRadius);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 0.5f);
    }
}