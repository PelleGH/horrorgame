using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Train/Landscape Settings", fileName = "LandscapeSettings")]
public class LandscapeSettings : ScriptableObject
{
    [Header("Generation")]
    [Tooltip("A new runtime seed each session. Disable to replay Seed exactly. Saved assets are never modified.")]
    public bool randomizeSeedOnStart = true;
    public int seed = 1327;
    [Tooltip("Optional shader override for generated vertex-coloured scenery. Imported prefabs keep their own materials.")]
    public Shader generatedShader;
    [Header("Shared route / also drives the train's turning and incline")]
    public SceneryRouteSettings route = new SceneryRouteSettings();
    [Header("Weather along the journey")]
    public SceneryWeatherSettings weather = new SceneryWeatherSettings();
    [Header("Biome sequence")]
    [Tooltip("Weighted choices. Zero weight excludes a biome. Empty list uses the legacy tree settings below.")]
    public List<SceneryBiomeChoice> biomes = new List<SceneryBiomeChoice>();
    [Range(0, 4)] public float populationDensity = 1;
    [Min(80f)] public float biomeLength = 480f;
    [Min(0f)] public float biomeBlendDistance = 80f;
    [Header("Tunnels, lakes and landmarks")]
    public List<SceneryFeatureRule> features = new List<SceneryFeatureRule>();
    [Tooltip("Time rules and RequestFeature are placed this far ahead, outside the train.")]
    [Min(30f)] public float featureLookAhead = 180f;
    [Range(1, 32)] public int maxFeaturesPerChunk = 8;
    [Header("Custom prefab budget")]
    [Tooltip("Maximum custom prefab copies of EACH choice per section. Procedural shapes are batched separately.")]
    [Range(1, 512)] public int maxCopiesPerPrefabPerChunk = 64;
    [Range(64, 2048)] public int maxCustomPrefabInstancesPerChunk = 512;
    [Range(256, 16384)] public int maxPlacementAttemptsPerChunk = 8192;
    [Range(20000, 1000000)] public int maxBuiltInVerticesPerChunk = 200000;
    [Range(0, 1)] public float foliageWindStrength = .16f;
    [Header("Visible world / rebuild after changing geometry")]
    [Range(5, 15)] public int chunkCount = 9;
    [Min(30f)] public float chunkLength = 80f;
    [Min(60f)] public float halfWidth = 320f;
    [Range(12, 64)] public int sideResolution = 26;
    [Range(8, 48)] public int lengthResolution = 18;
    [Header("Rolling hills")]
    [Min(0f)] public float hillHeight = 22f;
    [Min(0.0001f)] public float hillFrequency = 0.005f;
    [Tooltip("Clear distance from the track centre on either side, in metres.")]
    [Min(3f)] public float trackClearance = 7f;
    [Header("Trees")]
    [Range(0, 160)] public int treesPerChunk = 64;
    public Vector2 treeHeight = new Vector2(5f, 13f);
    [Tooltip("Optional visual-only tree prefab. Default is a shared low-poly pine mesh.")]
    public GameObject treePrefab;
    [Header("Landmarks")]
    [Range(0f, 1f)] public float houseChance = 0.25f;
    [Header("Palette")]
    public Color meadow = new Color(0.32f, 0.43f, 0.22f);
    public Color distantGrass = new Color(0.46f, 0.48f, 0.29f);
    public Color foliage = new Color(0.12f, 0.27f, 0.19f);
    public Color bark = new Color(0.24f, 0.16f, 0.105f);
    public Color fog = new Color(0.66f, 0.74f, 0.73f);
    [Min(30f)] public float fogStart = 140f;
    [Min(60f)] public float fogEnd = 270f;
}
