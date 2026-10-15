using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorAudio
{
    /// <summary>Only scene geometry and attachment points; the demo owns all gameplay and sound.</summary>
    public sealed class CabinLayout
    {
        public Transform DoorHinge;
        public GameObject DoorPanel;
        public Transform BallSpawn;
        public Vector3 PlayerSpawn;
        public Transform VentPoint, WindowPoint, RattlePoint, RunningGearPoint, CorridorSpeakerPoint;
        public BoxCollider CabinRegion, CorridorRegion;
    }

    public static class CabinListeningGeometry
    {
        public static CabinLayout Build(Transform root)
        {
            return new Builder(root).Build();
        }

        sealed class Builder
        {
            readonly Transform root;
            readonly CabinGeometryResources resources;
            readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
            readonly Shader shader;
            readonly Color wall = new Color(0.59f, 0.54f, 0.43f);
            readonly Color wood = new Color(0.23f, 0.14f, 0.085f);
            readonly Color darkWood = new Color(0.12f, 0.075f, 0.05f);
            readonly Color brass = new Color(0.63f, 0.47f, 0.25f);
            readonly Color fabric = new Color(0.075f, 0.23f, 0.24f);
            readonly Color carpet = new Color(0.20f, 0.125f, 0.105f);
            readonly Color dark = new Color(0.035f, 0.045f, 0.05f);
            readonly Color glass = new Color(0.13f, 0.24f, 0.31f);
            readonly Color lamp = new Color(1f, 0.86f, 0.62f);

            public Builder(Transform root)
            {
                this.root = root;
                resources = root.gameObject.AddComponent<CabinGeometryResources>();
                shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
            }

            public CabinLayout Build()
            {
                CabinLayout layout = new CabinLayout();
                BuildShell();
                BuildSeats();
                BuildWindows();
                BuildFurniture();
                BuildDoor(layout);
                BuildLights();
                layout.PlayerSpawn = root.TransformPoint(new Vector3(0f, 0.1f, -2.5f));
                layout.BallSpawn = Point("Loose ball spawn", new Vector3(-0.6f, 1.5f, -0.7f));
                layout.VentPoint = Point("Ventilation sound", new Vector3(-2f, 2.5f, 0f));
                layout.WindowPoint = Point("Window sound", new Vector3(2.4f, 1.5f, -1.5f));
                layout.RattlePoint = Point("Table rattle sound", new Vector3(1.5f, 0.95f, 1f));
                layout.RunningGearPoint = Point("Running gear sound", new Vector3(0f, -0.2f, 0f));
                layout.CorridorSpeakerPoint = Point("Corridor speaker sound", new Vector3(0f, 1.4f, 6.3f));
                layout.CabinRegion = Region("Compartment acoustic region", new Vector3(0f, 1.45f, -0.5f), new Vector3(5.2f, 2.9f, 8f));
                layout.CorridorRegion = Region("Corridor acoustic region", new Vector3(0f, 1.45f, 5.5f), new Vector3(5.2f, 2.9f, 4f));
                return layout;
            }

            void BuildShell()
            {
                Box("Compartment floor", new Vector3(0f, -0.15f, -0.5f), new Vector3(5.6f, 0.3f, 8.2f), carpet, true, true);
                Box("Corridor floor", new Vector3(0f, -0.15f, 5.5f), new Vector3(5.6f, 0.3f, 4.2f), darkWood, true, true);
                Box("Corridor center runner", new Vector3(0f, 0.004f, 5.5f), new Vector3(1.65f, 0.008f, 4f), carpet, false);
                Box("Ceiling", new Vector3(0f, 3f, 1.5f), new Vector3(5.6f, 0.2f, 12.4f), wall, true, true);
                Box("Compartment end wall", new Vector3(0f, 1.45f, -4.6f), new Vector3(5.6f, 2.9f, 0.2f), wall, true, true);
                Box("Corridor end wall", new Vector3(0f, 1.45f, 7.6f), new Vector3(5.6f, 2.9f, 0.2f), wall, true, true);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 2.7f;
                    Box("Carriage side wall", new Vector3(x, 1.45f, 1.5f), new Vector3(0.2f, 2.9f, 12.4f), wall, true, true);
                    Box("Wood wall panel", new Vector3(side * 2.585f, 0.54f, 1.5f), new Vector3(0.035f, 1.05f, 12f), wood, false);
                    Box("Brass waist rail", new Vector3(side * 2.56f, 1.08f, 1.5f), new Vector3(0.04f, 0.04f, 12f), brass, false);
                    Box("Baseboard", new Vector3(side * 2.55f, 0.08f, 1.5f), new Vector3(0.07f, 0.16f, 12f), darkWood, false);
                    Box("Ceiling cornice", new Vector3(side * 2.54f, 2.78f, 1.5f), new Vector3(0.12f, 0.14f, 12f), wood, false);
                    for (int stripe = 0; stripe < 3; stripe++)
                        Box("Carpet edge line", new Vector3(side * (0.83f + stripe * 0.035f), 0.006f, -0.5f),
                            new Vector3(0.012f, 0.008f, 7.95f), brass * 0.65f, false);
                }
                Box("End wall wood panel", new Vector3(0f, 0.55f, -4.485f), new Vector3(5.2f, 1.08f, 0.03f), wood, false);
                Box("End wall rail", new Vector3(0f, 1.1f, -4.46f), new Vector3(5.2f, 0.04f, 0.04f), brass, false);
                // The corridor speaker has open space around it and faces the doorway.
                Box("Speaker pedestal", new Vector3(0f, 0.57f, 6.48f), new Vector3(0.28f, 1.14f, 0.28f), darkWood);
                Box("Corridor speaker cabinet", new Vector3(0f, 1.4f, 6.48f), new Vector3(0.56f, 0.5f, 0.2f), dark);
                for (int slot = 0; slot < 6; slot++)
                    Box("Speaker grille", new Vector3(0f, 1.23f + slot * 0.065f, 6.369f), new Vector3(0.45f, 0.012f, 0.012f), brass, false);
                Box("Speaker status light", new Vector3(0.21f, 1.57f, 6.366f), new Vector3(0.025f, 0.025f, 0.012f), new Color(0.35f, 0.75f, 0.43f), false);
            }

            void BuildSeats()
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int row = 0; row < 2; row++)
                    {
                        float z = -2.6f + row * 3.3f;
                        Box("Bench plinth", new Vector3(side * 2.12f, 0.23f, z), new Vector3(0.88f, 0.46f, 2.15f), darkWood);
                        Box("Seat cushion", new Vector3(side * 2.1f, 0.51f, z), new Vector3(0.93f, 0.18f, 2.2f), fabric);
                        Box("Upholstered seat back", new Vector3(side * 2.49f, 1f, z), new Vector3(0.16f, 0.9f, 2.18f), fabric);
                        for (int end = -1; end <= 1; end += 2)
                        {
                            Box("Bench arm", new Vector3(side * 2.11f, 0.78f, z + end * 1.08f), new Vector3(0.9f, 0.12f, 0.12f), wood);
                            Box("Arm support", new Vector3(side * 1.75f, 0.65f, z + end * 1.08f), new Vector3(0.07f, 0.27f, 0.07f), brass, false);
                        }
                        for (int seam = -1; seam <= 1; seam++)
                            Box("Cushion seam", new Vector3(side * 2.085f, 0.605f, z + seam * 0.55f), new Vector3(0.85f, 0.01f, 0.012f), fabric * 0.7f, false);
                    }
                    Box("Luggage shelf", new Vector3(side * 2.19f, 2.38f, -0.8f), new Vector3(0.78f, 0.055f, 5.7f), brass, false);
                    for (int bracket = 0; bracket < 3; bracket++)
                        Box("Luggage rack bracket", new Vector3(side * 2.2f, 2.3f, -3.1f + bracket * 2.3f), new Vector3(0.8f, 0.2f, 0.04f), wood, false);
                }
                Box("Luggage case", new Vector3(-2.18f, 2.57f, -1.6f), new Vector3(0.58f, 0.32f, 0.9f), wood, false);
                Box("Case strap", new Vector3(-2.18f, 2.736f, -1.6f), new Vector3(0.6f, 0.015f, 0.075f), brass, false);
            }

            void BuildWindows()
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int row = 0; row < 3; row++)
                    {
                        float z = -2.6f + row * 3.25f;
                        Box("Window surround", new Vector3(side * 2.565f, 1.83f, z), new Vector3(0.075f, 1.15f, 1.95f), wood, false);
                        Box("Blue evening window", new Vector3(side * 2.517f, 1.83f, z), new Vector3(0.025f, 1f, 1.77f), glass, false);
                        Box("Window reflection", new Vector3(side * 2.5f, 2.11f, z), new Vector3(0.008f, 0.03f, 1.66f), glass * 1.6f, false);
                        Box("Distant horizon", new Vector3(side * 2.498f, 1.63f, z), new Vector3(0.01f, 0.08f, 1.75f), glass * 0.65f, false);
                        Box("Window center mullion", new Vector3(side * 2.488f, 1.83f, z), new Vector3(0.04f, 1.02f, 0.045f), brass, false);
                        Box("Window sill", new Vector3(side * 2.46f, 1.28f, z), new Vector3(0.26f, 0.065f, 2f), wood, false);
                    }
            }

            void BuildFurniture()
            {
                // Wall-mounted table leaves the entire middle aisle clear.
                Box("Compartment table", new Vector3(1.7f, 0.82f, 1f), new Vector3(1.45f, 0.08f, 1.1f), wood);
                Box("Table rim", new Vector3(0.965f, 0.815f, 1f), new Vector3(0.035f, 0.09f, 1.11f), brass, false);
                Box("Table leg", new Vector3(1.7f, 0.4f, 1f), new Vector3(0.09f, 0.8f, 0.09f), brass);
                Box("Folded newspaper", new Vector3(1.85f, 0.868f, 0.91f), new Vector3(0.42f, 0.014f, 0.32f), new Color(0.76f, 0.72f, 0.6f), false);
                for (int line = 0; line < 5; line++)
                    Box("Newspaper print", new Vector3(1.85f, 0.877f, 0.8f + line * 0.045f), new Vector3(0.34f, 0.004f, 0.009f), darkWood, false);
                Cylinder("Table cup", new Vector3(1.42f, 0.945f, 1.18f), new Vector3(0.12f, 0.085f, 0.12f), new Color(0.72f, 0.7f, 0.62f));
                Cylinder("Coffee surface", new Vector3(1.42f, 1.032f, 1.18f), new Vector3(0.095f, 0.002f, 0.095f), darkWood, false);
                Box("Ventilation frame", new Vector3(-2.45f, 2.5f, 0f), new Vector3(0.08f, 0.34f, 0.78f), brass, false);
                Box("Ventilation recess", new Vector3(-2.4f, 2.5f, 0f), new Vector3(0.025f, 0.27f, 0.69f), dark, false);
                for (int slat = 0; slat < 5; slat++)
                    Box("Ventilation grille", new Vector3(-2.38f, 2.39f + slat * 0.052f, 0f), new Vector3(0.025f, 0.018f, 0.67f), brass, false);
                Box("End wall picture frame", new Vector3(0f, 1.95f, -4.44f), new Vector3(1.35f, 0.78f, 0.05f), brass, false);
                Box("Abstract landscape picture", new Vector3(0f, 1.95f, -4.406f), new Vector3(1.23f, 0.66f, 0.012f), glass, false);
                Box("Landscape hills", new Vector3(0f, 1.78f, -4.395f), new Vector3(1.21f, 0.29f, 0.008f), fabric, false);
            }

            void BuildDoor(CabinLayout layout)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Partition wall", new Vector3(side * 1.7f, 1.2f, 3.5f), new Vector3(2f, 2.4f, 0.16f), wall, true, true);
                    Box("Partition wood panel", new Vector3(side * 1.7f, 0.55f, 3.405f), new Vector3(1.8f, 1.08f, 0.025f), wood, false);
                    Box("Door jamb", new Vector3(side * 0.755f, 1.2f, 3.5f), new Vector3(0.1f, 2.4f, 0.22f), darkWood, false);
                    Box("Door jamb trim", new Vector3(side * 0.82f, 1.2f, 3.37f), new Vector3(0.035f, 2.4f, 0.025f), brass, false);
                }
                Box("Partition header", new Vector3(0f, 2.65f, 3.5f), new Vector3(5.6f, 0.5f, 0.16f), wall, true, true);
                Box("Door lintel", new Vector3(0f, 2.43f, 3.5f), new Vector3(1.65f, 0.07f, 0.22f), darkWood, false);
                layout.DoorHinge = Point("Compartment door hinge", new Vector3(-0.7f, 0f, 3.5f));
                layout.DoorPanel = Box("Compartment door panel", new Vector3(0.7f, 1.2f, 0f), new Vector3(1.4f, 2.4f, 0.12f), wood, true, true, layout.DoorHinge);
                SoundAcousticMaterial acoustic = layout.DoorPanel.GetComponent<SoundAcousticMaterial>();
                acoustic.baseTransmissionLossDb = 16f;
                acoustic.lossDbPerMetre = 20f;
                acoustic.fallbackThickness = 0.12f;
                acoustic.transmissionLowPass = 1800f;
                acoustic.fullFilteringThickness = 0.1f;
                foreach (float face in new[] { -1f, 1f })
                {
                    Box("Door inset", new Vector3(0.7f, 1.65f, face * 0.069f), new Vector3(1.12f, 1.05f, 0.018f), darkWood, false, false, layout.DoorHinge);
                    Box("Door panel detail", new Vector3(0.7f, 0.55f, face * 0.069f), new Vector3(1.1f, 0.72f, 0.018f), darkWood, false, false, layout.DoorHinge);
                    Box("Door handle backplate", new Vector3(1.2f, 1.09f, face * 0.085f), new Vector3(0.09f, 0.23f, 0.03f), brass, false, false, layout.DoorHinge);
                    Box("Door handle", new Vector3(1.12f, 1.15f, face * 0.13f), new Vector3(0.24f, 0.035f, 0.04f), brass, false, false, layout.DoorHinge);
                }
                Box("Door threshold", new Vector3(0f, 0.008f, 3.5f), new Vector3(1.4f, 0.016f, 0.16f), brass, false);
            }

            void BuildLights()
            {
                for (int i = 0; i < 3; i++)
                {
                    float z = -2.6f + i * 3.5f;
                    Box("Ceiling light housing", new Vector3(0f, 2.865f, z), new Vector3(0.8f, 0.07f, 0.65f), brass, false);
                    GameObject diffuser = Box("Warm ceiling light", new Vector3(0f, 2.817f, z), new Vector3(0.67f, 0.025f, 0.52f), lamp, false);
                    Material glow = diffuser.GetComponent<Renderer>().sharedMaterial;
                    if (glow.HasProperty("_EmissionColor"))
                    {
                        glow.EnableKeyword("_EMISSION");
                        glow.SetColor("_EmissionColor", lamp * 0.75f);
                    }
                    Light light = Point("Interior ceiling light", new Vector3(0f, 2.55f, z)).gameObject.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.87f, 0.7f);
                    light.intensity = 2.4f;
                    light.range = 8f;
                    light.shadows = LightShadows.None;
                }
                Light fill = Point("Soft cabin fill", Vector3.zero).gameObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.6f;
                fill.color = new Color(0.83f, 0.87f, 0.95f);
                fill.shadows = LightShadows.None;
                fill.transform.localRotation = Quaternion.Euler(65f, -20f, 0f);
            }

            Transform Point(string name, Vector3 position, Transform parent = null)
            {
                Transform point = new GameObject(name).transform;
                point.SetParent(parent == null ? root : parent, false);
                point.localPosition = position;
                return point;
            }

            BoxCollider Region(string name, Vector3 position, Vector3 size)
            {
                BoxCollider collider = Point(name, position).gameObject.AddComponent<BoxCollider>();
                collider.size = size;
                collider.isTrigger = true;
                return collider;
            }

            GameObject Box(string name, Vector3 position, Vector3 size, Color color,
                bool solid = true, bool acoustic = false, Transform parent = null)
            {
                return Primitive(PrimitiveType.Cube, name, position, size, color, solid, acoustic, parent);
            }

            GameObject Cylinder(string name, Vector3 position, Vector3 size, Color color, bool solid = true)
            {
                return Primitive(PrimitiveType.Cylinder, name, position, size, color, solid, false, null);
            }

            GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 size,
                Color color, bool solid, bool acoustic, Transform parent)
            {
                GameObject part = GameObject.CreatePrimitive(type);
                part.name = name;
                part.transform.SetParent(parent == null ? root : parent, false);
                part.transform.localPosition = position;
                part.transform.localScale = size;
                Renderer renderer = part.GetComponent<Renderer>();
                renderer.sharedMaterial = MaterialFor(color);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Collider collider = part.GetComponent<Collider>();
                if (!solid)
                {
                    collider.enabled = false;
                    Object.Destroy(collider);
                }
                if (acoustic)
                {
                    SoundAcousticMaterial material = part.AddComponent<SoundAcousticMaterial>();
                    material.baseTransmissionLossDb = 6f;
                    material.lossDbPerMetre = 20f;
                    material.fallbackThickness = Mathf.Min(size.x, size.y, size.z);
                    material.transmissionLowPass = 1200f;
                    material.absorption = 0.32f;
                }
                return part;
            }

            Material MaterialFor(Color color)
            {
                if (materials.TryGetValue(color, out Material existing)) return existing;
                Material material = new Material(shader) { name = "Cabin test material", hideFlags = HideFlags.DontSave };
                material.color = color;
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.2f);
                materials.Add(color, material);
                resources.materials.Add(material);
                return material;
            }
        }
    }

    // Runtime-only owner: material instances are released with the isolated test room.
    sealed class CabinGeometryResources : MonoBehaviour
    {
        internal readonly List<Material> materials = new List<Material>();

        void OnDestroy()
        {
            foreach (Material material in materials)
                if (material != null) Destroy(material);
        }
    }
}
