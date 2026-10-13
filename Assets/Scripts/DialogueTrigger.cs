
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DialogueTrigger : MonoBehaviour, IInteractable
{
    [Serializable]
    public class LineEvent
    {
        [Tooltip("0 = first line.")]
        public int line;
        public UnityEvent onLineStarted;
    }

    [Header("Dialogue")]
    public DialogueAsset dialogue;
    public DialogueAsset repeatDialogue;

    [Header("Behaviour")]
    public bool playOnce = false;
    public bool triggerOnEnter = false;

    [Header("Presentation")]
    public string promptText = "talk";
    public bool focusCamera = true;
    public Transform focusPoint;

    [Range(0f, 1f)]
    public float highlightIntensity = 0.3f;

    [Range(0f, 1f)]
    public float highlightSaturationBoost = 0.25f;

    public Renderer[] highlightRenderers;

    [Header("Events")]
    public UnityEvent onStarted;
    public UnityEvent onFinished;
    public UnityEvent<int> onLineStarted;
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

    public DialogueAsset CurrentDialogue =>
        repeatDialogue != null &&
        DialogueState.HasPlayed(dialogue)
            ? repeatDialogue
            : dialogue;

    public bool CanInteract =>
        isActiveAndEnabled &&
        !(playOnce && played) &&
        CurrentDialogue != null &&
        CurrentDialogue.IsPlayable;

    public string InteractionPrompt => promptText;

    // Called by the generic player interactor.
    void IInteractable.Interact(PlayerInteract player)
    {
        Interact();
    }

    // Also supports automatic trigger interactions.
    public bool Interact()
    {
        if (!CanInteract || DialogueManager.IsBusy)
            return false;

        // Don't interrupt an ongoing item inspection.
        SimpleFPSControllerNewInput controller =
            FindFirstObjectByType<SimpleFPSControllerNewInput>();

        if (controller != null && controller.isInspecting)
            return false;

        DialogueAsset asset = CurrentDialogue;

        bool started = DialogueManager.Instance.StartDialogue(
            asset,
            focusCamera ? GetFocusPosition : (Func<Vector3>)null,
            HandleFinished,
            HandleLineStarted
        );

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
                material.EnableKeyword("_EMISSION");

                material.SetColor(
                    "_EmissionColor",
                    entry.glowColor * highlightIntensity
                );

                if (entry.glowMap != null)
                    material.SetTexture("_EmissionMap", entry.glowMap);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);

                if (entry.glowMap != null)
                {
                    material.SetTexture(
                        "_EmissionMap",
                        entry.originalEmissionMap
                    );
                }
            }
        }
    }

    private Color ComputeGlowColor(Material material)
    {
        Color baseColor = Color.white;

        if (material.HasProperty("_BaseColor"))
            baseColor = material.GetColor("_BaseColor");
        else if (material.HasProperty("_Color"))
            baseColor = material.GetColor("_Color");

        Color.RGBToHSV(
            baseColor,
            out float h,
            out float s,
            out float v
        );

        if (s < 0.15f)
        {
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

        return col != null
            ? col.bounds.center
            : transform.position;
    }

    private void CacheHighlightMaterials()
    {
        Renderer[] renderers =
            highlightRenderers != null &&
            highlightRenderers.Length > 0
                ? highlightRenderers
                : GetComponentsInChildren<Renderer>();

        highlightMaterials = new List<HighlightMaterial>();

        foreach (Renderer rend in renderers)
        {
            if (rend == null)
                continue;

            foreach (Material material in rend.materials)
            {
                if (material == null ||
                    !material.HasProperty("_EmissionColor"))
                    continue;

                HighlightMaterial entry = new HighlightMaterial
                {
                    material = material,
                    glowColor = ComputeGlowColor(material)
                };

                if (material.HasProperty("_EmissionMap"))
                {
                    Texture baseTexture = null;

                    if (material.HasProperty("_BaseMap"))
                        baseTexture = material.GetTexture("_BaseMap");
                    else if (material.HasProperty("_MainTex"))
                        baseTexture = material.GetTexture("_MainTex");

                    if (baseTexture != null)
                    {
                        entry.originalEmissionMap =
                            material.GetTexture("_EmissionMap");

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

        if (other.GetComponentInParent<
            SimpleFPSControllerNewInput>() == null)
            return;

        Interact();
    }
}
