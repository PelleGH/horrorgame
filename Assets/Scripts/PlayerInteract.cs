
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    [Header("Raycast")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public LayerMask raycastMask = ~0;

    [Header("Input")]
    public KeyCode inspectKey = KeyCode.E;

    [Header("Inspection")]
    public ItemInspector itemInspector;

    private IInteractable currentTarget;
    private string currentPrompt;

    private void Awake()
    {
        if (itemInspector == null)
            itemInspector = GetComponent<ItemInspector>();
    }

    private void Update()
    {
        // Inspection mode has priority.
        if (itemInspector != null && itemInspector.IsBusy)
        {
            SetTarget(null);

            if (itemInspector.IsInspecting &&
                Input.GetKeyDown(inspectKey))
            {
                itemInspector.StopInspect();
            }

            return;
        }

        // Dialogue owns input while a conversation is active.
        if (DialogueManager.IsBusy)
        {
            SetTarget(null);
            return;
        }

        Camera cam = playerCamera != null
            ? playerCamera
            : Camera.main;

        if (cam == null)
        {
            SetTarget(null);
            return;
        }

        Ray ray = new Ray(
            cam.transform.position,
            cam.transform.forward
        );

        IInteractable target = null;

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactDistance,
            raycastMask,
            QueryTriggerInteraction.UseGlobal))
        {
            target = hit.collider
                .GetComponentInParent<IInteractable>();

            if (target != null && !target.CanInteract)
                target = null;
        }

        SetTarget(target);

        if (currentTarget != null &&
            Input.GetKeyDown(inspectKey))
        {
            currentTarget.Interact(this);

            // Clear prompts when an exclusive action starts.
            if (DialogueManager.IsBusy ||
                (itemInspector != null && itemInspector.IsBusy) ||
                !currentTarget.CanInteract)
            {
                SetTarget(null);
            }
        }
    }

    private void SetTarget(IInteractable target)
    {
        if (!ReferenceEquals(currentTarget, target))
        {
            if (currentTarget is UnityEngine.Object oldObject &&
                oldObject != null)
            {
                currentTarget.SetHighlight(false);
            }

            if (currentPrompt != null)
                DialogueManager.HidePrompt();

            currentTarget = target;
            currentPrompt = null;

            if (currentTarget is UnityEngine.Object newObject &&
                newObject != null)
            {
                currentTarget.SetHighlight(true);
            }
        }

        if (currentTarget == null)
        {
            if (currentPrompt != null)
                DialogueManager.HidePrompt();

            currentPrompt = null;
            return;
        }

        string prompt = currentTarget.InteractionPrompt;

        string displayText = string.IsNullOrWhiteSpace(prompt)
            ? null
            : "[" + inspectKey + "] " + prompt;

        if (displayText == currentPrompt)
            return;

        currentPrompt = displayText;

        if (displayText == null)
        {
            DialogueManager.HidePrompt();
        }
        else
        {
            DialogueManager.Instance
                .ShowInteractPrompt(displayText);
        }
    }

    private void OnDisable()
    {
        SetTarget(null);
    }
}
