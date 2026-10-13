using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Standalone test of the same inventory automatically added to the real player.
public class PocketInventoryTestEnvironment : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    public PocketInventoryController Inventory { get; private set; }

    void Start()
    {
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.15f, 0.15f);
        Box("Pocket test floor", new Vector3(0f, -0.2f, 0f), new Vector3(14f, 0.4f, 14f), new Color(0.16f, 0.2f, 0.23f));
        Box("Pocket test wall", new Vector3(0f, 2f, 6f), new Vector3(14f, 4f, 0.3f), new Color(0.3f, 0.35f, 0.4f));
        SimpleFPSControllerNewInput player = FindFirstObjectByType<SimpleFPSControllerNewInput>();
        if (player == null)
        {
            GameObject root = new GameObject("Player - same controller as the game");
            root.layer = 7;
            root.transform.position = Vector3.up * 0.08f;
            CharacterController character = root.AddComponent<CharacterController>();
            character.height = 1.8f; character.radius = 0.25f;
            character.center = Vector3.up * 0.9f; character.skinWidth = 0.02f;
            Camera camera = new GameObject("Player camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(root.transform, false);
            camera.transform.localPosition = Vector3.up * 1.6f;
            camera.nearClipPlane = 0.025f;
            camera.gameObject.AddComponent<AudioListener>();
            player = root.AddComponent<SimpleFPSControllerNewInput>();
            player.cameraTransform = camera.transform;
            player.Pitch = 70f;
            camera.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
        }
        Inventory = player.GetComponent<PocketInventoryController>();
        if (Inventory == null) Inventory = player.gameObject.AddComponent<PocketInventoryController>();
        Inventory.Initialize();
    }

    void Box(string name, Vector3 position, Vector3 size, Color color)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = position;
        box.transform.localScale = size;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material material = new Material(shader) { color = color };
        materials.Add(material);
        box.GetComponent<Renderer>().sharedMaterial = material;
    }
    void OnDestroy() { foreach (Material material in materials) if (material != null) Destroy(material); }
}
