
using System.Collections;
using UnityEngine;

public class ItemInspector : MonoBehaviour
{
    private enum InspectState
    {
        Idle,
        PickingUp,
        Inspecting,
        PuttingDown
    }

    [Header("References")]
    public Camera playerCamera;
    public SimpleFPSControllerNewInput playerController;

    [Header("Interaction")]
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("Inspect Feel")]
    public float transitionDuration = 0.28f;
    public float positionSmoothTime = 0.035f;
    public int rotateMouseButton = 0;
    public KeyCode resetRotationKey = KeyCode.R;

    [Header("Zoom")]
    public float zoomSpeed = 0.35f;
    public float extraZoomOutDistance = 0.75f;

    [Header("Obstruction")]
    public LayerMask obstructionMask;

    private InspectState state = InspectState.Idle;

    public bool IsBusy => state != InspectState.Idle;

    public bool IsInspecting =>
        state == InspectState.Inspecting;

    private InspectableItem currentItem;
    private Transform inspectedTransform;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private Transform originalParent;

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;

    private Collider[] inspectedColliders;
    private bool[] originalColliderEnabled;

    private Rigidbody inspectedRigidbody;
    private bool originalKinematicState;

    private Vector3 positionVelocity;
    private Coroutine transitionCoroutine;

    private float currentInspectDistance;
    private Vector3 visualCenterLocalOffset;

    private void Update()
    {
        if (state != InspectState.Inspecting)
            return;

        // E is handled by PlayerInteract.
        // Escape can still cancel here.
        if (Input.GetKeyDown(cancelKey))
        {
            StopInspect();
            return;
        }

        HandleZoom();
        HandleRotation();

        if (Input.GetKeyDown(resetRotationKey))
            ResetRotationButKeepCentered();
    }

    private void LateUpdate()
    {
        if (state == InspectState.Inspecting &&
            currentItem != null &&
            inspectedTransform != null)
        {
            UpdateInspectPosition();
        }
    }

    // Called by PlayerInteract through InspectableItem.
    public bool BeginInspect(InspectableItem item)
    {
        if (!isActiveAndEnabled ||
            state != InspectState.Idle ||
            item == null ||
            DialogueManager.IsBusy ||
            (playerController != null &&
             playerController.isInspecting))
        {
            return false;
        }

        if (playerCamera == null)
        {
            Debug.LogWarning(
                "ItemInspector: Missing playerCamera."
            );
            return false;
        }

        currentItem = item;
        inspectedTransform = item.transform;

        // Save original transform.
        originalPosition = inspectedTransform.position;
        originalRotation = inspectedTransform.rotation;
        originalScale = inspectedTransform.localScale;
        originalParent = inspectedTransform.parent;

        originalLocalPosition =
            inspectedTransform.localPosition;

        originalLocalRotation =
            inspectedTransform.localRotation;

        originalLocalScale =
            inspectedTransform.localScale;

        // Calculate the visual center before disabling colliders.
        Vector3 visualCenterWorld =
            GetVisualCenterWorld(inspectedTransform);

        visualCenterLocalOffset =
            inspectedTransform.InverseTransformPoint(
                visualCenterWorld
            );

        // Temporarily disable colliders.
        inspectedColliders =
            inspectedTransform.GetComponentsInChildren<Collider>();

        originalColliderEnabled =
            new bool[inspectedColliders.Length];

        for (int i = 0; i < inspectedColliders.Length; i++)
        {
            Collider col = inspectedColliders[i];

            if (col == null)
                continue;

            originalColliderEnabled[i] = col.enabled;
            col.enabled = false;
        }

        // Freeze rigidbody physics.
        inspectedRigidbody =
            inspectedTransform.GetComponent<Rigidbody>();

        if (inspectedRigidbody != null)
        {
            originalKinematicState =
                inspectedRigidbody.isKinematic;

            inspectedRigidbody.isKinematic = true;
            inspectedRigidbody.angularVelocity = Vector3.zero;

#if UNITY_6000_0_OR_NEWER
            inspectedRigidbody.linearVelocity = Vector3.zero;
#else
            inspectedRigidbody.velocity = Vector3.zero;
#endif
        }

        // Detach while inspecting.
        inspectedTransform.SetParent(null, true);

        currentInspectDistance = currentItem.desiredDistance;

        Vector3 targetCenterPosition =
            GetTargetCenterPosition();

        Quaternion targetRotation = originalRotation;

        Vector3 targetScale = currentItem.overrideScale
            ? currentItem.inspectScale
            : inspectedTransform.localScale;

        Vector3 targetPivotPosition =
            GetPivotPositionForCenteredObject(
                targetCenterPosition,
                targetRotation,
                targetScale
            );

        // Freeze player movement and camera look.
        if (playerController != null)
            playerController.isInspecting = true;

        positionVelocity = Vector3.zero;

        state = InspectState.PickingUp;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(
            TransitionItem(
                inspectedTransform.position,
                targetPivotPosition,
                inspectedTransform.rotation,
                targetRotation,
                inspectedTransform.localScale,
                targetScale,
                transitionDuration,
                InspectState.Inspecting
            )
        );

        return true;
    }

    // Called by PlayerInteract on E, or internally on Escape.
    public bool StopInspect()
    {
        if (state != InspectState.Inspecting)
            return false;

        BeginStopInspect();
        return true;
    }

    private void BeginStopInspect()
    {
        if (currentItem == null ||
            inspectedTransform == null)
            return;

        state = InspectState.PuttingDown;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(
            TransitionItem(
                inspectedTransform.position,
                originalPosition,
                inspectedTransform.rotation,
                originalRotation,
                inspectedTransform.localScale,
                originalScale,
                transitionDuration,
                InspectState.Idle,
                FinishStopInspect
            )
        );
    }

    private void FinishStopInspect()
    {
        if (inspectedTransform != null)
        {
            inspectedTransform.SetParent(originalParent, false);

            inspectedTransform.localPosition =
                originalLocalPosition;

            inspectedTransform.localRotation =
                originalLocalRotation;

            inspectedTransform.localScale =
                originalLocalScale;
        }

        // Restore colliders to their previous enabled states.
        if (inspectedColliders != null)
        {
            for (int i = 0; i < inspectedColliders.Length; i++)
            {
                Collider col = inspectedColliders[i];

                if (col != null &&
                    originalColliderEnabled != null)
                {
                    col.enabled = originalColliderEnabled[i];
                }
            }
        }

        if (inspectedRigidbody != null)
        {
            inspectedRigidbody.isKinematic =
                originalKinematicState;
        }

        if (playerController != null)
            playerController.isInspecting = false;

        currentItem = null;
        inspectedTransform = null;
        inspectedColliders = null;
        originalColliderEnabled = null;
        inspectedRigidbody = null;
        originalParent = null;

        positionVelocity = Vector3.zero;
        transitionCoroutine = null;
    }

    private void UpdateInspectPosition()
    {
        Vector3 targetCenterPosition =
            GetTargetCenterPosition();

        Vector3 targetPivotPosition =
            GetPivotPositionForCenteredObject(
                targetCenterPosition,
                inspectedTransform.rotation,
                inspectedTransform.localScale
            );

        inspectedTransform.position = Vector3.SmoothDamp(
            inspectedTransform.position,
            targetPivotPosition,
            ref positionVelocity,
            positionSmoothTime
        );
    }

    private Vector3 GetTargetCenterPosition()
    {
        Vector3 camPos = playerCamera.transform.position;
        Vector3 forward = playerCamera.transform.forward;

        float targetDistance =
            GetObstructedDistance(currentInspectDistance);

        return camPos + forward * targetDistance;
    }

    private float GetObstructedDistance(float desiredDistance)
    {
        if (currentItem == null)
            return desiredDistance;

        if (Physics.SphereCast(
            playerCamera.transform.position,
            currentItem.collisionRadius,
            playerCamera.transform.forward,
            out RaycastHit hit,
            desiredDistance + currentItem.wallPadding,
            obstructionMask,
            QueryTriggerInteraction.Ignore))
        {
            return Mathf.Max(
                currentItem.minDistance,
                hit.distance - currentItem.wallPadding
            );
        }

        return desiredDistance;
    }

    private Vector3 GetPivotPositionForCenteredObject(
        Vector3 targetCenterPosition,
        Quaternion rotation,
        Vector3 scale)
    {
        Vector3 scaledLocalOffset = Vector3.Scale(
            visualCenterLocalOffset,
            scale
        );

        Vector3 worldOffset = rotation * scaledLocalOffset;

        return targetCenterPosition - worldOffset;
    }

    private Vector3 GetVisualCenterWorld(Transform root)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.center;
        }

        Collider[] colliders =
            root.GetComponentsInChildren<Collider>();

        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;

            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }

            return bounds.center;
        }

        return root.position;
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        float minDistance = currentItem.minDistance;

        float maxDistance =
            currentItem.desiredDistance +
            extraZoomOutDistance;

        currentInspectDistance -= scroll * zoomSpeed;

        currentInspectDistance = Mathf.Clamp(
            currentInspectDistance,
            minDistance,
            maxDistance
        );
    }

    private void HandleRotation()
    {
        if (!Input.GetMouseButton(rotateMouseButton))
            return;

        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        Vector3 center =
            inspectedTransform.TransformPoint(
                visualCenterLocalOffset
            );

        inspectedTransform.RotateAround(
            center,
            playerCamera.transform.up,
            -mouseX * currentItem.rotationSpeed * Time.deltaTime
        );

        inspectedTransform.RotateAround(
            center,
            playerCamera.transform.right,
            mouseY * currentItem.rotationSpeed * Time.deltaTime
        );
    }

    private void ResetRotationButKeepCentered()
    {
        if (inspectedTransform == null)
            return;

        Vector3 centerBeforeReset =
            inspectedTransform.TransformPoint(
                visualCenterLocalOffset
            );

        inspectedTransform.rotation = originalRotation;

        Vector3 centerAfterReset =
            inspectedTransform.TransformPoint(
                visualCenterLocalOffset
            );

        inspectedTransform.position +=
            centerBeforeReset - centerAfterReset;
    }

    private IEnumerator TransitionItem(
        Vector3 startPosition,
        Vector3 targetPosition,
        Quaternion startRotation,
        Quaternion targetRotation,
        Vector3 startScale,
        Vector3 targetScale,
        float duration,
        InspectState nextState,
        System.Action onComplete = null)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (inspectedTransform == null)
                yield break;

            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            inspectedTransform.position =
                Vector3.Lerp(startPosition, targetPosition, t);

            inspectedTransform.rotation =
                Quaternion.Slerp(startRotation, targetRotation, t);

            inspectedTransform.localScale =
                Vector3.Lerp(startScale, targetScale, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (inspectedTransform != null)
        {
            inspectedTransform.position = targetPosition;
            inspectedTransform.rotation = targetRotation;
            inspectedTransform.localScale = targetScale;
        }

        onComplete?.Invoke();

        state = nextState;
        transitionCoroutine = null;
    }

    private void OnDisable()
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        if (currentItem != null)
            FinishStopInspect();

        state = InspectState.Idle;
    }
}
