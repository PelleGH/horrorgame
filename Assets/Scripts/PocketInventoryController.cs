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
    public bool WorldInteractionBusy { get; set; }
    public bool IsPocketVisible { get; private set; }
    public bool CanTogglePocket => initialized && pocket != null && isActiveAndEnabled
        && worldCamera.enabled && !player.isInspecting && !WorldInteractionBusy
        && DownAngle >= lookDownAngle;
    private float DownAngle => Mathf.Asin(Mathf.Clamp(-worldCamera.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
    public bool IsPointerOverPocket => targetedItem != null;
    public PocketInventoryItem HeldItem { get; private set; }
    private PocketInventoryItem targetedItem;
    private PocketInventoryRoom generatedRoom;
    private float bodyYaw, reveal;
    private int originalWorldMask;
    private float originalNearClip;
    private bool initialized, createdHand, opened;

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
        if (!available || downAngle < lookDownAngle - 10f) opened = false;
        if (CanTogglePocket && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            opened = !opened;
        float targetReveal = available && opened ? Mathf.InverseLerp(lookDownAngle - 10f, lookDownAngle + 5f, downAngle) : 0f;
        reveal = Mathf.Lerp(reveal, targetReveal, 1f - Mathf.Exp(-revealSpeed * Time.deltaTime));
        IsPocketVisible = available && opened && downAngle >= lookDownAngle && reveal > 0.1f;
        // Keep the waist facing still during downward free look so side items can be aimed at.
        if (downAngle < lookDownAngle - 10f)
            bodyYaw = Mathf.LerpAngle(bodyYaw, transform.eulerAngles.y, 1f - Mathf.Exp(-6f * Time.deltaTime));
        if (generatedRoom != null)
        {
            Quaternion facing = Quaternion.Euler(0f, bodyYaw, 0f);
            Vector3 offset = waistOffset;
            if (followCrouch)
                offset.y = Mathf.Max(0.1f,
                    worldCamera.transform.position.y - transform.position.y - cameraPocketDistance);
            Vector3 position = transform.position + facing * offset;
            // Stay underneath the eyes even when the camera has a local horizontal offset.
            position.x += worldCamera.transform.position.x - transform.position.x;
            position.z += worldCamera.transform.position.z - transform.position.z;
            generatedRoom.transform.SetPositionAndRotation(position,
                facing * Quaternion.Euler(90f, 0f, 0f));
            generatedRoom.transform.localScale = Vector3.one * pocketScale * Mathf.Max(0.001f, reveal);
            generatedRoom.gameObject.SetActive(reveal > 0.01f);
            generatedRoom.UpdateLighting(this);
            if (IsPocketVisible) Physics.SyncTransforms();
        }
        FindItem();
        if (IsPocketVisible && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && targetedItem != null)
            SelectItem(targetedItem);
        if (available && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) ReturnHeldItem();
    }

    void FindItem()
    {
        PocketInventoryItem target = null;
        if (IsPocketVisible && Physics.Raycast(worldCamera.transform.position, worldCamera.transform.forward,
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
    void OnDisable()
    {
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
        if (targetedItem != null)
            GUI.Box(new Rect(Screen.width * 0.5f - 85f, Screen.height * 0.65f, 170f, 25f), "Click - " + targetedItem.itemName);
        else if (HeldItem != null)
            GUI.Label(new Rect(12f, 12f, 320f, 25f), "Holding " + HeldItem.itemName + " | G put back");
    }
}
