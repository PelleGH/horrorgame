using UnityEngine;

public class ItemInspector : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;

    [Header("Interaction")]
    public float inspectRange = 3f;
    public KeyCode inspectKey = KeyCode.E;
    public LayerMask inspectableMask;

    [Header("Obstruction")]
    public LayerMask obstructionMask;
    public float positionSmoothTime = 0.04f;

    [Header("Controls")]
    public int rotateMouseButton = 1; // Right mouse button

    public SimpleFPSControllerNewInput playerController;

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
    private bool isInspecting;

    void Update()
    {
        if (!isInspecting)
        {
            if (Input.GetKeyDown(inspectKey))
                TryStartInspect();
        }
        else
        {
            if (Input.GetKeyDown(inspectKey))
                StopInspect();

            RotateInspectedItem();
        }
    }

    void LateUpdate()
    {
        if (isInspecting && currentItem != null)
            UpdateInspectPosition();
    }

    void TryStartInspect()
    {
        Debug.DrawRay(
            playerCamera.transform.position,
            playerCamera.transform.forward * inspectRange,
            Color.red,
            1f
        );

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, inspectRange))
        {
            Debug.Log("Hit: " + hit.collider.name);

            InspectableItem item = hit.collider.GetComponentInParent<InspectableItem>();

            if (item != null)
            {
                Debug.Log("Inspectable item found: " + item.name);
                StartInspect(item);
            }
            else
            {
                Debug.Log("Hit object has no InspectableItem component.");
            }
        }
        else
        {
            Debug.Log("Raycast hit nothing.");
        }
    }

    void StartInspect(InspectableItem item)
    {
        currentItem = item;
        inspectedTransform = item.transform;
        inspectedColliders = inspectedTransform.GetComponentsInChildren<Collider>();
        playerController.isInspecting = true;
        foreach (Collider col in inspectedColliders)
        {
            col.enabled = false;
        }
        originalPosition = inspectedTransform.position;
        originalRotation = inspectedTransform.rotation;
        originalScale = inspectedTransform.localScale;
        originalParent = inspectedTransform.parent;

        inspectedTransform.SetParent(null, true);

        inspectedRigidbody = inspectedTransform.GetComponent<Rigidbody>();

        if (inspectedRigidbody != null)
        {
            originalKinematicState = inspectedRigidbody.isKinematic;
            inspectedRigidbody.isKinematic = true;
            inspectedRigidbody.linearVelocity = Vector3.zero; // use velocity if older Unity
            inspectedRigidbody.angularVelocity = Vector3.zero;
        }

        inspectedTransform.rotation =
            playerCamera.transform.rotation *
            Quaternion.Euler(currentItem.startingRotationOffset);

        if (currentItem.overrideScale)
            inspectedTransform.localScale = currentItem.inspectScale;

        positionVelocity = Vector3.zero;
        isInspecting = true;
    }

    void StopInspect()
    {
        if (currentItem == null)
            return;
        foreach (Collider col in inspectedColliders)
        {
            if (col != null)
                col.enabled = true;
        }
        playerController.isInspecting = false;
        inspectedColliders = null;
        inspectedTransform.position = originalPosition;
        inspectedTransform.rotation = originalRotation;
        inspectedTransform.localScale = originalScale;
        inspectedTransform.SetParent(originalParent, true);

        if (inspectedRigidbody != null)
            inspectedRigidbody.isKinematic = originalKinematicState;

        currentItem = null;
        inspectedTransform = null;
        inspectedRigidbody = null;

        isInspecting = false;
    }

    void UpdateInspectPosition()
    {
        Vector3 camPos = playerCamera.transform.position;
        Vector3 forward = playerCamera.transform.forward;

        float targetDistance = currentItem.desiredDistance;

        if (Physics.SphereCast(
            camPos,
            currentItem.collisionRadius,
            forward,
            out RaycastHit hit,
            currentItem.desiredDistance + currentItem.wallPadding,
            obstructionMask,
            QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Max(
                currentItem.minDistance,
                hit.distance - currentItem.wallPadding
            );
        }

        Vector3 targetPosition = camPos + forward * targetDistance;

        inspectedTransform.position = Vector3.SmoothDamp(
            inspectedTransform.position,
            targetPosition,
            ref positionVelocity,
            positionSmoothTime
        );
    }

    void RotateInspectedItem()
    {
        bool isRotatingItem = Input.GetMouseButton(0); // left mouse button

        playerController.isInspecting = isRotatingItem;

        if (!isRotatingItem)
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
}