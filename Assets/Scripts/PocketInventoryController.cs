using UnityEngine;
using UnityEngine.InputSystem;

// A physical pocket opening in the ordinary first-person view. No camera switch.
[DisallowMultipleComponent]
public class PocketInventoryController : MonoBehaviour
{
    public SimpleFPSControllerNewInput player;
    public Camera worldCamera;
    public Transform handAnchor;
    public PocketEntrance pocket;
    [Header("Look-down pocket")]
    [Range(15f, 80f)] public float lookDownAngle = 50f;
    public bool createDefaultInterior = true;
    [Range(8, 31)] public int pocketLayer = 30;
    [Tooltip("Pocket position relative to the player's feet and body facing.")]
    public Vector3 waistOffset = new Vector3(0f, 1.28f, 0f);
    [Tooltip("Pocket position around the body: 0 front, 90 right, 135 back-right, -90 left.")]
    [Range(-180f, 180f)] public float pocketSideAngle = 135f;
    [Tooltip("Horizontal distance from the body toward Pocket Side Angle.")]
    [Range(0f, 0.4f)] public float pocketSideDistance = 0.18f;
    public bool followCrouch = true;
    [Tooltip("Distance below the camera when following crouch. Smaller values bring the pocket closer.")]
    [Range(0.2f, 0.8f)] public float cameraPocketDistance = 0.32f;
    [Range(0.06f, 0.3f)] public float pocketScale = 0.14f;
    [Min(0.1f)] public float revealSpeed = 8f;
    [Header("Interior")]
    [Min(0f)] public float centerLightIntensity = 2.4f;
    [Range(0f, 1f)] public float liningBrightness = 0.025f;
    [Min(0.1f)] public float itemReach = 1.5f;
    public bool showHelp = true;
    [Header("Lean into pocket")]
    [Range(0.05f, 0.5f)] public float scrollSensitivity = 0.18f;
    [Min(1f)] public float cameraSmoothness = 9f;
    [Range(0.0005f, 0.01f)] public float panSensitivity = 0.003f;
    [Range(0.1f, 0.7f)] public float panLimit = 0.45f;
    [Range(0.6f, 1.8f)] public float zoomViewDistance = 1.1f;
    public bool IsCameraLocked => cameraCaptured;
    [Header("Throw ball")]
    [Min(0f)] public float throwSpeed = 10f;
    [Min(0f)] public float throwLift = 1.5f;
    public bool WorldInteractionBusy { get; set; }
    public bool IsPocketVisible { get; private set; }
    public bool CanTogglePocket => initialized && pocket != null && isActiveAndEnabled
        && worldCamera.enabled && !player.isInspecting && !WorldInteractionBusy
        && (cameraCaptured || DownAngle >= lookDownAngle);
    private float DownAngle => Mathf.Asin(Mathf.Clamp(-worldCamera.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
    public bool IsPointerOverPocket => targetedItem != null;
    public PocketInventoryItem HeldItem { get; private set; }
    private PocketInventoryItem targetedItem;
    private PocketInventoryRoom generatedRoom;
    private float bodyYaw, reveal;
    private int originalWorldMask;
    private float originalNearClip;
    private bool initialized, createdHand, opened;
    private bool cameraCaptured;
    private float zoom, targetZoom;
    private Vector2 pan, targetPan;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private CursorLockMode savedCursorLock;
    private bool savedCursorVisible;

    void Start() => Initialize();

    public void Initialize()
    {
        if (initialized) return;
        if (player == null) player = GetComponent<SimpleFPSControllerNewInput>();
        if (worldCamera == null && player != null && player.cameraTransform != null)
            worldCamera = player.cameraTransform.GetComponent<Camera>();
        if (worldCamera == null) worldCamera = GetComponentInChildren<Camera>();
        if (player == null || worldCamera == null) return;
        originalWorldMask = worldCamera.cullingMask;
        originalNearClip = worldCamera.nearClipPlane;
        worldCamera.nearClipPlane = Mathf.Min(originalNearClip, 0.025f);
        worldCamera.cullingMask |= 1 << pocketLayer;
        bodyYaw = transform.eulerAngles.y;
        if (pocket == null && createDefaultInterior) generatedRoom = PocketInventoryRoom.Create(this);
        if (handAnchor == null)
        {
            handAnchor = new GameObject("Pocket held item display").transform;
            createdHand = true;
            handAnchor.SetParent(worldCamera.transform, false);
            handAnchor.localPosition = new Vector3(0.22f, -0.2f, 0.55f);
        }
        initialized = true;
    }

    void LateUpdate()
    {
        if (!initialized) Initialize();
        if (!initialized || pocket == null) return;
        float downAngle = DownAngle;
        bool available = isActiveAndEnabled && worldCamera.enabled && !player.isInspecting && !WorldInteractionBusy;
        if (!available || (!cameraCaptured && downAngle < lookDownAngle - 10f)) opened = false;
        if (CanTogglePocket && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            opened = !opened;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) opened = false;
        if (!opened && cameraCaptured) targetZoom = 0f;
        float targetReveal = available && (opened || cameraCaptured) ?
            (cameraCaptured ? 1f : Mathf.InverseLerp(lookDownAngle - 10f, lookDownAngle + 5f, downAngle)) : 0f;
        reveal = Mathf.Lerp(reveal, targetReveal, 1f - Mathf.Exp(-revealSpeed * Time.deltaTime));
        IsPocketVisible = available && opened && (cameraCaptured || downAngle >= lookDownAngle) && reveal > 0.1f;
        // Keep the waist facing still during downward free look so side items can be aimed at.
        if (downAngle < lookDownAngle - 10f)
            bodyYaw = Mathf.LerpAngle(bodyYaw, transform.eulerAngles.y, 1f - Mathf.Exp(-6f * Time.deltaTime));
        if (generatedRoom != null && !cameraCaptured)
        {
            Quaternion facing = Quaternion.Euler(0f, bodyYaw, 0f);
            Quaternion pocketFacing = facing * Quaternion.Euler(0f, pocketSideAngle, 0f);
            Vector3 offset = waistOffset;
            if (followCrouch)
                offset.y = Mathf.Max(0.1f,
                    worldCamera.transform.position.y - transform.position.y - cameraPocketDistance);
            Vector3 position = transform.position + facing * offset
                + pocketFacing * Vector3.forward * pocketSideDistance;
            // Stay underneath the eyes even when the camera has a local horizontal offset.
            position.x += worldCamera.transform.position.x - transform.position.x;
            position.z += worldCamera.transform.position.z - transform.position.z;
            generatedRoom.transform.SetPositionAndRotation(position,
                pocketFacing * Quaternion.Euler(90f, 0f, 0f));
            generatedRoom.transform.localScale = Vector3.one * pocketScale * Mathf.Max(0.001f, reveal);
            generatedRoom.gameObject.SetActive(reveal > 0.01f);
            generatedRoom.UpdateLighting(this);
            if (IsPocketVisible) Physics.SyncTransforms();
        }
        UpdatePocketCamera(available);
        FindItem();
        if (IsPocketVisible && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && targetedItem != null)
            SelectItem(targetedItem);
        if (available && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) ReturnHeldItem();
        if (available && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            ThrowHeldBall();
    }

    void UpdatePocketCamera(bool available)
    {
        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Mathf.Approximately(scroll, 0f)) scroll = Input.mouseScrollDelta.y;
#endif
        // Some mice/backends report wheel notches as 1, others as 120.
        float notches = Mathf.Abs(scroll) >= 20f ? scroll / 120f : scroll;
        if (available && opened && (cameraCaptured || DownAngle >= lookDownAngle) && notches != 0f)
        {
            targetZoom = Mathf.Clamp01(targetZoom + notches * scrollSensitivity);
            if (!cameraCaptured && targetZoom > 0f)
            {
                savedCameraPosition = worldCamera.transform.localPosition;
                savedCameraRotation = worldCamera.transform.localRotation;
                cameraCaptured = true;
                pan = targetPan = Vector2.zero;
                savedCursorLock = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
            }
        }
        if (!cameraCaptured) return;
        if (!available) { RestoreCamera(); opened = false; return; }
        if (opened && targetZoom > 0f && Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            targetPan += new Vector2(delta.x, delta.y) * panSensitivity;
            targetPan.x = Mathf.Clamp(targetPan.x, -panLimit, panLimit);
            targetPan.y = Mathf.Clamp(targetPan.y, -panLimit, panLimit);
        }
        float blend = 1f - Mathf.Exp(-cameraSmoothness * Time.deltaTime);
        zoom = Mathf.Lerp(zoom, targetZoom, blend);
        pan = Vector2.Lerp(pan, targetPan, blend);
        Transform cam = worldCamera.transform;
        Vector3 origin = cam.parent != null ? cam.parent.TransformPoint(savedCameraPosition) : savedCameraPosition;
        Quaternion rotation = cam.parent != null ? cam.parent.rotation * savedCameraRotation : savedCameraRotation;
        Vector3 closePosition = pocket.transform.TransformPoint(new Vector3(pan.x * 0.12f, pan.y * 0.12f, -zoomViewDistance));
        Vector3 aim = pocket.transform.TransformPoint(new Vector3(pan.x, pan.y, 0.4f));
        Quaternion closeRotation = Quaternion.LookRotation(aim - closePosition, pocket.transform.up);
        float eased = Mathf.SmoothStep(0f, 1f, zoom);
        cam.SetPositionAndRotation(Vector3.Lerp(origin, closePosition, eased), Quaternion.Slerp(rotation, closeRotation, eased));
        if (targetZoom == 0f && zoom < 0.001f) RestoreCamera();
    }

    void RestoreCamera()
    {
        if (cameraCaptured && worldCamera != null)
        {
            worldCamera.transform.localPosition = savedCameraPosition;
            worldCamera.transform.localRotation = savedCameraRotation;
        }
        if (cameraCaptured)
        {
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
        }
        cameraCaptured = false;
        zoom = targetZoom = 0f;
        pan = targetPan = Vector2.zero;
    }

    void FindItem()
    {
        PocketInventoryItem target = null;
        Ray ray = cameraCaptured && Mouse.current != null
            ? worldCamera.ScreenPointToRay(Mouse.current.position.ReadValue())
            : new Ray(worldCamera.transform.position, worldCamera.transform.forward);
        if (IsPocketVisible && Physics.Raycast(ray,
            out RaycastHit hit, itemReach, 1 << pocketLayer, QueryTriggerInteraction.Collide))
        {
            var candidate = hit.collider.GetComponentInParent<PocketInventoryItem>();
            if (candidate != null && candidate.IsStored && candidate.pocketIndex == pocket.pocketIndex)
                target = candidate;
        }
        if (target == targetedItem) return;
        if (targetedItem != null) targetedItem.SetHighlight(false);
        targetedItem = target;
        if (targetedItem != null) targetedItem.SetHighlight(true);
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused || !cameraCaptured) return;
        RestoreCamera();
        opened = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SelectItem(PocketInventoryItem item)
    {
        if (!IsPocketVisible || item == null || !item.IsStored || item.pocketIndex != pocket.pocketIndex || handAnchor == null) return;
        ReturnHeldItem();
        HeldItem = item;
        item.Equip(handAnchor);
        targetedItem = null;
    }
    public void ReturnHeldItem()
    {
        if (HeldItem == null) return;
        HeldItem.ReturnToPocket();
        HeldItem = null;
    }

    public bool ThrowHeldBall()
    {
        // Finish leaning back before throwing into the surrounding world.
        if (HeldItem == null || !HeldItem.canThrow || cameraCaptured || player.isInspecting || WorldInteractionBusy) return false;
        Vector3 origin = worldCamera.transform.position;
        Vector3 direction = worldCamera.transform.forward;
        float radius = 0.16f * HeldItem.thrownScale;
        float distance = 0.25f;
        foreach (RaycastHit hit in Physics.SphereCastAll(origin, radius, direction, distance,
            ~(1 << pocketLayer), QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.01f));
        }
        if (!HeldItem.Throw(origin + direction * distance, direction * throwSpeed + Vector3.up * throwLift, transform)) return false;
        HeldItem = null;
        return true;
    }
    void OnDisable()
    {
        RestoreCamera();
        if (targetedItem != null) targetedItem.SetHighlight(false);
        targetedItem = null;
        IsPocketVisible = false;
        opened = false;
        reveal = 0f;
        if (generatedRoom != null) generatedRoom.gameObject.SetActive(false);
    }
    void OnDestroy()
    {
        ReturnHeldItem();
        if (initialized && worldCamera != null) worldCamera.cullingMask = originalWorldMask;
        if (initialized && worldCamera != null) worldCamera.nearClipPlane = originalNearClip;
        if (generatedRoom != null) Destroy(generatedRoom.gameObject);
        if (createdHand && handAnchor != null) Destroy(handAnchor.gameObject);
    }
    void OnGUI()
    {
        if (!showHelp) return;
        GUI.color = Color.white;
        if (CanTogglePocket)
            GUI.Label(new Rect(Screen.width * 0.5f - 85f, Screen.height * 0.72f, 170f, 25f),
                opened ? "[E] Close pocket" : "[E] Open pocket");
        if (IsPocketVisible)
            GUI.Label(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.78f, 300f, 25f),
                "Scroll to lean in/out | Mouse to look | Esc close");
        if (targetedItem != null)
            GUI.Box(new Rect(Screen.width * 0.5f - 85f, Screen.height * 0.65f, 170f, 25f), "Click - " + targetedItem.itemName);
        if (HeldItem != null)
            GUI.Label(new Rect(12f, 12f, 520f, 25f), "Holding " + HeldItem.itemName + " | G put back" +
                (HeldItem.canThrow ? (cameraCaptured ? " | Scroll back to throw" : " | Right-click throw") : ""));
    }
}
