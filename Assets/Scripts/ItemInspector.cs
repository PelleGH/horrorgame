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
    public float inspectRange = 3f;
    public KeyCode inspectKey = KeyCode.E;
    public KeyCode cancelKey = KeyCode.Escape;
    public LayerMask inspectableMask;

    [Header("Resident Evil Inspect Feel")]
    [Tooltip("How long it takes for the item to move to/from inspect position.")]
    public float transitionDuration = 0.28f;

    [Tooltip("How strongly the item follows the camera position.")]
    public float positionSmoothTime = 0.035f;

    [Tooltip("Mouse button used to rotate the item. 0 = left, 1 = right.")]
    public int rotateMouseButton = 0;

    [Tooltip("Scroll wheel zoom speed.")]
    public float zoomSpeed = 0.35f;

    [Tooltip("Extra distance allowed when zooming out.")]
    public float extraZoomOutDistance = 0.75f;

    [Tooltip("Press this key to reset the inspected item's rotation.")]
    public KeyCode resetRotationKey = KeyCode.R;

    [Header("Obstruction")]
    public LayerMask obstructionMask;

    private InspectState state = InspectState.Idle;

    private InspectableItem currentItem;
    private Transform inspectedTransform;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private Transform originalParent;

    private Collider[] inspectedColliders;
    private Rigidbody inspectedRigidbody;
    private bool originalKinematicState;

    private Vector3 positionVelocity;
    private Coroutine transitionCoroutine;

    private float currentInspectDistance;
    private Quaternion startingInspectRotation;

    private void Update()
    {
        switch (state)
        {
            case InspectState.Idle:
                if (Input.GetKeyDown(inspectKey))
                    TryStartInspect();
                break;

            case InspectState.Inspecting:
                if (Input.GetKeyDown(inspectKey) || Input.GetKeyDown(cancelKey))
                {
                    BeginStopInspect();
                    return;
                }

                HandleZoom();
                HandleRotation();

                if (Input.GetKeyDown(resetRotationKey))
                    ResetInspectRotation();

                break;

            case InspectState.PickingUp:
            case InspectState.PuttingDown:
                break;
        }
    }

    private void LateUpdate()
    {
        if (state == InspectState.Inspecting && currentItem != null && inspectedTransform != null)
        {
            UpdateInspectPosition();
        }
    }

    private void TryStartInspect()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("ItemInspector: Player camera is missing.");
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        int mask = inspectableMask == 0 ? ~0 : inspectableMask;

        if (!Physics.Raycast(ray, out RaycastHit hit, inspectRange, mask, QueryTriggerInteraction.Ignore))
            return;

        InspectableItem item = hit.collider.GetComponentInParent<InspectableItem>();

        if (item == null)
            return;

        StartInspect(item);
    }

    private void StartInspect(InspectableItem item)
    {
        currentItem = item;
        inspectedTransform = item.transform;

        originalPosition = inspectedTransform.position;
        originalRotation = inspectedTransform.rotation;
        originalScale = inspectedTransform.localScale;
        originalParent = inspectedTransform.parent;

        inspectedColliders = inspectedTransform.GetComponentsInChildren<Collider>();

        foreach (Collider col in inspectedColliders)
        {
            if (col != null)
                col.enabled = false;
        }

        inspectedRigidbody = inspectedTransform.GetComponent<Rigidbody>();

        if (inspectedRigidbody != null)
        {
            originalKinematicState = inspectedRigidbody.isKinematic;
            inspectedRigidbody.isKinematic = true;
            inspectedRigidbody.angularVelocity = Vector3.zero;

#if UNITY_6000_0_OR_NEWER
            inspectedRigidbody.linearVelocity = Vector3.zero;
#else
            inspectedRigidbody.velocity = Vector3.zero;
#endif
        }

        inspectedTransform.SetParent(null, true);

        currentInspectDistance = currentItem.desiredDistance;

        startingInspectRotation =
            playerCamera.transform.rotation *
            Quaternion.Euler(currentItem.startingRotationOffset);

        Vector3 targetPosition = GetInspectPosition();
        Quaternion targetRotation = startingInspectRotation;
        Vector3 targetScale = currentItem.overrideScale ? currentItem.inspectScale : inspectedTransform.localScale;

        if (playerController != null)
            playerController.isInspecting = true;

        positionVelocity = Vector3.zero;
        state = InspectState.PickingUp;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(
            TransitionItem(
                inspectedTransform.position,
                targetPosition,
                inspectedTransform.rotation,
                targetRotation,
                inspectedTransform.localScale,
                targetScale,
                transitionDuration,
                InspectState.Inspecting
            )
        );
    }

    private void BeginStopInspect()
    {
        if (currentItem == null || inspectedTransform == null)
            return;

        state = InspectState.PuttingDown;

        if (playerController != null)
            playerController.isInspecting = false;

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
        if (inspectedTransform == null)
            return;

        inspectedTransform.position = originalPosition;
        inspectedTransform.rotation = originalRotation;
        inspectedTransform.localScale = originalScale;
        inspectedTransform.SetParent(originalParent, true);

        if (inspectedColliders != null)
        {
            foreach (Collider col in inspectedColliders)
            {
                if (col != null)
                    col.enabled = true;
            }
        }

        if (inspectedRigidbody != null)
            inspectedRigidbody.isKinematic = originalKinematicState;

        currentItem = null;
        inspectedTransform = null;
        inspectedColliders = null;
        inspectedRigidbody = null;
        originalParent = null;
        positionVelocity = Vector3.zero;
    }

    private void UpdateInspectPosition()
    {
        Vector3 targetPosition = GetInspectPosition();

        inspectedTransform.position = Vector3.SmoothDamp(
            inspectedTransform.position,
            targetPosition,
            ref positionVelocity,
            positionSmoothTime
        );
    }

    private Vector3 GetInspectPosition()
    {
        Vector3 camPos = playerCamera.transform.position;
        Vector3 forward = playerCamera.transform.forward;

        float targetDistance = GetObstructedDistance(currentInspectDistance);

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

    private void HandleRotation()
    {
        if (!Input.GetMouseButton(rotateMouseButton))
            return;

        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        inspectedTransform.Rotate(
            playerCamera.transform.up,
            -mouseX * currentItem.rotationSpeed * Time.deltaTime,
            Space.World
        );

        inspectedTransform.Rotate(
            playerCamera.transform.right,
            mouseY * currentItem.rotationSpeed * Time.deltaTime,
            Space.World
        );
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        float minDistance = currentItem.minDistance;
        float maxDistance = currentItem.desiredDistance + extraZoomOutDistance;

        currentInspectDistance -= scroll * zoomSpeed;
        currentInspectDistance = Mathf.Clamp(currentInspectDistance, minDistance, maxDistance);
    }

    private void ResetInspectRotation()
    {
        if (inspectedTransform == null)
            return;

        inspectedTransform.rotation = startingInspectRotation;
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
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            if (inspectedTransform == null)
                yield break;

            inspectedTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
            inspectedTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            inspectedTransform.localScale = Vector3.Lerp(startScale, targetScale, t);

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
        {
            if (playerController != null)
                playerController.isInspecting = false;

            FinishStopInspect();
            state = InspectState.Idle;
        }
    }
}