using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put on an NPC or object to give it dialogue. Started by pressing the interact key while
/// looking at it (PlayerInteract; its collider must be on the interact layer), and/or by
/// walking into its trigger collider.
/// </summary>
public class DialogueTrigger : MonoBehaviour
{
    [Serializable]
    public class LineEvent
    {
        [Tooltip("Index of the line in the dialogue this trigger started (0 = first line).")]
        public int line;
        public UnityEvent onLineStarted;
    }

    [Header("Dialogue")]
    public DialogueAsset dialogue;

    [Tooltip("Played instead of 'Dialogue' once 'Dialogue' has been played before. Empty = replay 'Dialogue'.")]
    public DialogueAsset repeatDialogue;

    [Header("Behaviour")]
    [Tooltip("Can only be started once per scene load. Overrides 'Repeat Dialogue'.")]
    public bool playOnce = false;

    [Tooltip("Start automatically when the player enters this object's trigger collider.")]
    public bool triggerOnEnter = false;

    [Header("Presentation")]
    [Tooltip("Shown next to the interact key while looking at this, e.g. \"[E] talk\".")]
    public string promptText = "talk";

    [Tooltip("Turn the player's view towards the speaker during the dialogue.")]
    public bool focusCamera = true;

    [Tooltip("What the camera looks at. Empty = centre of this object's collider.")]
    public Transform focusPoint;

    [Tooltip("How strong the glow is. It is tinted from the object's own colour/texture.")]
    [Range(0f, 1f)] public float highlightIntensity = 0.3f;

    [Tooltip("Coloured materials get this much more saturated in the glow. Greys get lighter instead.")]
    [Range(0f, 1f)] public float highlightSaturationBoost = 0.25f;

    [Tooltip("Renderers that glow when looked at. Empty = all renderers on this object and its children.")]
    public Renderer[] highlightRenderers;

    [Header("Events")]
    public UnityEvent onStarted;
    public UnityEvent onFinished;

    [Tooltip("Fires for every line shown, with the line's index.")]
    public UnityEvent<int> onLineStarted;

    [Tooltip("Fires for specific lines of the dialogue this trigger started.")]
    public LineEvent[] lineEvents;

    private bool played;
    private DialogueAsset activeAsset;
    private class HighlightMaterial
    {
        public Material material;
        public Texture originalEmissionMap;
        public Texture glowMap;
        public Color glowColor;
    }

    private List<HighlightMaterial> highlightMaterials;
    private bool highlighted;

    /// <summary>The dialogue that would play right now.</summary>
    public DialogueAsset CurrentDialogue =>
        repeatDialogue != null && DialogueState.HasPlayed(dialogue) ? repeatDialogue : dialogue;

    public bool CanInteract =>
        !(playOnce && played) && CurrentDialogue != null && CurrentDialogue.IsPlayable;

    /// <summary>Starts the dialogue. Returns false if it could not start.</summary>
    public bool Interact()
    {
        if (!CanInteract)
            return false;

        DialogueAsset asset = CurrentDialogue;

        bool started = DialogueManager.Instance.StartDialogue(
            asset,
            focusCamera ? GetFocusPosition : (Func<Vector3>)null,
            HandleFinished,
            HandleLineStarted);

        if (started)
        {
            played = true;
            activeAsset = asset;
            onStarted?.Invoke();
        }

        return started;
    }

    public void SetHighlight(bool on)
    {
        if (on == highlighted)
            return;

        if (on && highlightMaterials == null)
            CacheHighlightMaterials();

        highlighted = on;

        if (highlightMaterials == null)
            return;

        foreach (HighlightMaterial entry in highlightMaterials)
        {
            Material material = entry.material;

            if (material == null)
                continue;

            if (on)
            {
                // Recomputed each time so inspector tweaks apply without a rebuild.
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", entry.glowColor * highlightIntensity);

                if (entry.glowMap != null)
                    material.SetTexture("_EmissionMap", entry.glowMap);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);

                if (entry.glowMap != null)
                    material.SetTexture("_EmissionMap", entry.originalEmissionMap);
            }
        }
    }

    // Glow tint derived from the material's own colour: coloured materials get a more saturated
    // version of their hue, greys get a lighter grey.
    private Color ComputeGlowColor(Material material)
    {
        Color baseColor = Color.white;

        if (material.HasProperty("_BaseColor"))
            baseColor = material.GetColor("_BaseColor");
        else if (material.HasProperty("_Color"))
            baseColor = material.GetColor("_Color");

        Color.RGBToHSV(baseColor, out float h, out float s, out float v);

        if (s < 0.15f)
        {
            // Grey/white: no hue to push, so make it lighter.
            s = 0f;
            v = Mathf.Lerp(v, 1f, 0.6f);
        }
        else
        {
            s = Mathf.Clamp01(s + highlightSaturationBoost);
            v = Mathf.Max(v, 0.7f);
        }

        return Color.HSVToRGB(h, s, v);
    }

    private void HandleLineStarted(DialogueAsset asset, int index)
    {
        onLineStarted?.Invoke(index);

        if (asset != activeAsset || lineEvents == null)
            return;

        foreach (LineEvent lineEvent in lineEvents)
        {
            if (lineEvent != null && lineEvent.line == index)
                lineEvent.onLineStarted?.Invoke();
        }
    }

    private void HandleFinished()
    {
        activeAsset = null;
        onFinished?.Invoke();
    }

    private Vector3 GetFocusPosition()
    {
        if (focusPoint != null)
            return focusPoint.position;

        Collider col = GetComponentInChildren<Collider>();
        return col != null ? col.bounds.center : transform.position;
    }

    private void CacheHighlightMaterials()
    {
        Renderer[] renderers = highlightRenderers != null && highlightRenderers.Length > 0
            ? highlightRenderers
            : GetComponentsInChildren<Renderer>();

        highlightMaterials = new List<HighlightMaterial>();

        foreach (Renderer rend in renderers)
        {
            if (rend == null)
                continue;

            foreach (Material material in rend.materials)
            {
                if (material == null || !material.HasProperty("_EmissionColor"))
                    continue;

                HighlightMaterial entry = new HighlightMaterial
                {
                    material = material,
                    glowColor = ComputeGlowColor(material)
                };

                // Textured materials glow in their own texture's colours.
                if (material.HasProperty("_EmissionMap"))
                {
                    Texture baseTexture = null;

                    if (material.HasProperty("_BaseMap"))
                        baseTexture = material.GetTexture("_BaseMap");
                    else if (material.HasProperty("_MainTex"))
                        baseTexture = material.GetTexture("_MainTex");

                    if (baseTexture != null)
                    {
                        entry.originalEmissionMap = material.GetTexture("_EmissionMap");
                        entry.glowMap = baseTexture;
                    }
                }

                highlightMaterials.Add(entry);
            }
        }
    }

    private void OnDestroy()
    {
        if (highlightMaterials == null)
            return;

        foreach (HighlightMaterial entry in highlightMaterials)
        {
            if (entry.material != null)
                Destroy(entry.material);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnEnter)
            return;

        if (other.GetComponentInParent<SimpleFPSControllerNewInput>() == null)
            return;

        Interact();
    }
}
