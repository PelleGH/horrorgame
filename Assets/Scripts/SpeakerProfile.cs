using UnityEngine;

/// <summary>
/// Look and sound of a character's dialogue.
/// Create via Assets > Create > Dialogue > Speaker Profile.
/// </summary>
[CreateAssetMenu(fileName = "NewSpeaker", menuName = "Dialogue/Speaker Profile")]
public class SpeakerProfile : ScriptableObject
{
    public string displayName;
    public Color nameColor = new Color(0.5f, 0.5f, 0.46f, 0.85f);
    public Color textColor = new Color(0.72f, 0.72f, 0.68f, 0.95f);

    [Header("Typing")]
    [Tooltip("Multiplies the manager's typing speed. Below 1 is slower.")]
    public float speedMultiplier = 1f;
    [Tooltip("Optional. Overrides the manager's typing blip.")]
    public AudioClip blipClip;
    [Range(0f, 1f)] public float blipVolume = 0.4f;
    public Vector2 blipPitchRange = new Vector2(0.9f, 1.1f);
}
