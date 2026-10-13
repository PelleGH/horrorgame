using System;
using UnityEngine;

/// <summary>One line of spoken dialogue. Edited in the inspector on a DialogueAsset.</summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("Name shown above the text. Leave empty to use the profile's display name (or show no name).")]
    public string speaker;

    [TextArea(2, 6)]
    public string text;

    [Tooltip("Optional. Styles the line: name/text colour, typing blip and speed.")]
    public SpeakerProfile profile;

    [Tooltip("Optional voice clip played when the line starts.")]
    public AudioClip voice;
}

/// <summary>A player-selectable reply shown after the last line of a DialogueAsset.</summary>
[Serializable]
public class DialogueChoice
{
    public string text;

    [Tooltip("Dialogue to play when picked. Leave empty to end the conversation.")]
    public DialogueAsset next;
}
