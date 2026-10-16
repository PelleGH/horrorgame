using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[AddComponentMenu("Train/Scenery Weather")]
public class TrainSceneryWeather : MonoBehaviour
{
    public SceneryWeatherKind CurrentKind => Conditions.Kind;
    public float Intensity => Conditions.Intensity;
    public float SnowAmount => Conditions.SnowAmount;
    public float VisibilityMultiplier => Conditions.VisibilityMultiplier;
    public SceneryWeatherConditions Conditions { get; private set; }
    public int ParticleCount { get { int count = 0; foreach (var e in emitters) count += e.system.particleCount; return count; } }
    public int ParticleCapacity { get; private set; }
    public bool ManualWeather { get; private set; }
    public SceneryWeatherKind ManualKind { get; private set; }
    readonly List<Emitter> emitters = new List<Emitter>();
    readonly Vector4[] beamPositions = new Vector4[SceneryLighting.MaximumBeams], beamDirections = new Vector4[SceneryLighting.MaximumBeams], beamColors = new Vector4[SceneryLighting.MaximumBeams];
    LandscapeSettings settings;
    RollingLandscape landscape;
    StationaryTrainSimulation simulation;
    Transform cabin;
    Material snowMaterial, rainMaterial;
    int observedSeed;
    class Emitter { public ParticleSystem system; public ParticleSystem.Particle[] buffer; public bool snow; }
    SceneryWeatherSettings Config => settings.weather;
    [ContextMenu("Rebuild Weather Volumes")]
    public void RebuildWeather()
    {
        if (Application.isPlaying && settings != null && landscape != null) Initialize(settings, landscape, simulation, cabin);
    }
    void OnDisable() { foreach (var emitter in emitters) emitter.system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    void OnEnable() { foreach (var emitter in emitters) emitter.system.Play(); }

    public void Initialize(LandscapeSettings source, RollingLandscape world, StationaryTrainSimulation train, Transform carriage)
    {
        settings = source; landscape = world; simulation = train; cabin = carriage;
        if (settings.weather == null) settings.weather = new SceneryWeatherSettings();
        foreach (var emitter in emitters) { emitter.system.gameObject.SetActive(false); Destroy(emitter.system.gameObject); }
        emitters.Clear(); ParticleCapacity = 0;
        if (snowMaterial != null) Destroy(snowMaterial);
        if (rainMaterial != null) Destroy(rainMaterial);
        var shader = Resources.Load<Shader>("SceneryPrecipitation");
        if (shader == null) shader = Shader.Find("Train/Scenery Precipitation");
        snowMaterial = new Material(shader) { name = "Headlight snow" };
        rainMaterial = new Material(shader) { name = "Headlight rain" };
        rainMaterial.SetFloat("_Rain", 1);
        int capacity = Mathf.Clamp(Config.maxParticles, 100, 6000) / 6;
        Bounds interior = InteriorBounds();
        // Shape centres are expressed in the coach frame, including its raised deck.
        // Keep the entire birth volume above the roof; falling particles then travel
        // through the exterior view rather than appearing inside window openings.
        const float volumeHeight = 7f;
        float spawnY = interior.max.y + .4f + volumeHeight * .5f;
        float sideLength = interior.size.z + 24f;
        float sideZ = interior.center.z + interior.extents.z * .25f;
        for (int side = -1; side <= 1; side += 2)
        {
            var position = new Vector3(interior.center.x + side * (interior.extents.x + 4.5f), spawnY, sideZ);
            var size = new Vector3(8f, volumeHeight, sideLength);
            CreateEmitter(position, size, true, capacity);
            CreateEmitter(position, size, false, capacity);
        }
        var forwardPosition = new Vector3(interior.center.x, spawnY, interior.max.z + 13f);
        var forwardSize = new Vector3(interior.size.x + 14f, volumeHeight, 24f);
        CreateEmitter(forwardPosition, forwardSize, true, capacity);
        CreateEmitter(forwardPosition, forwardSize, false, capacity);
        observedSeed = world.ActiveSeed; ApplyDistance(world.DistanceTravelled);
    }
    Bounds InteriorBounds() => new Bounds(Config.carriageInteriorCenter, Vector3.Max(Vector3.one * .1f, Config.carriageInteriorSize));
    void CreateEmitter(Vector3 position, Vector3 size, bool snow, int capacity)
    {
        var go = new GameObject(snow ? "Snow volume" : "Rain volume"); go.transform.SetParent(transform, false);
        Transform frame = cabin != null ? cabin : transform;
        go.transform.position = frame.TransformPoint(position);
        go.transform.rotation = frame.rotation;
        // The weather root and the carriage can have different positions, rotations
        // and scales. Shape dimensions remain coach-local metres in either case.
        Vector3 desiredScale = frame.lossyScale, parentScale = transform.lossyScale;
        go.transform.localScale = new Vector3(SafeRatio(desiredScale.x, parentScale.x), SafeRatio(desiredScale.y, parentScale.y), SafeRatio(desiredScale.z, parentScale.z));
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.loop = true; main.maxParticles = capacity;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startSpeed = 0;
        main.startLifetime = snow ? 7 : 1.8f; main.startSize = new ParticleSystem.MinMaxCurve(snow ? Config.snowSize.x : Config.rainSize.x, snow ? Config.snowSize.y : Config.rainSize.y);
        main.startColor = snow ? new Color(.84f,.91f,1,.85f) : new Color(.58f,.73f,.87f,.65f);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = size;
        var emission = ps.emission; emission.rateOverTime = 0;
        var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = Config.wind.x; velocity.y = -(snow ? Config.snowFallSpeed : Config.rainFallSpeed); velocity.z = 0;
        var noise = ps.noise; noise.enabled = snow && Config.snowFlutter > 0; noise.strength = Config.snowFlutter; noise.frequency = .24f; noise.scrollSpeed = .35f; noise.quality = ParticleSystemNoiseQuality.Low;
        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = snow ? snowMaterial : rainMaterial;
        renderer.renderMode = snow ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = snow ? 1 : 8; renderer.velocityScale = snow ? 0 : .035f;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.maxParticleSize = .04f; ps.useAutoRandomSeed = false;
        ps.randomSeed = (uint)(landscape.ActiveSeed ^ (emitters.Count + 1) * 739);
        emitters.Add(new Emitter {system = ps, snow = snow, buffer = new ParticleSystem.Particle[capacity]}); ParticleCapacity += capacity;
        ps.Play();
    }
    static float SafeRatio(float value, float divisor) => Mathf.Abs(divisor) > .0001f ? value / divisor : 1f;
    static double Unit(int seed, long index, int salt)
    {
        unchecked { uint h = (uint)seed ^ (uint)index * 0x9e3779b9u ^ (uint)(index >> 32) ^ (uint)salt; h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16; return h / 4294967296d; }
    }
    SceneryWeatherConditions State(SceneryWeatherKind kind, float intensity)
    {
        intensity = kind == SceneryWeatherKind.Clear ? 0 : Mathf.Clamp01(intensity);
        return new SceneryWeatherConditions {Kind = kind, Intensity = intensity, SnowAmount = kind == SceneryWeatherKind.Snow ? intensity : 0, RainAmount = kind == SceneryWeatherKind.Rain ? intensity : 0,
            VisibilityMultiplier = 1 - Config.visibilityReduction * intensity};
    }
    SceneryWeatherConditions Procedural(long section, int seed)
    {
        if (section < 0) return State(Config.initialWeather, Mathf.Max(Config.intensityRange.x, Config.intensityRange.y));
        double snow = Math.Max(0,Config.snowWeight), rain = Math.Max(0,Config.rainWeight), clear = Math.Max(0,Config.clearWeight);
        double pick = Unit(seed,section,997) * (snow + rain + clear);
        var kind = snow + rain + clear <= 0 ? SceneryWeatherKind.Clear : pick < snow ? SceneryWeatherKind.Snow : pick < snow + rain ? SceneryWeatherKind.Rain : SceneryWeatherKind.Clear;
        return State(kind, Mathf.Lerp(Mathf.Min(Config.intensityRange.x,Config.intensityRange.y), Mathf.Max(Config.intensityRange.x,Config.intensityRange.y),(float)Unit(seed,section,431)));
    }
    static SceneryWeatherConditions Blend(SceneryWeatherConditions a, SceneryWeatherConditions b, float t)
    {
        return new SceneryWeatherConditions {Kind = t < .5f ? a.Kind : b.Kind, Intensity = Mathf.Lerp(a.Intensity,b.Intensity,t), SnowAmount = Mathf.Lerp(a.SnowAmount,b.SnowAmount,t),
            RainAmount = Mathf.Lerp(a.RainAmount,b.RainAmount,t), VisibilityMultiplier = Mathf.Lerp(a.VisibilityMultiplier,b.VisibilityMultiplier,t)};
    }
    public SceneryWeatherConditions Evaluate(double distance, int seed)
    {
        if (settings == null || !Config.enabled) return new SceneryWeatherConditions {Kind = SceneryWeatherKind.Clear, VisibilityMultiplier = 1};
        if (ManualWeather) return State(ManualKind, Mathf.Max(Config.intensityRange.x,Config.intensityRange.y));
        double span = Math.Max(80,Config.sectionLength), after = distance - Math.Max(0,Config.initialWeatherDistance);
        long section = after < 0 ? -1 : (long)Math.Floor(after / span);
        var next = Procedural(section,seed);
        if (section >= 0)
        {
            float t = Mathf.SmoothStep(0,1,(float)((after - section * span) / Math.Max(1,Math.Min(Config.transitionDistance,span))));
            next = Blend(Procedural(section-1,seed),next,t);
        }
        if (Config.overrides != null) foreach (var interval in Config.overrides)
        {
            if (interval == null || distance < interval.startDistance || distance > interval.startDistance + Math.Max(1,interval.length)) continue;
            double edge = Math.Min(distance-interval.startDistance, interval.startDistance+interval.length-distance);
            float t = Mathf.SmoothStep(0,1,(float)(edge/Math.Max(1,Math.Min(Config.transitionDistance,interval.length*.5))));
            next = Blend(next,State(interval.kind,interval.intensity),t); break;
        }
        return next;
    }
    public void SetManualWeather(SceneryWeatherKind kind) { ManualWeather = true; ManualKind = kind; ApplyDistance(landscape.DistanceTravelled); }
    public void ResumeProceduralWeather() { ManualWeather = false; ApplyDistance(landscape.DistanceTravelled); }
    public void ApplyDistance(double distance)
    {
        if (settings == null || landscape == null) return;
        Conditions = Evaluate(distance,landscape.ActiveSeed);
        float shelter = landscape.IsInsideTunnel ? Mathf.Clamp01(Config.tunnelPrecipitation) : 1;
        float speed = simulation != null ? simulation.Speed : landscape.fallbackSpeed;
        foreach (var e in emitters)
        {
            var emission = e.system.emission; emission.rateOverTime = (e.snow ? Config.snowParticlesPerSecond * Conditions.SnowAmount : Config.rainParticlesPerSecond * Conditions.RainAmount) * shelter / 3;
            var velocity = e.system.velocityOverLifetime; velocity.x = Config.wind.x; velocity.y = -(e.snow ? Mathf.Max(.1f,Config.snowFallSpeed) : Mathf.Max(1,Config.rainFallSpeed)); velocity.z = Config.wind.y - speed;
            if (shelter < .01f || !Config.enabled) e.system.Clear();
        }
    }
    public void SetLighting(Color ambient, IReadOnlyList<Light> beams)
    {
        SceneryLighting.Apply(snowMaterial,ambient,beams,beamPositions,beamDirections,beamColors);
        SceneryLighting.Apply(rainMaterial,ambient,beams,beamPositions,beamDirections,beamColors);
    }
    void LateUpdate()
    {
        if (landscape == null || settings == null) return;
        if (observedSeed != landscape.ActiveSeed)
        {
            observedSeed = landscape.ActiveSeed;
            foreach (var e in emitters) e.system.Clear();
        }
        ApplyDistance(landscape.DistanceTravelled);
        Bounds dryBounds = InteriorBounds();
        Transform frame = cabin != null ? cabin : transform;
        foreach (var e in emitters)
        {
            int count = e.system.GetParticles(e.buffer);
            for (int i = 0; i < count; i++)
            {
                var p = e.buffer[i]; Vector3 world = e.system.transform.TransformPoint(p.position);
                Vector3 local = frame.InverseTransformPoint(world);
                if (OutsideParticleEnvelope(world, local, dryBounds))
                    p.remainingLifetime = -1;
                if (simulation != null && p.remainingLifetime > 0)
                {
                    float yaw = simulation.TurnRate * Mathf.Deg2Rad;
                    // Position response to the rotating train frame, in addition to forward streaming.
                    Vector3 cabinDisplacement = new Vector3(-yaw * local.z, 0, yaw * local.x) * Time.deltaTime;
                    p.position += e.system.transform.InverseTransformVector(frame.TransformVector(cabinDisplacement));
                    world = e.system.transform.TransformPoint(p.position);
                    local = frame.InverseTransformPoint(world);
                    // A turn can push a particle through a window during this very
                    // frame, so test its updated position before it is rendered.
                    if (OutsideParticleEnvelope(world, local, dryBounds)) p.remainingLifetime = -1;
                }
                e.buffer[i] = p;
            }
            e.system.SetParticles(e.buffer,count);
        }
    }
    bool OutsideParticleEnvelope(Vector3 world, Vector3 local, Bounds dryBounds)
    {
        Vector3 fromCentre = local - dryBounds.center;
        return dryBounds.Contains(local) || landscape.transform.InverseTransformPoint(world).y < -.25f ||
            Mathf.Abs(fromCentre.x) > dryBounds.extents.x + 30f || Mathf.Abs(fromCentre.z) > dryBounds.extents.z + 30f;
    }
    void OnDestroy() { if (snowMaterial != null) Destroy(snowMaterial); if (rainMaterial != null) Destroy(rainMaterial); }
}
