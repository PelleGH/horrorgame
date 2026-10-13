using UnityEngine;



public class PlayerInteract : MonoBehaviour
{
    public float interactDistance = 3f;
    public LayerMask interactLayer;

   public KeyCode inspectKey = KeyCode.E;

    private Door currentDoor;
    private DialogueTrigger currentDialogue;

    void Update()
    {
        if (DialogueManager.IsBusy)
        {
            SetCurrentDialogue(null);
            return;
        }

        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;
        DialogueTrigger lookedAtDialogue = null;

        if (Physics.Raycast(ray, out hit, interactDistance, interactLayer))
        {
            Door door = hit.collider.GetComponentInParent<Door>();

            if (door != null)
            {
                SetCurrentDialogue(null);
                currentDoor = door;
                door.SetHighlight(true);

                if (Input.GetKeyDown(inspectKey))
                {
                    door.ToggleDoor();
                }

                return;
            }

            DialogueTrigger dialogue = hit.collider.GetComponentInParent<DialogueTrigger>();

            if (dialogue != null && dialogue.CanInteract)
            {
                lookedAtDialogue = dialogue;

                if (Input.GetKeyDown(inspectKey))
                {
                    dialogue.Interact();

                    if (DialogueManager.IsBusy)
                        lookedAtDialogue = null;
                }
            }
        }

        SetCurrentDialogue(lookedAtDialogue);

        // If not looking anymore
        if (currentDoor != null)
        {
            currentDoor.SetHighlight(false);
            currentDoor = null;
        }
    }

    void SetCurrentDialogue(DialogueTrigger dialogue)
    {
        if (dialogue == currentDialogue)
            return;

        if (currentDialogue != null)
            currentDialogue.SetHighlight(false);

        currentDialogue = dialogue;

        if (dialogue != null)
        {
            dialogue.SetHighlight(true);
            DialogueManager.Instance.ShowInteractPrompt("[" + inspectKey + "] " + dialogue.promptText);
        }
        else
        {
            DialogueManager.HidePrompt();
        }
    }

    void OnDisable()
    {
        SetCurrentDialogue(null);
    }
}
