using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Miniature pocket cavity rendered by the normal player camera at the waist.
public class PocketInventoryRoom : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    private Mesh liningMesh;
    private Mesh backMesh;
    private int pocketLayer;
    private Light centerLight;
    private Material liningMaterial;

    public static PocketInventoryRoom Create(PocketInventoryController owner)
    {
        GameObject root = new GameObject("Pocket inventory - dark interior");
        root.transform.SetPositionAndRotation(owner.transform.position + owner.waistOffset,
            Quaternion.Euler(90f, 0f, 0f));
        root.transform.localScale = Vector3.one * owner.pocketScale;
        if (owner.gameObject.scene.IsValid() && owner.gameObject.scene.isLoaded)
            SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
        PocketInventoryRoom room = root.AddComponent<PocketInventoryRoom>();
        room.pocketLayer = owner.pocketLayer;
        room.Build(owner);
        return room;
    }

    void Build(PocketInventoryController owner)
    {
        PocketEntrance entrance = gameObject.AddComponent<PocketEntrance>();
        entrance.pocketName = "Pocket";
        entrance.pocketIndex = 0;
        Transform view = new GameObject("Pocket viewpoint").transform;
        view.SetParent(transform, false);
        entrance.interiorView = view;
        owner.pocket = entrance;
        liningMaterial = Material(Color.white * owner.liningBrightness);
        BuildLining(liningMaterial);
        BuildBack(liningMaterial);
        BuildPocketHem();
        BuildFabricDetails();
        GameObject lamp = new GameObject("Soft center pocket light");
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = new Vector3(0f, 0.18f, 0.25f);
        lamp.layer = pocketLayer;
        Light light = lamp.AddComponent<Light>();
        centerLight = light;
        light.type = LightType.Point;
        light.color = new Color(1f, 0.84f, 0.63f);
        light.intensity = owner.centerLightIntensity;
        light.range = 2.3f * owner.pocketScale;
        light.cullingMask = 1 << pocketLayer;
        light.shadows = LightShadows.None;
        GameObject glow = Primitive(PrimitiveType.Sphere, "Small center glow", transform,
            lamp.transform.localPosition, Vector3.one * 0.045f, Material(new Color(0.6f, 0.4f, 0.15f)));
        glow.GetComponent<Collider>().enabled = false;
        glow.layer = pocketLayer;
        Material glowMaterial = glow.GetComponent<Renderer>().sharedMaterial;
        if (glowMaterial.HasProperty("_EmissionColor"))
        {
            glowMaterial.EnableKeyword("_EMISSION");
            glowMaterial.SetColor("_EmissionColor", new Color(0.5f, 0.28f, 0.07f));
        }
        CreateItem(transform, 0, "Coin", new Vector3(-0.5f, -0.08f, 0.35f), new Color(0.85f, 0.55f, 0.12f), 0);
        CreateItem(transform, 0, "Key", new Vector3(0f, -0.08f, 0.4f), new Color(0.55f, 0.65f, 0.75f), 1);
        CreateItem(transform, 0, "Ball", new Vector3(0.5f, -0.08f, 0.35f), new Color(0.18f, 0.4f, 0.8f), 2);
        Physics.SyncTransforms();
    }

    public void UpdateLighting(PocketInventoryController owner)
    {
        if (centerLight != null)
        {
            float scale = transform.lossyScale.x;
            centerLight.range = 2.3f * scale;
            centerLight.intensity = owner.centerLightIntensity *
                (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? scale * scale : 1f);
        }
        if (liningMaterial != null) liningMaterial.color = Color.white * owner.liningBrightness;
    }

    void BuildLining(Material material)
    {
        const int segments = 40;
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        int[] triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            for (int ring = 0; ring < 2; ring++)
                vertices[ring * (segments + 1) + i] = new Vector3(Mathf.Cos(angle) * (ring == 0 ? 1.2f : 0.85f),
                    Mathf.Sin(angle) * (ring == 0 ? 0.8f : 0.6f), ring == 0 ? -0.15f + Mathf.Sin(angle * 3f) * 0.035f : 0.7f);
            if (i == segments) continue;
            int a = i, b = i + 1, c = segments + 1 + i, d = c + 1;
            int t = i * 6;
            triangles[t] = a; triangles[t + 1] = c; triangles[t + 2] = b;
            triangles[t + 3] = b; triangles[t + 4] = c; triangles[t + 5] = d;
        }
        liningMesh = new Mesh { name = "Inward-facing pocket lining", vertices = vertices, triangles = triangles };
        liningMesh.RecalculateNormals();
        liningMesh.RecalculateBounds();
        GameObject lining = new GameObject("Curved dark pocket lining", typeof(MeshFilter), typeof(MeshRenderer));
        lining.transform.SetParent(transform, false);
        lining.layer = pocketLayer;
        lining.GetComponent<MeshFilter>().sharedMesh = liningMesh;
        lining.GetComponent<Renderer>().sharedMaterial = material;
    }

    void BuildBack(Material material)
    {
        const int segments = 40;
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        vertices[0] = new Vector3(0f, 0f, 0.7f);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.85f, Mathf.Sin(angle) * 0.6f, 0.7f);
            if (i == segments) continue;
            triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1;
        }
        backMesh = new Mesh { name = "Elliptical dark pocket bottom", vertices = vertices, triangles = triangles };
        backMesh.RecalculateNormals();
        backMesh.RecalculateBounds();
        GameObject back = new GameObject("Dark pocket bottom", typeof(MeshFilter), typeof(MeshRenderer));
        back.transform.SetParent(transform, false);
        back.layer = pocketLayer;
        back.GetComponent<MeshFilter>().sharedMesh = backMesh;
        back.GetComponent<Renderer>().sharedMaterial = material;
    }

    void BuildPocketHem()
    {
        Material fabric = Material(new Color(0.12f, 0.23f, 0.24f));
        if (fabric.HasProperty("_Smoothness")) fabric.SetFloat("_Smoothness", 0f);
        if (fabric.HasProperty("_Glossiness")) fabric.SetFloat("_Glossiness", 0f);
        Material thread = Material(new Color(0.72f, 0.53f, 0.28f));
        const int segments = 40;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 point = new Vector3(Mathf.Cos(angle) * 1.2f, Mathf.Sin(angle) * 0.8f,
                -0.15f + Mathf.Sin(angle * 3f) * 0.035f);
            float tangent = Mathf.Atan2(0.8f * Mathf.Cos(angle), -1.2f * Mathf.Sin(angle)) * Mathf.Rad2Deg;
            GameObject hem = Box("Folded fabric pocket edge", transform, point,
                new Vector3(0.2f, 0.15f, 0.1f), fabric, false);
            hem.layer = pocketLayer;
            hem.transform.localRotation = Quaternion.Euler(0f, 0f, tangent);
            GameObject stitch = Box("Pocket seam stitch", transform, point + Vector3.back * 0.05f,
                new Vector3(0.055f, 0.012f, 0.012f), thread, false);
            stitch.layer = pocketLayer;
            stitch.transform.localRotation = hem.transform.localRotation;
        }
    }

    void BuildFabricDetails()
    {
        Material patch = Material(new Color(0.3f, 0.13f, 0.09f));
        Material thread = Material(new Color(0.65f, 0.47f, 0.25f));
        GameObject label = Box("Worn leather pocket label", transform, new Vector3(0.45f, 0.62f, 0.06f),
            new Vector3(0.42f, 0.16f, 0.025f), patch, false);
        label.layer = pocketLayer;
        label.transform.localRotation = Quaternion.Euler(25f, 0f, -12f);
        for (int i = 0; i < 5; i++)
        {
            GameObject stitch = Box("Label hand stitching", label.transform,
                new Vector3(-0.36f + i * 0.18f, 0.3f, -0.6f), new Vector3(0.065f, 0.1f, 0.15f), thread, false);
            stitch.layer = pocketLayer;
        }
        // Small contrasting seams run down the bag without covering the item slots.
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 9; i++)
            {
                float depth = i / 8f;
                GameObject stitch = Box("Interior side seam", transform,
                    new Vector3(side * Mathf.Lerp(1.15f, 0.81f, depth), 0f, Mathf.Lerp(-0.08f, 0.66f, depth)),
                    new Vector3(0.02f, 0.065f, 0.025f), thread, false);
                stitch.layer = pocketLayer;
            }
    }

    void CreateItem(Transform parent, int pocket, string name, Vector3 position, Color color, int shape)
    {
        GameObject item = new GameObject(name + " in pocket " + pocket);
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        Material material = Material(color);
        if (shape == 0)
        {
            GameObject coin = Primitive(PrimitiveType.Cylinder, "Coin disc", item.transform,
                Vector3.zero, new Vector3(0.38f, 0.035f, 0.38f), material);
            coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box("Coin engraving", item.transform, new Vector3(0f, 0f, -0.075f), new Vector3(0.025f, 0.18f, 0.015f),
                Material(color * 1.45f), false).layer = pocketLayer;
        }
        else if (shape == 1)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                GameObject ring = Box("Key ring segment", item.transform,
                    new Vector3(Mathf.Cos(angle) * 0.12f, 0.14f + Mathf.Sin(angle) * 0.12f, 0f),
                    new Vector3(0.11f, 0.035f, 0.05f), material, true);
                ring.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
            }
            Box("Key shaft", item.transform, new Vector3(0f, -0.10f, 0f), new Vector3(0.04f, 0.3f, 0.05f), material, true);
            for (int tooth = 0; tooth < 2; tooth++)
                Box("Key tooth", item.transform, new Vector3(0.055f, -0.17f - tooth * 0.07f, 0f),
                    new Vector3(0.13f, 0.035f, 0.05f), material, true);
        }
        else Primitive(PrimitiveType.Sphere, "Ball", item.transform, Vector3.zero, Vector3.one * 0.32f, material);
        foreach (Transform part in item.GetComponentsInChildren<Transform>()) part.gameObject.layer = pocketLayer;
        // Selection volumes must not push the character or collide with the world.
        foreach (Collider collider in item.GetComponentsInChildren<Collider>()) collider.isTrigger = true;
        PocketInventoryItem data = item.AddComponent<PocketInventoryItem>();
        data.itemName = name; data.pocketIndex = pocket;
        data.canThrow = shape == 2;
        data.InitializeStorage();
    }

    Material Material(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { color = color };
        materials.Add(material);
        return material;
    }

    GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collision)
    {
        GameObject box = Primitive(PrimitiveType.Cube, name, parent, position, size, material);
        box.GetComponent<Collider>().enabled = collision;
        return box;
    }

    GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = size;
        shape.GetComponent<Renderer>().sharedMaterial = material;
        return shape;
    }


    void OnDestroy()
    {
        foreach (Material material in materials) if (material != null) Destroy(material);
        if (liningMesh != null) Destroy(liningMesh);
        if (backMesh != null) Destroy(backMesh);
    }
}
