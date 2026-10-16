using System;
using System.Collections.Generic;
using UnityEngine;

public enum SceneryFeatureKind
{
    Tunnel,
    Lake,
    Landmark
}

public enum SceneryFeatureSchedule
{
    Distance,
    Time,
    RepeatingDistance,
    Random
}

/// <summary>Reusable exterior feature; journey rules decide where it appears.</summary>
[CreateAssetMenu(menuName = "Train/Scenery/Feature", fileName = "SceneryFeature")]
public class SceneryFeature : ScriptableObject
{
    public SceneryFeatureKind kind = SceneryFeatureKind.Landmark;
    [Tooltip("Optional model. Leave empty for the built-in tunnel or lake. Use scenery layers for frequently repeated small objects.")]
    public GameObject prefab;
    [Tooltip("Cross-track width in metres. For a built-in tunnel this is its clear interior width.")]
    [Min(1f)] public float width = 22f;
    [Tooltip("Height in metres. A tunnel must clear your train cabin and roof.")]
    [Min(1f)] public float height = 7f;
    [Tooltip("Depth below the terrain surface for lakes, in metres.")]
    [Min(0f)] public float waterDepth = 3f;
    [Tooltip("Built-in visual tint. Imported prefab materials remain authored on the model.")]
    public Color tint = new Color(0.24f, 0.29f, 0.3f);
    [Tooltip("Additional local rotation for an imported model. Models should face local +Z and stand on local +Y.")]
    public Vector3 rotationOffset;
}

[Serializable]
public class SceneryFeatureRule
{
    public string name = "Feature";
    public bool enabled = true;
    public SceneryFeature feature;
    [Tooltip("Optional biome filter at the feature's start. Empty allows every biome.")]
    public List<SceneryBiome> allowedBiomes = new List<SceneryBiome>();
    public SceneryFeatureSchedule schedule = SceneryFeatureSchedule.Distance;
    [Tooltip("First start location along the journey in metres. Random schedules start their first interval here.")]
    [Min(0f)] public float startDistance = 500f;
    [Tooltip("Elapsed journey seconds for Time schedules. The event is placed ahead of the train so it can be streamed in.")]
    [Min(0f)] public float startTime = 30f;
    [Tooltip("Distance between starts for Repeating Distance schedules, in metres.")]
    [Min(1f)] public float repeatDistance = 1000f;
    [Tooltip("Minimum and maximum distance between attempts for Random schedules. The journey seed makes the sequence repeatable.")]
    public Vector2 randomSpacing = new Vector2(600f, 1200f);
    [Tooltip("Acceptance chance per scheduled occurrence, including repeated and random attempts.")]
    [Range(0f, 1f)] public float chance = 1f;
    [Tooltip("Length along the railway in metres; a custom model's local Z axis follows the railway.")]
    [Min(1f)] public float length = 100f;
    [Tooltip("Left or right of the railway. Both is suitable for a tunnel enclosing the track.")]
    public ScenerySide side = ScenerySide.Both;
    [Tooltip("Distance of the feature centre from the railway centre in metres. Use zero for tunnels.")]
    [Min(0f)] public float offsetFromTrack;
    [Min(0.001f)] public float scale = 1f;
    [Tooltip("Stop scheduling new occurrences beyond this journey distance. Zero means no end limit.")]
    [Min(0f)] public float endDistance;
}
