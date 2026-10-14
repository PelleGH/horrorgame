
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
    [Header("Adaptive Audio (optional)")]
    public HorrorAudio.SoundEmitter soundEmitter;
    public HorrorAudio.GameSoundProfile openSound;
    public HorrorAudio.GameSoundProfile closeSound;
    public HorrorAudio.GameSoundProfile latchSound;
    [Tooltip("Optional direct files; use the emitter's object settings for their effects.")]
    public AudioClip openClip, closeClip, latchClip;

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
        if (soundEmitter == null) soundEmitter = GetComponent<HorrorAudio.SoundEmitter>();
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
        if (!isAnimating && doorRoot != null)
            StartCoroutine(RotateDoor());
    }

    private IEnumerator RotateDoor()
    {
        isAnimating = true;
        if (soundEmitter != null)
        {
            AudioClip clip = isOpen ? closeClip : openClip;
            if (clip != null) soundEmitter.PlayClip(clip);
            else if (soundEmitter.HasDirectSound) soundEmitter.PlayDefaultSound(forceOneShot: true);
            else soundEmitter.Play(isOpen ? closeSound : openSound);
        }

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
        if (soundEmitter != null)
        {
            if (latchClip != null) soundEmitter.PlayClip(latchClip);
            else soundEmitter.Play(latchSound);
        }
    }

    private void OnDisable()
    {
        SetHighlight(false);
    }
}
