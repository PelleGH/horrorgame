using System;
using System.Collections.Generic;
using UnityEngine;

public enum ScenerySide
{
    Both,
    Left,
    Right
}

public enum SceneryBuiltinVisual
{
    None,
    Pine,
    Bush,
    Rock,
    Grass,
    FallenLeaves,
    Branch,
    DeadTree,
    Fern,
    BroadleafTree
}

public enum SceneryPlacementMode { Uniform, Clusters }

/// <summary>A reusable palette, terrain character and collection of scenery layers.</summary>
[CreateAssetMenu(menuName = "Train/Scenery/Biome", fileName = "SceneryBiome")]
public class SceneryBiome : ScriptableObject
{
    [Header("Terrain")]
    [Min(0f)] public float hillHeightMultiplier = 1f;
    [Min(0.01f)] public float hillFrequencyMultiplier = 1f;
    public Color meadow = new Color(0.32f, 0.43f, 0.22f);
    public Color distantGrass = new Color(0.46f, 0.48f, 0.29f);
    public Color foliage = new Color(0.12f, 0.27f, 0.19f);
    public Color bark = new Color(0.24f, 0.16f, 0.105f);
    [Range(0, 4)] public float densityMultiplier = 1;
    [Range(0, .5f)] public float variation = .18f;
    [Min(0)] public float microterrainHeight = .18f;
    [Min(.001f)] public float microterrainScale = .12f;
    [Header("Persistent winter cover")]
    [Tooltip("Snow cover on built-in trees, bushes and rocks. Imported prefabs retain their authored appearance.")]
    [Range(0f, 1f)] public float snowCover;
    public Color snowColor = new Color(.79f, .87f, .93f);

    [Header("Objects")]
    [Tooltip("Independent layers for trees, undergrowth, rocks or your own prefab categories. Empty layers spawn nothing.")]
    public List<ScenerySpawnLayer> layers = new List<ScenerySpawnLayer>();
}

[Serializable]
public class ScenerySpawnLayer
{
    public string name = "Trees";
    [Tooltip("Stable random stream identity. Existing assets with an empty ID use their layer name. Keep this unchanged to preserve placements when adding other layers.")]
    public string id = "";
    public bool enabled = true;
    [Tooltip("Maximum placement attempts in each landscape section. Rejected placements stay empty; this also limits the pooled instance count.")]
    [Range(0, 2048)] public int attemptsPerChunk = 32;
    [Range(0, 4)] public float densityMultiplier = 1;
    [Tooltip("Chance of accepting each attempt before choosing a prefab. Use this for density and prefab weights for relative rarity.")]
    [Range(0f, 1f)] public float spawnChance = 1f;
    [Tooltip("Minimum and maximum lateral distance from the railway centre in metres. Track clearance is always respected.")]
    public Vector2 distanceFromTrack = new Vector2(10f, 130f);
    public ScenerySide side = ScenerySide.Both;
    [Header("Natural distribution")]
    public SceneryPlacementMode placement;
    public Vector2 clusterSize = new Vector2(3, 8);
    [Min(.1f)] public float clusterRadius = 5;
    [Tooltip("Minimum spacing within this layer, including across section borders. Uses a jittered world grid. Zero allows free placement.")]
    [Min(0)] public float minSpacing;
    [Min(.001f)] public float noiseScale = .04f;
    [Range(0, 1)] public float noiseThreshold;
    [Tooltip("Density across the layer's lateral range: 0 is near the track and 1 is the far edge.")]
    public AnimationCurve distanceDensityCurve = AnimationCurve.Linear(0, 1, 1, 1);
    [Tooltip("Terrain height relative to the railway, in metres. This filters ground elevation, separately from model Height.")]
    public Vector2 heightRange = new Vector2(-100, 300);
    public Vector2 slopeRange = new Vector2(0, 70);
    public Vector2 yawRange = new Vector2(0, 360);
    public Vector2 tiltDegrees = new Vector2(0, 4);

    [Header("Size and placement")]
    [Tooltip("Random uniform scale, multiplied by the prefab's authored scale and the selected choice's Scale Multiplier.")]
    public Vector2 scale = Vector2.one;
    [Tooltip("Normalize visual height before applying Scale. Useful when imported models use different units. Leave disabled to preserve authored prefab scale.")]
    public bool normalizeHeight;
    [Tooltip("Random visual height in metres when Normalize Height is enabled.")]
    public Vector2 height = new Vector2(5f, 13f);
    [Tooltip("Rotate the object up axis towards the terrain normal. Random yaw is still applied.")]
    public bool alignToSlope;
    [Range(0f, 90f)] public float maxSlope = 70f;
    [Tooltip("Offset from the terrain surface in metres, useful for prefab pivots above or below ground.")]
    public float surfaceOffset;

    [Header("Weighted prefab choices")]
    [Tooltip("Positive weights are relative: weight 1 among weights 1 and 9 means a 10% selection chance. Zero disables an entry. Prefab visuals keep their own materials.")]
    public List<SceneryPrefabChoice> prefabs = new List<SceneryPrefabChoice>();
}

[Serializable]
public class SceneryPrefabChoice
{
    [Tooltip("Drag a prefab from the Project window. Its visual children are supported; exterior objects are visual scenery rather than gameplay physics.")]
    public GameObject prefab;
    [Min(0f)] public float weight = 1f;
    [Tooltip("Optional built-in visual used only when Prefab is empty. None leaves this entry empty.")]
    public SceneryBuiltinVisual builtin = SceneryBuiltinVisual.None;
    [Tooltip("Additional local rotation for imported models with a different up or forward axis.")]
    public Vector3 rotationOffset;
    [Min(0.0001f)] public float scaleMultiplier = 1f;
    [Tooltip("Multiplies the built-in visual palette. Imported prefab materials remain authored on the prefab.")]
    public Color tint = Color.white;
}

[Serializable]
public class SceneryBiomeChoice
{
    public SceneryBiome biome;
    [Min(0f)] public float weight = 1f;
}
