using System;
using System.Collections.Generic;
using UnityEngine;

public enum SceneryWeatherKind { Snow, Rain, Clear }

[Serializable]
public class SceneryWeatherInterval
{
    public string name = "Weather override";
    [Min(0)] public float startDistance = 600;
    [Min(1)] public float length = 200;
    public SceneryWeatherKind kind = SceneryWeatherKind.Snow;
    [Range(0, 1)] public float intensity = 1;
}

[Serializable]
public class SceneryWeatherSettings
{
    public bool enabled = true;
    [Tooltip("The first stretch always has this weather, independent of the seed.")]
    public SceneryWeatherKind initialWeather = SceneryWeatherKind.Snow;
    [Min(0)] public float initialWeatherDistance = 350;
    [Min(80)] public float sectionLength = 500;
    [Min(1)] public float transitionDistance = 110;
    [Header("Relative chances after the initial stretch")]
    [Min(0)] public float snowWeight = 6;
    [Min(0)] public float rainWeight = 2;
    [Min(0)] public float clearWeight = 2;
    public Vector2 intensityRange = new Vector2(.5f, 1);
    public List<SceneryWeatherInterval> overrides = new List<SceneryWeatherInterval>();
    [Header("Bounded exterior particles")]
    [Range(100, 6000)] public int maxParticles = 2400;
    [Min(0)] public float snowParticlesPerSecond = 340;
    [Min(0)] public float rainParticlesPerSecond = 650;
    public Vector2 snowSize = new Vector2(.035f, .11f);
    public Vector2 rainSize = new Vector2(.012f, .026f);
    [Min(.1f)] public float snowFallSpeed = 2.4f;
    [Min(1)] public float rainFallSpeed = 17;
    public Vector2 wind = new Vector2(.6f, -.25f);
    [Min(0)] public float snowFlutter = .7f;
    [Range(0, 1)] public float visibilityReduction = .55f;
    [Range(0, 1)] public float tunnelPrecipitation = 0;
    [Header("Dry carriage envelope / local to the carriage")]
    public Vector3 carriageInteriorCenter = new Vector3(0, 1.2f, 0);
    public Vector3 carriageInteriorSize = new Vector3(4.6f, 4.4f, 13.4f);
}

public struct SceneryWeatherConditions
{
    public SceneryWeatherKind Kind;
    public float Intensity, SnowAmount, RainAmount, VisibilityMultiplier;
}
