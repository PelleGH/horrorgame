using UnityEngine;

public class PocketInventoryItem : MonoBehaviour
{
    public string itemName = "Coin";
    [HideInInspector] public int pocketIndex;
    public bool IsStored { get; private set; } = true;
    private Transform storedParent;
    private Vector3 storedPosition, storedScale;
    private Quaternion storedRotation;
    private Renderer[] renderers;
    private Color[] colors;
    private Collider[] colliders;
    private bool[] colliderStates;
    private int[] layers;
    private Transform[] parts;
    private MaterialPropertyBlock block;

    public void InitializeStorage()
    {
        storedParent = transform.parent;
        storedPosition = transform.localPosition;
        storedRotation = transform.localRotation;
        storedScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>();
        colors = new Color[renderers.Length];
        for (int i = 0; i < colors.Length; i++) colors[i] = renderers[i].sharedMaterial.color;
        colliders = GetComponentsInChildren<Collider>();
        colliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) colliderStates[i] = colliders[i].enabled;
        parts = GetComponentsInChildren<Transform>();
        layers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++) layers[i] = parts[i].gameObject.layer;
    }

    public void SetHighlight(bool selected)
    {
        if (renderers == null) InitializeStorage();
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = selected ? Color.Lerp(colors[i], new Color(1f, 0.82f, 0.3f), 0.65f) : colors[i];
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderers[i].SetPropertyBlock(block);
        }
    }

    public void Equip(Transform hand)
    {
        if (renderers == null) InitializeStorage();
        SetHighlight(false);
        IsStored = false;
        transform.SetParent(hand, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(15f, -15f, 0f);
        transform.localScale = storedScale * 0.32f;
        foreach (Collider collider in colliders) collider.enabled = false;
        foreach (Transform part in parts) part.gameObject.layer = 0;
    }

    public void ReturnToPocket()
    {
        if (storedParent == null) return;
        transform.SetParent(storedParent, false);
        transform.localPosition = storedPosition;
        transform.localRotation = storedRotation;
        transform.localScale = storedScale;
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = colliderStates[i];
        for (int i = 0; i < parts.Length; i++) parts[i].gameObject.layer = layers[i];
        IsStored = true;
    }
}
