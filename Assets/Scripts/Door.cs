
using UnityEngine;
using System.Collections;

public class Door : MonoBehaviour, IInteractable
{
    [Header("References")]
    public Transform doorRoot;
    public Renderer handleRenderer;

    [Header("Door Settings")]
    public float openAngle = 90f;
    public float speed = 3f;
    [Header("Door events")]
    public UnityEngine.Events.UnityEvent onOpening = new UnityEngine.Events.UnityEvent();
    public UnityEngine.Events.UnityEvent onClosing = new UnityEngine.Events.UnityEvent();
    public UnityEngine.Events.UnityEvent onOpened = new UnityEngine.Events.UnityEvent();
    public UnityEngine.Events.UnityEvent onClosed = new UnityEngine.Events.UnityEvent();
    public UnityEngine.Events.UnityEvent onMotionStopped = new UnityEngine.Events.UnityEvent();
    public bool IsOpen => isOpen;
    public bool IsAnimating => isAnimating;

    [Header("Interaction")]
    public string promptText = "open / close";

    private bool isOpen = false;
    private bool isAnimating = false;

    private Quaternion closedRot;
    private Quaternion openRot;

    private Material mat;

    public bool CanInteract =>
        isActiveAndEnabled && doorRoot != null;

    public string InteractionPrompt => promptText;

    private void Start()
    {
        if (doorRoot == null)
        {
            Debug.LogWarning(
                "Door is missing doorRoot: " + name
            );
            return;
        }

        closedRot = doorRoot.localRotation;
        openRot = Quaternion.Euler(0, openAngle, 0)
            * closedRot;

        if (handleRenderer != null)
        {
            mat = handleRenderer.material;
            mat.DisableKeyword("_EMISSION");
        }
    }

    // Called by PlayerInteract.
    public void Interact(PlayerInteract player)
    {
        if (CanInteract)
            ToggleDoor();
    }

    public void SetHighlight(bool state)
    {
        if (mat == null)
            return;

        if (state)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor(
                "_EmissionColor",
                Color.green * 2f
            );
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
        }
    }

    public void ToggleDoor()
    {
        if (isActiveAndEnabled && !isAnimating && doorRoot != null && speed > 0f)
            StartCoroutine(RotateDoor());
    }

    private IEnumerator RotateDoor()
    {
        isAnimating = true;
        if (isOpen) onClosing.Invoke(); else onOpening.Invoke();

        Quaternion target = isOpen
            ? closedRot
            : openRot;

        while (Quaternion.Angle(
            doorRoot.localRotation,
            target) > 0.1f)
        {
            doorRoot.localRotation = Quaternion.Slerp(
                doorRoot.localRotation,
                target,
                Time.deltaTime * speed
            );

            yield return null;
        }

        doorRoot.localRotation = target;

        isOpen = !isOpen;
        isAnimating = false;
        if (isOpen) onOpened.Invoke(); else onClosed.Invoke();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isAnimating = false;
        onMotionStopped.Invoke();
        SetHighlight(false);
    }
}
