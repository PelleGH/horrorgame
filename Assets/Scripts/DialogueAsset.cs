using UnityEngine;

/// <summary>A reusable conversation. Create via Assets > Create > Dialogue > Dialogue Asset.</summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Asset")]
public class DialogueAsset : ScriptableObject
{
    public DialogueLine[] lines;

    [Tooltip("Shown after the last line. Leave empty for no choices.")]
    public DialogueChoice[] choices;

    [Tooltip("Played automatically after this one when there are no choices.")]
    public DialogueAsset next;

    /// <summary>True if at least one choice has text.</summary>
    public bool HasChoices
    {
        get
        {
            if (choices == null)
                return false;

            foreach (DialogueChoice choice in choices)
            {
                if (choice != null && !string.IsNullOrWhiteSpace(choice.text))
                    return true;
            }

            return false;
        }
    }

    public bool HasLines => lines != null && lines.Length > 0;

    public bool IsPlayable => HasLines || HasChoices || next != null;
}
