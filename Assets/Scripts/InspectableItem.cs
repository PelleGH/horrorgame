
using UnityEngine;

public class InspectableItem : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    public string promptText = "inspect";

    [Header("Position")]
    public float desiredDistance = 1.2f;
    public float minDistance = 0.35f;
    public float wallPadding = 0.08f;
    public float collisionRadius = 0.25f;

    [Header("Rotation")]
    public Vector3 startingRotationOffset;
    public float rotationSpeed = 160f;

    [Header("Scale")]
    public bool overrideScale = false;
    public Vector3 inspectScale = Vector3.one;

    public bool CanInteract => isActiveAndEnabled;

    public string InteractionPrompt => promptText;

    public void Interact(PlayerInteract player)
    {
        if (!CanInteract || player == null)
            return;

        if (player.itemInspector != null)
        {
            player.itemInspector.BeginInspect(this);
        }
    }

    public void SetHighlight(bool highlighted)
    {
        // Optional:
        // Add item highlighting here later.
    }
}
