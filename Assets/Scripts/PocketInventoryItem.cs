using UnityEngine;

public class PocketInventoryItem : MonoBehaviour
{
    public string itemName = "Coin";
    public bool canThrow;
    [Min(0.01f)] public float throwMass = 0.15f;
    [Range(0f, 1f)] public float bounce = 0.65f;
    [Min(0.01f)] public float thrownScale = 0.5f;
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
    private Rigidbody body;
    private PhysicsMaterial bounceMaterial;
    private bool[] triggerStates;
    private Collider[] ignoredPlayerColliders;

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
        triggerStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderStates[i] = colliders[i].enabled;
            triggerStates[i] = colliders[i].isTrigger;
        }
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
        StopPhysics();
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
        StopPhysics();
        transform.SetParent(storedParent, false);
        transform.localPosition = storedPosition;
        transform.localRotation = storedRotation;
        transform.localScale = storedScale;
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = colliderStates[i];
        for (int i = 0; i < parts.Length; i++) parts[i].gameObject.layer = layers[i];
        IsStored = true;
    }

    public bool Throw(Vector3 position, Vector3 velocity, Transform player)
    {
        if (!canThrow || IsStored) return false;
        SetHighlight(false);
        transform.SetParent(null, true);
        transform.position = position;
        transform.localScale = Vector3.one * thrownScale;
        if (bounceMaterial == null)
            bounceMaterial = new PhysicsMaterial("Pocket ball bounce")
            {
                bounciness = bounce, dynamicFriction = 0.3f, staticFriction = 0.3f,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.mass = throwMass;
        body.isKinematic = false;
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        foreach (Collider collider in colliders)
        {
            collider.enabled = true;
            collider.isTrigger = false;
            collider.sharedMaterial = bounceMaterial;
        }
        ignoredPlayerColliders = player.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
            foreach (Collider playerCollider in ignoredPlayerColliders)
                if (playerCollider != null) Physics.IgnoreCollision(collider, playerCollider, true);
        body.linearVelocity = velocity;
        body.angularVelocity = new Vector3(4f, 7f, 2f);
        return true;
    }

    void StopPhysics()
    {
        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
        }
        if (ignoredPlayerColliders != null)
            foreach (Collider collider in colliders)
                foreach (Collider playerCollider in ignoredPlayerColliders)
                    if (playerCollider != null) Physics.IgnoreCollision(collider, playerCollider, false);
        ignoredPlayerColliders = null;
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].isTrigger = triggerStates[i];
            colliders[i].sharedMaterial = null;
        }
    }

    void OnDestroy()
    {
        if (bounceMaterial != null) Destroy(bounceMaterial);
    }
}
