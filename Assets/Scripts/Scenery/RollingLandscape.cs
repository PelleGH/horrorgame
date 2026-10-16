using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[AddComponentMenu("Train/Rolling Landscape")]
[DisallowMultipleComponent]
public class RollingLandscape : MonoBehaviour, IStationaryTrainPath
{
    public LandscapeSettings settings;
    [Tooltip("Supplies speed and stops. The landscape's shared route supplies this simulation's turning and grade while bound.")]
    public StationaryTrainSimulation speedSource;
    [Min(0f)] public float fallbackSpeed = 18f;
    public bool moving = true;
    public double DistanceTravelled { get; private set; }
    public int RecycledChunks { get; private set; }
    public int ActiveChunkCount => chunks.Count;
    public int TreeSlotCount { get; private set; }
    public bool IsReady { get; private set; }
    public int ActiveSeed { get; private set; }
    public SceneryRoute Route { get; private set; }
    public SceneryBiome CurrentBiome => BiomeAt(DistanceTravelled);
    public int ActiveFeatureCount => visibleFeatures.Count;
    public bool IsInsideTunnel
    {
        get
        {
            foreach (var feature in visibleFeatures)
                if (feature.feature.kind == SceneryFeatureKind.Tunnel && DistanceTravelled >= feature.start - 7 && DistanceTravelled <= feature.end + 7) return true;
            return false;
        }
    }
    readonly Vector4[] beamPositions = new Vector4[SceneryLighting.MaximumBeams], beamDirections = new Vector4[SceneryLighting.MaximumBeams], beamColors = new Vector4[SceneryLighting.MaximumBeams];
    public void SetSceneLighting(Color ambient, IReadOnlyList<Light> beams)
    {
        SceneryLighting.Apply(generatedMaterial, ambient, beams, beamPositions, beamDirections, beamColors);
        if (generatedMaterial != null) generatedMaterial.SetFloat("_FoliageWindStrength", settings.foliageWindStrength);
    }

    int MaximumPrefabInstancesPerChunk => Mathf.Clamp(settings.maxCustomPrefabInstancesPerChunk, 64, 2048);
    int MaximumPlacementAttemptsPerChunk => Mathf.Clamp(settings.maxPlacementAttemptsPerChunk, 256, 16384);
    int MaximumBuiltInVertices => Mathf.Clamp(settings.maxBuiltInVerticesPerChunk, 20000, 1000000);
    public int PopulationCount { get { int total = 0; foreach (var c in chunks) total += c.populationCount; return total; } }
    public int PopulationVertexCount { get { int total = 0; foreach (var c in chunks) total += c.vegetationBuffer.VertexCount; return total; } }
    public bool PopulationBudgetLimited { get { foreach (var c in chunks) if (c.populationLimited) return true; return false; } }
    public int GetBuiltinCount(SceneryBuiltinVisual kind) { int total = 0; int index = (int)kind; foreach (var c in chunks) if (index >= 0 && index < c.builtinCounts.Length) total += c.builtinCounts[index]; return total; }
    int LayerAttempts(SceneryBiome biome, ScenerySpawnLayer layer) => Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0,layer.attemptsPerChunk) * Mathf.Max(0,settings.populationDensity) * Mathf.Max(0,biome.densityMultiplier) * Mathf.Max(0,layer.densityMultiplier)),0,8192);
    static int StableHash(string text)
    {
        unchecked { int hash =  unchecked((int)2166136261); foreach (char c in text ?? "") hash = (hash ^ c) * 16777619; return hash; }
    }
    readonly List<Chunk> chunks = new List<Chunk>();
    readonly List<SceneryFeatureOccurrence> visibleFeatures = new List<SceneryFeatureOccurrence>(64);
    readonly List<SceneryBiome> biomeRegistry = new List<SceneryBiome>();
    readonly List<SceneryFeature> runtimeFeatureRegistry = new List<SceneryFeature>();
    readonly Dictionary<long, SceneryBiome> biomeCache = new Dictionary<long, SceneryBiome>();
    SceneryFeaturePlanner planner;
    LandscapeSettings plannerSettings;
    int plannerSeed;
    Material generatedMaterial;
    Transform inactivePool;
    LandscapeSettings ownedSettings;
    StationaryTrainSimulation boundSimulation;
    IStationaryTrainPath previousPath;
    double lastSimulationDistance, elapsedTime;
    bool seedChosen;
    bool poolBudgetWarned;
    long featureWindow = long.MinValue;
    int featureRevision = -1;

    sealed class Chunk
    {
        public Transform root;
        public Mesh terrain, vegetation, railway, features;
        public readonly MeshBuffer vegetationBuffer = new MeshBuffer(22000);
        public readonly MeshBuffer railwayBuffer = new MeshBuffer(6000);
        public readonly MeshBuffer featureBuffer = new MeshBuffer(12000);
        public Vector3[] vertices, normals;
        public Color[] colors;
        public int[] triangles;
        public long index = long.MinValue;
        public readonly Dictionary<SceneryPrefabChoice, VisualPool> prefabPools = new Dictionary<SceneryPrefabChoice, VisualPool>();
        public readonly Dictionary<SceneryFeature, VisualPool> featurePools = new Dictionary<SceneryFeature, VisualPool>();
        public VisualPool legacyTrees;
        public int instances, populationCount;
        public bool populationLimited;
        public readonly int[] builtinCounts = new int[10];
    }

    sealed class Visual
    {
        public Transform transform;
        public Vector3 scale;
        public Quaternion rotation;
        public Vector3 size;
        public Bounds bounds;
    }

    sealed class VisualPool
    {
        public readonly List<Visual> items = new List<Visual>();
        public int used;
        public Visual Take()
        {
            if (used >= items.Count) return null;
            Visual item = items[used++];
            item.transform.gameObject.SetActive(true);
            return item;
        }
        public void Reset()
        {
            used = 0;
            for (int i = 0; i < items.Count; i++) items[i].transform.gameObject.SetActive(false);
        }
    }

    void Start() { if (!IsReady) Rebuild(); }
    void OnEnable() { if (IsReady) BindSimulation(); }
    void OnDisable() { UnbindSimulation(); }

    public void Initialize(LandscapeSettings source, StationaryTrainSimulation simulation)
    {
        UnbindSimulation();
        settings = source;
        speedSource = simulation;
        seedChosen = false;
        planner = null;
        runtimeFeatureRegistry.Clear();
        DistanceTravelled = 0d;
        elapsedTime = 0d;
        Rebuild();
    }

    public void UseSeed(int seed)
    {
        ActiveSeed = seed;
        seedChosen = true;
        ResetJourney();
        Rebuild();
    }

    public void GenerateNewJourney() => UseSeed(NewSeed());

    void ResetJourney()
    {
        DistanceTravelled = 0d;
        elapsedTime = 0d;
        planner = null;
        runtimeFeatureRegistry.Clear();
        if (speedSource != null) speedSource.ResetJourney();
        lastSimulationDistance = speedSource != null ? speedSource.ContinuousTravelDistance : 0d;
    }

    static int NewSeed() => unchecked(Guid.NewGuid().GetHashCode() ^ Environment.TickCount);

    [ContextMenu("Rebuild Landscape")]
    public void Rebuild()
    {
        if (!Application.isPlaying) return;
        IsReady = false;
        if (settings == null)
        {
            if (ownedSettings == null) ownedSettings = ScriptableObject.CreateInstance<LandscapeSettings>();
            settings = ownedSettings;
        }
        if (!seedChosen)
        {
            ActiveSeed = settings.randomizeSeedOnStart ? NewSeed() : settings.seed;
            seedChosen = true;
        }
        ClearChunks();
        poolBudgetWarned = false;
        Shader shader = settings.generatedShader != null ? settings.generatedShader : Resources.Load<Shader>("SceneryGround") ?? Shader.Find("Train/Scenery Vertex Color") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (generatedMaterial == null)
        {
            generatedMaterial = new Material(shader) { name = "Scenery Generated Surfaces", enableInstancing = true };
            if (generatedMaterial.HasProperty("_Smoothness")) generatedMaterial.SetFloat("_Smoothness", 0.1f);
            if (generatedMaterial.HasProperty("_Glossiness")) generatedMaterial.SetFloat("_Glossiness", 0.1f);
        }
        else if (generatedMaterial.shader != shader) generatedMaterial.shader = shader;
        if (inactivePool == null)
        {
            inactivePool = new GameObject("Inactive Prefab Staging").transform;
            inactivePool.SetParent(transform, false);
            inactivePool.gameObject.SetActive(false);
        }
        Route = new SceneryRoute(settings.route ?? new SceneryRouteSettings(), ActiveSeed, Mathf.Max(60f, settings.halfWidth) * 1.2f);
        if (planner == null || plannerSettings != settings || plannerSeed != ActiveSeed)
        {
            planner = new SceneryFeaturePlanner(settings, ActiveSeed, BiomeAt);
            plannerSettings = settings;
            plannerSeed = ActiveSeed;
        }
        biomeRegistry.Clear(); biomeCache.Clear();
        if (settings.biomes != null)
            foreach (SceneryBiomeChoice choice in settings.biomes)
                if (choice != null && choice.biome != null && choice.weight > 0f && !biomeRegistry.Contains(choice.biome)) biomeRegistry.Add(choice.biome);
        TreeSlotCount = 0; RecycledChunks = 0;
        featureWindow = long.MinValue;
        featureRevision = -1;
        int count = Mathf.Clamp(settings.chunkCount, 5, 15);
        for (int i = 0; i < count; i++) chunks.Add(CreateChunk(i));
        BindSimulation();
        if (speedSource != null) speedSource.RefreshMotionPath();
        lastSimulationDistance = speedSource != null ? speedSource.ContinuousTravelDistance : 0d;
        RefreshFeatures();
        RefreshChunks(true);
        IsReady = true;
    }

    void BindSimulation()
    {
        if (boundSimulation == speedSource && boundSimulation != null && ReferenceEquals(boundSimulation.MotionPath, this)) return;
        UnbindSimulation();
        if (speedSource == null) return;
        boundSimulation = speedSource;
        previousPath = boundSimulation.MotionPath;
        boundSimulation.MotionPath = this;
        lastSimulationDistance = boundSimulation.ContinuousTravelDistance;
    }

    void UnbindSimulation()
    {
        if (boundSimulation != null && ReferenceEquals(boundSimulation.MotionPath, this)) boundSimulation.MotionPath = previousPath;
        boundSimulation = null; previousPath = null;
    }

    public void SampleMotion(double continuousDistance, float speed, out float turnDegreesPerSecond, out float uphillDegrees)
    {
        double distance = DistanceTravelled + (moving ? Math.Max(0d, continuousDistance - lastSimulationDistance) : 0d);
        turnDegreesPerSecond = moving && Route != null ? Route.Curvature(distance) * speed : 0f;
        uphillDegrees = Route != null ? Route.Grade(distance) : 0f;
    }

    void LateUpdate()
    {
        if (!IsReady) return;
        if (boundSimulation != speedSource || (speedSource != null && !ReferenceEquals(speedSource.MotionPath, this))) BindSimulation();
        double step;
        if (speedSource != null)
        {
            double current = speedSource.ContinuousTravelDistance;
            step = Math.Max(0d, current - lastSimulationDistance);
            lastSimulationDistance = current;
            elapsedTime = speedSource.ElapsedTime;
        }
        else { step = Mathf.Max(0f, fallbackSpeed) * Time.deltaTime; elapsedTime += Time.deltaTime; }
        int revision = planner.Revision;
        planner.TriggerTimeRules(elapsedTime, DistanceTravelled, settings.featureLookAhead);
        if (moving && step > 0d) DistanceTravelled += step;
        RefreshFeatures();
        RefreshChunks(revision != planner.Revision);
    }

    public void AdvanceDistance(double metres)
    {
        if (!IsReady || double.IsNaN(metres) || double.IsInfinity(metres)) return;
        DistanceTravelled = Math.Max(0d, DistanceTravelled + metres);
        if (speedSource != null) { lastSimulationDistance = speedSource.ContinuousTravelDistance; speedSource.RefreshMotionPath(); }
        RefreshFeatures(); RefreshChunks(false);
    }

    public void RequestFeature(SceneryFeature feature, float leadDistance = 180f, float featureLength = 100f,
        ScenerySide side = ScenerySide.Both, float offset = 0f)
    {
        if (!IsReady || feature == null) return;
        if (feature.prefab != null)
        {
            if (!runtimeFeatureRegistry.Contains(feature))
            {
                if (runtimeFeatureRegistry.Count >= SceneryFeaturePlanner.MaxQueuedOccurrences)
                {
                    Debug.LogWarning("The runtime scenery feature registry is full. Start a new journey before requesting additional unique feature models.", this);
                    return;
                }
                runtimeFeatureRegistry.Add(feature);
            }
            foreach (Chunk chunk in chunks)
            {
                if (chunk.featurePools.ContainsKey(feature)) continue;
                Transform owner = chunk.root.Find("Custom Models");
                VisualPool pool = PreparePool(chunk, feature.prefab, owner != null ? owner : chunk.root, Mathf.Clamp(settings.maxFeaturesPerChunk, 1, 32), feature.rotationOffset);
                chunk.featurePools[feature] = pool;
                if (pool.items.Count == 0) Debug.LogWarning("The scenery prefab budget is full; the requested custom feature cannot be displayed. Reduce custom choices or increase their distribution across biomes.", this);
            }
        }
        planner.QueueFeature(feature, DistanceTravelled + Mathf.Max(30f, leadDistance), featureLength, side, offset);
        RefreshFeatures(); RefreshChunks(true);
    }

    float Length => Mathf.Max(30f, settings.chunkLength);

    void RefreshFeatures()
    {
        long centre = (long)Math.Floor(DistanceTravelled / Length);
        if (featureWindow == centre && featureRevision == planner.Revision) return;
        featureWindow = centre;
        featureRevision = planner.Revision;
        double first = (centre - chunks.Count / 2) * Length;
        planner.Collect(first - 2d, first + chunks.Count * Length + 2d, visibleFeatures);
    }

    void RefreshChunks(bool force)
    {
        long centre = (long)Math.Floor(DistanceTravelled / Length);
        long first = centre - chunks.Count / 2;
        Quaternion inverseCurrent = Quaternion.Inverse(Route.Rotation(DistanceTravelled));
        for (int i = 0; i < chunks.Count; i++)
        {
            long index = first + i;
            int slot = (int)((index % chunks.Count + chunks.Count) % chunks.Count);
            Chunk chunk = chunks[slot];
            if (force || chunk.index != index)
            {
                if (chunk.index != long.MinValue && chunk.index != index) RecycledChunks++;
                chunk.index = index;
                Generate(chunk);
            }
            double start = index * (double)Length;
            chunk.root.localPosition = Route.PositionRelative(start, DistanceTravelled);
            chunk.root.localRotation = inverseCurrent * Route.Rotation(start);
        }
    }

    Chunk CreateChunk(int slot)
    {
        var chunk = new Chunk();
        chunk.root = new GameObject("Landscape Section " + slot).transform;
        chunk.root.SetParent(transform, false);
        chunk.terrain = AddMesh(chunk.root, "Terrain");
        Transform vegetation = Group("Vegetation", chunk.root);
        chunk.vegetation = AddMesh(Group("Built-in Batched", vegetation), "Vegetation");
        chunk.railway = AddMesh(Group("Railway", chunk.root), "Railway");
        chunk.features = AddMesh(Group("Features", chunk.root), "Features");
        int across = Mathf.Clamp(settings.sideResolution, 12, 64) * 2 + 7;
        int along = Mathf.Clamp(settings.lengthResolution, 8, 48) + 1;
        chunk.vertices = new Vector3[across * along]; chunk.normals = new Vector3[across * along];
        chunk.colors = new Color[across * along]; chunk.triangles = new int[(across - 1) * (along - 1) * 6];
        int t = 0;
        for (int z = 0; z < along - 1; z++)
            for (int x = 0; x < across - 1; x++)
            {
                int a = z * across + x;
                chunk.triangles[t++] = a; chunk.triangles[t++] = a + across; chunk.triangles[t++] = a + 1;
                chunk.triangles[t++] = a + 1; chunk.triangles[t++] = a + across; chunk.triangles[t++] = a + across + 1;
            }
        foreach (SceneryBiome biome in biomeRegistry)
        {
            Transform biomeGroup = Group(biome.name, vegetation);
            if (biome.layers == null) continue;
            foreach (ScenerySpawnLayer layer in biome.layers)
            {
                if (layer == null || layer.prefabs == null) continue;
                if (layer.enabled) TreeSlotCount += LayerAttempts(biome,layer);
                Transform layerGroup = Group(layer.name, biomeGroup);
                foreach (SceneryPrefabChoice choice in layer.prefabs)
                {
                    if (choice == null || choice.prefab == null || choice.weight <= 0f || !layer.enabled || layer.spawnChance <= 0f || chunk.prefabPools.ContainsKey(choice)) continue;
                    int copies = Mathf.Min(LayerAttempts(biome,layer), Mathf.Clamp(settings.maxCopiesPerPrefabPerChunk, 1, 512));
                    chunk.prefabPools[choice] = PreparePool(chunk, choice.prefab, layerGroup, copies, choice.rotationOffset);
                }
            }
        }
        if (biomeRegistry.Count == 0)
        {
            int count = Mathf.Clamp(settings.treesPerChunk, 0, 160);
            TreeSlotCount += count;
            if (settings.treePrefab != null) chunk.legacyTrees = PreparePool(chunk, settings.treePrefab, vegetation, Mathf.Min(count, settings.maxCopiesPerPrefabPerChunk));
        }
        Transform featureGroup = Group("Custom Models", chunk.root);
        if (settings.features != null)
            foreach (SceneryFeatureRule rule in settings.features)
                if (rule != null && rule.feature != null && rule.feature.prefab != null && !chunk.featurePools.ContainsKey(rule.feature))
                    chunk.featurePools[rule.feature] = PreparePool(chunk, rule.feature.prefab, featureGroup, Mathf.Clamp(settings.maxFeaturesPerChunk, 1, 32), rule.feature.rotationOffset);
        foreach (SceneryFeature feature in runtimeFeatureRegistry)
            if (feature != null && feature.prefab != null && !chunk.featurePools.ContainsKey(feature))
                chunk.featurePools[feature] = PreparePool(chunk, feature.prefab, featureGroup, Mathf.Clamp(settings.maxFeaturesPerChunk, 1, 32), feature.rotationOffset);
        return chunk;
    }

    static Transform Group(string name, Transform parent)
    {
        Transform group = new GameObject(string.IsNullOrEmpty(name) ? "Layer" : name).transform;
        group.SetParent(parent, false); return group;
    }

    Mesh AddMesh(Transform owner, string name)
    {
        var mesh = new Mesh { name = "Scenery " + name, indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic(); owner.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        owner.gameObject.AddComponent<MeshRenderer>().sharedMaterial = generatedMaterial;
        return mesh;
    }

    VisualPool PreparePool(Chunk chunk, GameObject prefab, Transform owner, int requested, Vector3 rotationOffset = default)
    {
        var pool = new VisualPool();
        int count = Mathf.Min(Mathf.Max(0, requested), Mathf.Max(0, MaximumPrefabInstancesPerChunk - chunk.instances));
        if (count < requested && !poolBudgetWarned)
        {
            Debug.LogWarning("A scenery section reached Max Custom Prefab Instances Per Chunk. Reduce prewarm copies/choices or raise the budget. Later entries remain empty until capacity is available.", this);
            poolBudgetWarned = true;
        }
        for (int i = 0; i < count; i++)
        {
            Transform wrapper = new GameObject(prefab.name + " (Scenery)").transform;
            wrapper.SetParent(inactivePool, false);
            wrapper.gameObject.SetActive(false);
            GameObject instance = Instantiate(prefab, wrapper, false);
            instance.transform.localRotation = Quaternion.Euler(rotationOffset) * instance.transform.localRotation;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (AudioSource audio in instance.GetComponentsInChildren<AudioSource>(true)) { audio.playOnAwake = false; audio.enabled = false; }
            foreach (MonoBehaviour script in instance.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            { var main = particles.main; main.playOnAwake = false; particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            instance.SetActive(true);
            var visual = new Visual { transform = wrapper, scale = Vector3.one, rotation = Quaternion.identity };
            visual.bounds = MeasureLocalBounds(wrapper);
            Vector3 size = visual.bounds.size;
            visual.size = new Vector3(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y), Mathf.Max(0.001f, size.z));
            wrapper.SetParent(owner, false);
            pool.items.Add(visual); chunk.instances++;
        }
        return pool;
    }

    static Bounds MeasureLocalBounds(Transform root)
    {
        bool found = false; Bounds bounds = default;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
            Bounds b = renderer.localBounds;
            Matrix4x4 matrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                if (found) bounds.Encapsulate(p); else { bounds = new Bounds(p, Vector3.zero); found = true; }
            }
        }
        return found ? bounds : new Bounds(Vector3.up * 0.5f, Vector3.one);
    }

    void Generate(Chunk chunk)
    {
        foreach (VisualPool pool in chunk.prefabPools.Values) pool.Reset();
        foreach (VisualPool pool in chunk.featurePools.Values) pool.Reset();
        chunk.legacyTrees?.Reset();
        chunk.vegetationBuffer.Clear(); chunk.railwayBuffer.Clear(); chunk.featureBuffer.Clear();
        chunk.populationCount = 0; chunk.populationLimited = false; Array.Clear(chunk.builtinCounts,0,chunk.builtinCounts.Length);
        double start = chunk.index * (double)Length;
        GenerateTerrain(chunk, start);
        GenerateRailway(chunk, start);
        GenerateVegetation(chunk, start);
        GenerateFeatures(chunk, start);
        chunk.vegetationBuffer.Apply(chunk.vegetation);
        chunk.railwayBuffer.Apply(chunk.railway);
        chunk.featureBuffer.Apply(chunk.features);
    }

    void GenerateTerrain(Chunk chunk, double start)
    {
        int side = Mathf.Clamp(settings.sideResolution, 12, 64), across = side * 2 + 7;
        int steps = Mathf.Clamp(settings.lengthResolution, 8, 48);
        float width = Mathf.Max(60f, settings.halfWidth), clearance = Mathf.Max(3f, settings.trackClearance);
        for (int row = 0; row <= steps; row++)
        {
            double distance = start + row * (double)Length / steps;
            for (int column = 0; column < across; column++)
            {
                float x = column < side ? -Mathf.Lerp(clearance, width, Mathf.Pow((side-column)/(float)side,1.7f)) :
                    column < side + 7 ? Mathf.Lerp(-clearance, clearance, (column - side) / 6f) :
                    Mathf.Lerp(clearance, width, Mathf.Pow((column - side - 6) / (float)side,1.7f));
                int index = row * across + column;
                chunk.vertices[index] = Point(x, SampleHeight(x, distance), distance, start);
                Vector3 dx = Point(x + 0.5f, SampleHeight(x + 0.5f, distance), distance, start) - Point(x - 0.5f, SampleHeight(x - 0.5f, distance), distance, start);
                Vector3 dz = Point(x, SampleHeight(x, distance + 0.5d), distance + 0.5d, start) - Point(x, SampleHeight(x, distance - 0.5d), distance - 0.5d, start);
                chunk.normals[index] = Vector3.Cross(dz, dx).normalized;
                chunk.colors[index] = Mathf.Abs(x) < 2.3f ? new Color(0.22f, 0.23f, 0.21f) : TerrainColor(x, distance);
            }
        }
        chunk.terrain.vertices = chunk.vertices; chunk.terrain.normals = chunk.normals;
        chunk.terrain.colors = chunk.colors; chunk.terrain.triangles = chunk.triangles; chunk.terrain.RecalculateBounds();
    }

    Vector3 Point(float x, float height, double distance, double origin) => Route.PointRelative(x, height, distance, origin);
    Quaternion LocalRotation(double distance, double origin) => Quaternion.Inverse(Route.Rotation(origin)) * Route.Rotation(distance);

    public float SampleHeight(float x, double absoluteZ)
    {
        float height = BaseHeight(x, absoluteZ);
        for (int i = 0; i < visibleFeatures.Count; i++)
        {
            SceneryFeatureOccurrence occurrence = visibleFeatures[i];
            if (occurrence.feature.kind != SceneryFeatureKind.Lake) continue;
            float q = LakeRadiusSquared(occurrence, x, absoluteZ);
            if (q >= 1.3f || Mathf.Abs(x) < Mathf.Max(3f, settings.trackClearance)) continue;
            float water = WaterHeight(occurrence, absoluteZ);
            float bed = water - Mathf.Max(0f, occurrence.feature.waterDepth) * Mathf.Clamp01(1f - q);
            height = Mathf.Lerp(height, Mathf.Min(height, bed), 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1.3f, q)));
        }
        return height;
    }

    float BaseHeight(float x, double distance)
    {
        BlendAt(distance, out SceneryBiome a, out SceneryBiome b, out float blend);
        float amplitude = Mathf.Lerp(a != null ? a.hillHeightMultiplier : 1f, b != null ? b.hillHeightMultiplier : 1f, blend);
        float frequency = settings.hillFrequency;
        float fa = frequency * (a != null ? a.hillFrequencyMultiplier : 1f);
        float fb = frequency * (b != null ? b.hillFrequencyMultiplier : 1f);
        float h = Mathf.Lerp(NoiseHeight(x, distance, fa), NoiseHeight(x, distance, fb), blend);
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Mathf.Max(3f, settings.trackClearance), 90f, Mathf.Abs(x)));
        float microHeight = Mathf.Lerp(a != null ? a.microterrainHeight : 0, b != null ? b.microterrainHeight : 0, blend);
        float microScale = Mathf.Lerp(a != null ? a.microterrainScale : .12f, b != null ? b.microterrainScale : .12f, blend);
        float micro = (Mathf.PerlinNoise(x * microScale + 317, (float)(distance * microScale) + (ActiveSeed & 1023)) - .5f) * microHeight;
        return -.22f + h * Mathf.Max(0f,settings.hillHeight) * amplitude * fade + micro * Mathf.SmoothStep(0,1,Mathf.InverseLerp(settings.trackClearance,settings.trackClearance+5,Mathf.Abs(x)));
    }

    float NoiseHeight(float x, double distance, float frequency)
    {
        float offset = (ActiveSeed & 65535) * 0.03125f;
        float z = (float)(distance * frequency);
        return (Mathf.PerlinNoise(x * frequency + offset, z + offset * 0.7f) - 0.28f) * 0.85f +
            (Mathf.PerlinNoise(x * frequency * 2.3f - offset, z * 2.3f + 40f) - 0.5f) * 0.23f;
    }

    Color TerrainColor(float x, double distance)
    {
        BlendAt(distance, out SceneryBiome a, out SceneryBiome b, out float blend);
        Color ac = Mathf.Abs(x) > 90f ? a != null ? a.distantGrass : settings.distantGrass : a != null ? a.meadow : settings.meadow;
        Color bc = Mathf.Abs(x) > 90f ? b != null ? b.distantGrass : settings.distantGrass : b != null ? b.meadow : settings.meadow;
        return Color.Lerp(ac, bc, blend);
    }

    SceneryBiome BiomeAt(double distance)
    {
        if (biomeRegistry.Count == 0) return null;
        long zone = (long)Math.Floor(distance / Mathf.Max(80f, settings.biomeLength));
        if (biomeCache.TryGetValue(zone, out SceneryBiome cached)) return cached;
        float sum = 0f;
        foreach (SceneryBiomeChoice choice in settings.biomes) if (choice != null && choice.biome != null) sum += Mathf.Max(0f, choice.weight);
        double pick = Unit(Hash(ActiveSeed, zone, 171)) * sum;
        SceneryBiome selected = biomeRegistry[0];
        foreach (SceneryBiomeChoice choice in settings.biomes)
        {
            if (choice == null || choice.biome == null || choice.weight <= 0f) continue;
            pick -= choice.weight;
            if (pick <= 0d) { selected = choice.biome; break; }
        }
        if (biomeCache.Count > 64) biomeCache.Clear();
        biomeCache[zone] = selected; return selected;
    }

    void BlendAt(double distance, out SceneryBiome a, out SceneryBiome b, out float blend)
    {
        double length = Mathf.Max(80f, settings.biomeLength);
        long zone = (long)Math.Floor(distance / length);
        double local = distance - zone * length;
        float width = Mathf.Clamp(settings.biomeBlendDistance, 0f, (float)length * 0.5f);
        b = BiomeAt(distance);
        if (width <= 0f || local >= width) { a = b; blend = 1f; return; }
        a = BiomeAt((zone - 1) * length + 0.5d);
        blend = Mathf.SmoothStep(0f, 1f, (float)(local / width));
    }

    void GenerateVegetation(Chunk chunk, double start)
    {
        if (biomeRegistry.Count == 0) { GenerateLegacyTrees(chunk,start,new System.Random(unchecked((int)Hash(ActiveSeed,chunk.index,919)))); return; }
        int attempts = 0;
        foreach (var biome in biomeRegistry)
        {
            if (biome.layers == null) continue;
            foreach (var layer in biome.layers)
            {
                if (layer == null || !layer.enabled || layer.prefabs == null) continue;
                int key = StableHash(biome.name + "/" + (string.IsNullOrEmpty(layer.id) ? layer.name : layer.id));
                var random = new System.Random(unchecked((int)Hash(ActiveSeed,chunk.index,key)));
                var occupied = new HashSet<long>();
                int count = LayerAttempts(biome,layer);
                float min = Mathf.Max(Mathf.Max(3,settings.trackClearance),Mathf.Min(layer.distanceFromTrack.x,layer.distanceFromTrack.y));
                float max = Mathf.Min(Mathf.Max(60,settings.halfWidth)*.96f,Mathf.Max(min,Mathf.Max(layer.distanceFromTrack.x,layer.distanceFromTrack.y)));
                if (max < min) continue;
                int groupSize = Mathf.Clamp(Mathf.RoundToInt(RandomRange(random,layer.clusterSize)),1,64);
                float clusterX = 0; double clusterZ = 0;
                for (int i = 0; i < count; i++)
                {
                    if (++attempts > MaximumPlacementAttemptsPerChunk) { chunk.populationLimited = true; break; }
                    int side = layer.side == ScenerySide.Left ? -1 : layer.side == ScenerySide.Right ? 1 : random.Next(2)==0 ? -1 : 1;
                    float x = Mathf.Lerp(min,max,(float)random.NextDouble()) * side;
                    double distance = start + random.NextDouble()*Length;
                    if (layer.placement == SceneryPlacementMode.Clusters)
                    {
                        if (i % groupSize == 0) { clusterX = x; clusterZ = distance; }
                        float radius = Mathf.Sqrt((float)random.NextDouble()) * Mathf.Max(.1f,layer.clusterRadius);
                        float angle = (float)random.NextDouble()*Mathf.PI*2;
                        x = clusterX + Mathf.Cos(angle)*radius; distance = clusterZ + Mathf.Sin(angle)*radius;
                    }
                    if (layer.minSpacing > .001f)
                    {
                        double cell = layer.minSpacing / .6;
                        long cx = (long)Math.Floor(x/cell), cz = (long)Math.Floor(distance/cell);
                        uint h = Hash(key,cz,unchecked((int)cx));
                        x = (float)((cx + .3 + Unit(h)*.4)*cell);
                        distance = (cz + .3 + Unit(Hash(key,cz,unchecked((int)cx+91)))*.4)*cell;
                        long cellKey = unchecked(cx * 73856093L ^ cz * 19349663L);
                        if (!occupied.Add(cellKey)) continue;
                    }
                    if (distance < start || distance >= start+Length || Mathf.Abs(x) < min || Mathf.Abs(x) > max || (layer.side==ScenerySide.Left && x>=0) || (layer.side==ScenerySide.Right && x<=0)) continue;
                    BlendAt(distance,out var previous,out var next,out float blend);
                    if ((random.NextDouble()<blend ? next : previous) != biome) continue;
                    float lateral = Mathf.InverseLerp(min,max,Mathf.Abs(x));
                    float density = layer.distanceDensityCurve != null ? Mathf.Max(0,layer.distanceDensityCurve.Evaluate(lateral)) : 1;
                    if (random.NextDouble() >= Mathf.Clamp01(layer.spawnChance*density)) continue;
                    if (layer.noiseThreshold > 0 && Mathf.PerlinNoise(x*Mathf.Max(.001f,layer.noiseScale)+(key&1023),(float)(distance*Mathf.Max(.001f,layer.noiseScale))) < layer.noiseThreshold) continue;
                    if (Excluded(x,distance)) continue;
                    float terrainHeight = SampleHeight(x,distance);
                    if (terrainHeight < Mathf.Min(layer.heightRange.x,layer.heightRange.y) || terrainHeight > Mathf.Max(layer.heightRange.x,layer.heightRange.y)) continue;
                    Vector3 normal = TerrainNormal(x,distance); float slope = Vector3.Angle(Vector3.up,normal);
                    float slopeMaximum = string.IsNullOrEmpty(layer.id) && layer.slopeRange == new Vector2(0,70) ? layer.maxSlope : Mathf.Max(layer.slopeRange.x,layer.slopeRange.y);
                    if (slope < Mathf.Min(layer.slopeRange.x,layer.slopeRange.y) || slope > slopeMaximum) continue;
                    var choice = PickChoice(layer.prefabs,random); if (choice == null) continue;
                    float scale = Mathf.Max(.001f,RandomRange(random,layer.scale)) * Mathf.Max(.0001f,choice.scaleMultiplier);
                    if (layer.normalizeHeight) scale *= Mathf.Max(.01f,RandomRange(random,layer.height));
                    float tilt = RandomRange(random,layer.tiltDegrees);
                    Quaternion rotation = LocalRotation(distance,start) * (layer.alignToSlope ? Quaternion.FromToRotation(Vector3.up,normal) : Quaternion.identity)
                        * Quaternion.Euler(0,RandomRange(random,layer.yawRange),0) * Quaternion.Euler(tilt,0,tilt*((float)random.NextDouble()*2-1));
                    Vector3 position = Point(x,terrainHeight+layer.surfaceOffset,distance,start);
                    if (choice.prefab != null)
                    {
                        if (!chunk.prefabPools.TryGetValue(choice,out var pool)) continue;
                        var visual = pool.Take(); if (visual == null) { chunk.populationLimited = true; continue; }
                        if (layer.normalizeHeight) scale /= visual.size.y;
                        Place(visual,position,rotation,visual.scale*scale);
                    }
                    else if (choice.builtin != SceneryBuiltinVisual.None)
                    {
                        if (chunk.vegetationBuffer.VertexCount + 1500 > MaximumBuiltInVertices) { chunk.populationLimited = true; continue; }
                        if (!layer.normalizeHeight) scale *= BuiltinSize(choice.builtin);
                        float variation = 1 + ((float)random.NextDouble()*2-1)*Mathf.Clamp(biome.variation,0,.5f);
                        int firstVertex = chunk.vegetationBuffer.VertexCount;
                        Quaternion builtinRotation = rotation*Quaternion.Euler(choice.rotationOffset);
                        AddBuiltin(chunk.vegetationBuffer,choice.builtin,position,builtinRotation,scale,biome,choice.tint*variation,unchecked(key+i*397),Mathf.Abs(x)<55);
                        if (layer.normalizeHeight) chunk.vegetationBuffer.NormalizeHeight(firstVertex,position,builtinRotation,scale);
                        chunk.builtinCounts[(int)choice.builtin]++;
                    }
                    else continue;
                    chunk.populationCount++;
                }
            }
        }
    }
    static float BuiltinSize(SceneryBuiltinVisual kind)
    {
        switch(kind)
        {
            case SceneryBuiltinVisual.Pine: return 12;
            case SceneryBuiltinVisual.BroadleafTree: return 10;
            case SceneryBuiltinVisual.DeadTree: return 9;
            case SceneryBuiltinVisual.Grass: return .65f;
            case SceneryBuiltinVisual.FallenLeaves: return .7f;
            case SceneryBuiltinVisual.Branch: return 2.2f;
            case SceneryBuiltinVisual.Fern: return .85f;
            default:return 1.4f;
        }
    }

    void GenerateLegacyTrees(Chunk chunk, double start, System.Random random)
    {
        int count = Mathf.Clamp(settings.treesPerChunk, 0, 160);
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(10f, Mathf.Min(130f, settings.halfWidth * 0.87f), Mathf.Pow((float)random.NextDouble(), 1.65f)) * (random.Next(2) == 0 ? -1 : 1);
            double distance = start + random.NextDouble() * Length;
            float height = RandomRange(random, settings.treeHeight);
            Quaternion rotation = LocalRotation(distance, start) * Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            if (Excluded(x, distance)) continue;
            Vector3 position = Point(x, SampleHeight(x, distance), distance, start);
            if (chunk.legacyTrees != null)
            {
                Visual visual = chunk.legacyTrees.Take(); if (visual == null) continue;
                Place(visual, position, rotation, visual.scale * (height / visual.size.y));
            }
            else AddBuiltin(chunk.vegetationBuffer, SceneryBuiltinVisual.Pine, position, rotation, height, null);
        }
    }

    static SceneryPrefabChoice PickChoice(List<SceneryPrefabChoice> choices, System.Random random)
    {
        double total = 0d;
        foreach (SceneryPrefabChoice choice in choices) if (choice != null) total += Mathf.Max(0f, choice.weight);
        if (total <= 0d) return null;
        double pick = random.NextDouble() * total;
        foreach (SceneryPrefabChoice choice in choices)
        {
            if (choice == null || choice.weight <= 0f) continue;
            pick -= choice.weight; if (pick <= 0d) return choice;
        }
        return null;
    }

    static float RandomRange(System.Random random, Vector2 range) => Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), (float)random.NextDouble());
    static void Place(Visual visual, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        visual.transform.localPosition = position;
        visual.transform.localRotation = rotation * visual.rotation;
        visual.transform.localScale = scale;
    }

    Vector3 TerrainNormal(float x, double distance) => new Vector3(SampleHeight(x - 0.5f, distance) - SampleHeight(x + 0.5f, distance), 1f,
        SampleHeight(x, distance - 0.5d) - SampleHeight(x, distance + 0.5d)).normalized;

    bool Excluded(float x, double distance)
    {
        foreach (SceneryFeatureOccurrence occurrence in visibleFeatures)
        {
            if (occurrence.feature.kind == SceneryFeatureKind.Lake && LakeRadiusSquared(occurrence, x, distance) < 1.3f) return true;
            if (occurrence.feature.kind == SceneryFeatureKind.Tunnel && distance >= occurrence.start - 3d && distance <= occurrence.end + 3d && Mathf.Abs(x) < occurrence.width * 0.5f + 4f) return true;
            if (occurrence.feature.kind == SceneryFeatureKind.Landmark && distance >= occurrence.start && distance <= occurrence.end && Mathf.Abs(x - occurrence.sideSign * occurrence.offsetFromTrack) < occurrence.width * 0.6f) return true;
        }
        return false;
    }

    void AddBuiltin(MeshBuffer mesh, SceneryBuiltinVisual builtin, Vector3 position, Quaternion rotation, float height, SceneryBiome biome,
        Color tint = default, int variant = 0, bool detailed = true)
    {
        if (tint == default) tint = Color.white;
        Color foliage = (biome != null ? biome.foliage : settings.foliage) * tint;
        Color bark = (biome != null ? biome.bark : settings.bark) * tint;
        float snow = biome != null ? Mathf.Clamp01(biome.snowCover) : 0;
        Color snowColor = (biome != null ? biome.snowColor : Color.white) * tint;
        float R(int salt) => (float)Unit(Hash(variant,salt,419));
        Vector3 P(float x,float y,float z) => position + rotation * new Vector3(x,y,z) * height;
        void Limb(Vector3 a, Vector3 b,float radius,Color color) => mesh.Cylinder(a,b,radius,color,5);
        if (builtin == SceneryBuiltinVisual.Pine)
        {
            Limb(P(0,0,0),P(.012f,1,0),height*.012f,bark);
            int tiers = detailed ? 6 : 5;
            for (int tier=0;tier<tiers;tier++)
            {
                float t=tier/(float)(tiers-1), y=Mathf.Lerp(.30f,.94f,t);
                float radius=Mathf.Lerp(.125f,.018f,t)*(1+(R(tier+71)-.5f)*.25f);
                int sprays=detailed?5:3;
                for(int i=0;i<sprays;i++)
                {
                    float angle=(i/(float)sprays+tier*.19f+R(17)*.4f)*Mathf.PI*2;
                    float dx=Mathf.Cos(angle),dz=Mathf.Sin(angle);
                    Vector3 basePoint=P(dx*radius*.78f,y+(R(i+tier*5+83)-.5f)*.024f,dz*radius*.78f);
                    Vector3 tip=P(dx*radius,y-.025f,dz*radius);
                    if(detailed&&tier<4) Limb(P(0,y,0),tip,height*.003f,bark);
                    mesh.WindWeight=.04f+t*.13f;
                    mesh.SnowCanopy(basePoint,rotation*Quaternion.Euler((R(i+tier*7+99)-.5f)*16,90-angle*Mathf.Rad2Deg,0),
                        new Vector3(radius*.34f,.018f+radius*.17f,radius*.48f)*height,foliage*(.85f+R(tier*31+i)*.3f),snowColor,snow);
                    mesh.WindWeight=0;
                }
            }
        }
        else if (builtin == SceneryBuiltinVisual.BroadleafTree)
        {
            Color trunk=Color.Lerp(bark,new Color(.59f,.63f,.61f),.65f);
            Limb(P(0,0,0),P(.02f,.82f,0),height*.018f,trunk);
            for(int i=0;i<(detailed?7:4);i++)
            {
                float angle=i*2.39996f+R(47),radius=.13f+R(i+10)*.11f;
                Vector3 top=P(Mathf.Cos(angle)*radius,.67f+R(i+23)*.18f,Mathf.Sin(angle)*radius);
                Limb(P(0,.35f+R(i+1)*.25f,0),top,height*.008f,trunk);
                mesh.WindWeight=.14f;
                mesh.SnowCanopy(top,rotation,new Vector3(.14f,.14f,.13f)*height,foliage,snowColor,snow);
                mesh.WindWeight=0;
            }
        }
        else if (builtin == SceneryBuiltinVisual.DeadTree)
        {
            Color trunk=Color.Lerp(bark,new Color(.46f,.48f,.45f),.35f);
            Limb(P(0,0,0),P(.025f,1,0),height*.014f,trunk);
            for(int i=0;i<(detailed?9:5);i++)
            {
                float y=.3f+R(i+1)*.6f,a=i*2.4f+R(37),r=.11f+R(i+21)*.08f;
                Vector3 end=P(Mathf.Cos(a)*r,y+.08f,Mathf.Sin(a)*r);
                Limb(P(0,y,0),end,height*.0045f,trunk);
                if(detailed) Limb(end,end+rotation*new Vector3(.03f,.10f,.02f)*height,height*.0023f,trunk);
            }
        }
        else if (builtin == SceneryBuiltinVisual.Bush)
        {
            for(int i=0;i<(detailed?4:2);i++)
            {
                float a=i*2.4f;
                Vector3 top=P(Mathf.Cos(a)*.28f,.35f+R(i+4)*.22f,Mathf.Sin(a)*.28f);
                Limb(position,top,height*.022f,bark);
                mesh.SnowCanopy(top,rotation,new Vector3(.36f,.34f,.32f)*height,foliage,snowColor,snow);
            }
        }
        else if (builtin == SceneryBuiltinVisual.Rock)
        {
            mesh.SnowCanopy(P(0,.27f,0),rotation,new Vector3(.65f,.44f,.48f)*height,new Color(.33f,.36f,.34f)*tint,snowColor,snow);
        }
        else if (builtin == SceneryBuiltinVisual.Grass)
        {
            Color straw=Color.Lerp(new Color(.23f,.32f,.16f)*tint,new Color(.46f,.40f,.24f)*tint,snow*.65f);
            for(int i=0;i<5;i++)
            {
                float angle=i*2.39996f+R(11),h=height*(.45f+R(i+7)*.55f);
                Vector3 foot=position+rotation*new Vector3(Mathf.Cos(angle)*height*.13f,0,Mathf.Sin(angle)*height*.13f);
                Vector3 right=rotation*new Vector3(Mathf.Cos(angle)*height*.045f,0,Mathf.Sin(angle)*height*.045f);
                Vector3 top=foot+rotation*new Vector3(Mathf.Sin(angle)*h*.23f,h,Mathf.Cos(angle)*h*.23f);
                mesh.Blade(foot-right,top-right*.2f,top+right*.2f,foot+right,straw,Color.Lerp(straw,snowColor,snow*.45f));
            }
        }
        else if (builtin == SceneryBuiltinVisual.FallenLeaves)
        {
            for(int i=0;i<8;i++)
            {
                float a=R(i+13)*Mathf.PI*2,r=R(i+29)*.75f;
                Color leaf=Color.Lerp(new Color(.24f,.13f,.045f),new Color(.46f,.29f,.11f),R(i+43))*tint;
                Vector3 center=P(Mathf.Cos(a)*r,.015f,Mathf.Sin(a)*r);
                mesh.SnowCanopy(center,rotation*Quaternion.Euler(0,R(i+53)*360,0),new Vector3(.12f,.018f,.2f)*height,leaf,snowColor,snow*.3f);
            }
        }
        else if (builtin == SceneryBuiltinVisual.Branch)
        {
            Vector3 from=P(-.5f,.045f,0),to=P(.5f,.075f,.08f);
            Limb(from,to,height*.026f,bark);
            for(int i=0;i<3;i++)
            {
                Vector3 middle=Vector3.Lerp(from,to,.2f+i*.25f);
                Limb(middle,middle+rotation*new Vector3(.15f,.025f,(i%2==0?1:-1)*.26f)*height,height*.01f,bark*.85f);
            }
            if(snow>.01f) mesh.SnowCanopy(P(0,.08f,.02f),rotation,new Vector3(.45f,.03f,.045f)*height,bark,snowColor,snow);
        }
        else if (builtin == SceneryBuiltinVisual.Fern)
        {
            Color green=Color.Lerp(foliage,new Color(.22f,.32f,.13f)*tint,.55f);
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3,dx=Mathf.Cos(a),dz=Mathf.Sin(a);
                Vector3 top=P(dx*.55f,.35f,dz*.55f);
                Limb(position,top,height*.008f,green);
                for(int j=1;j<=3;j++)
                {
                    Vector3 center=Vector3.Lerp(position,top,j/4f)+rotation*Vector3.up*(height*.16f);
                    mesh.WindWeight=.45f;
                    mesh.SnowCanopy(center,rotation*Quaternion.Euler(12,-a*Mathf.Rad2Deg,0),new Vector3(.16f,.018f,.10f)*height,green,snowColor,snow*.45f);
                    mesh.WindWeight=0;
                }
            }
        }
    }

    void GenerateRailway(Chunk chunk, double start)
    {
        int railSegments = Mathf.CeilToInt(Length / 4f);
        Color iron = new Color(0.24f, 0.26f, 0.27f), wood = new Color(0.24f, 0.17f, 0.12f);
        for (int i = 0; i < railSegments; i++)
        {
            double distance = start + (i + 0.5d) * Length / railSegments;
            Quaternion rotation = LocalRotation(distance, start);
            for (int side = -1; side <= 1; side += 2)
                chunk.railwayBuffer.Box(Point(side * 0.78f, -0.09f, distance, start), new Vector3(0.09f, 0.15f, Length / railSegments + 0.025f), rotation, iron);
        }
        int sleepers = Mathf.CeilToInt(Length / 1.6f);
        for (int i = 0; i < sleepers; i++)
        {
            double distance = start + (i + 0.5d) * Length / sleepers;
            chunk.railwayBuffer.Box(Point(0f, -0.17f, distance, start), new Vector3(2.2f, 0.12f, 0.2f), LocalRotation(distance, start), wood);
        }
    }

    void GenerateFeatures(Chunk chunk, double start)
    {
        int count = 0;
        foreach (SceneryFeatureOccurrence occurrence in visibleFeatures)
        {
            if (occurrence.end < start || occurrence.start >= start + Length || count++ >= Mathf.Clamp(settings.maxFeaturesPerChunk, 1, 32)) continue;
            if (occurrence.feature.prefab != null)
            {
                double middle = (occurrence.start + occurrence.end) * 0.5d;
                if (middle < start || middle >= start + Length || !chunk.featurePools.TryGetValue(occurrence.feature, out VisualPool pool)) continue;
                Visual visual = pool.Take(); if (visual == null) continue;
                float x = occurrence.feature.kind == SceneryFeatureKind.Lake ? LakeCentre(occurrence) : occurrence.sideSign * occurrence.offsetFromTrack;
                Quaternion rotation = LocalRotation(middle, start);
                Vector3 desired = new Vector3(occurrence.width, occurrence.height, (float)(occurrence.end - occurrence.start));
                Vector3 scale = new Vector3(desired.x / visual.size.x, desired.y / visual.size.y, desired.z / visual.size.z);
                Vector3 pivot = new Vector3(visual.bounds.center.x, visual.bounds.min.y, visual.bounds.center.z);
                float ground = occurrence.feature.kind == SceneryFeatureKind.Lake ? WaterHeight(occurrence, middle) : SampleHeight(x, middle);
                Vector3 position = Point(x, ground, middle, start) - rotation * Vector3.Scale(pivot, scale);
                Place(visual, position, rotation, scale);
            }
            else if (occurrence.feature.kind == SceneryFeatureKind.Tunnel) BuildTunnel(chunk.featureBuffer, occurrence, start);
            else if (occurrence.feature.kind == SceneryFeatureKind.Lake) BuildLake(chunk.featureBuffer, occurrence, start);
            else BuildLandmark(chunk.featureBuffer, occurrence, start);
        }
    }

    void BuildTunnel(MeshBuffer mesh, SceneryFeatureOccurrence occurrence, double start)
    {
        double from = Math.Max(start, occurrence.start), to = Math.Min(start + Length, occurrence.end);
        int segments = Math.Max(1, (int)Math.Ceiling((to - from) / 5d));
        float half = Mathf.Max(2.8f, occurrence.width * 0.5f), height = Mathf.Max(4.5f, occurrence.height);
        for (int i = 0; i < segments; i++)
        {
            double distance = from + (i + 0.5d) * (to - from) / segments;
            float length = (float)((to - from) / segments) + 0.12f;
            Quaternion rotation = LocalRotation(distance, start);
            for (int side = -1; side <= 1; side += 2)
                mesh.Box(Point(side * (half + 0.45f), -0.22f + height * 0.5f, distance, start), new Vector3(0.9f, height, length), rotation, occurrence.feature.tint);
            mesh.Box(Point(0f, height - 0.22f + 0.45f, distance, start), new Vector3(half * 2f + 1.8f, 0.9f, length), rotation, occurrence.feature.tint);
        }
    }

    float LakeCentre(SceneryFeatureOccurrence occurrence)
    {
        float minimum = occurrence.width * 0.5f * Mathf.Sqrt(1.3f) + Mathf.Max(3f, settings.trackClearance);
        return (occurrence.sideSign < 0 ? -1f : 1f) * Mathf.Max(occurrence.offsetFromTrack, minimum);
    }

    float LakeRadiusSquared(SceneryFeatureOccurrence occurrence, float x, double distance)
    {
        float centre = LakeCentre(occurrence);
        float dx = (x - centre) / Mathf.Max(0.5f, occurrence.width * 0.5f);
        double dz = (distance - (occurrence.start + occurrence.end) * 0.5d) / Math.Max(0.5d, (occurrence.end - occurrence.start) * 0.5d);
        return dx * dx + (float)(dz * dz);
    }

    float WaterHeight(SceneryFeatureOccurrence occurrence, double distance)
    {
        double middle = (occurrence.start + occurrence.end) * 0.5d;
        float centre = LakeCentre(occurrence);
        double absolute = Route.Elevation(middle) + BaseHeight(centre, middle) * Math.Cos(Route.Grade(middle) * Mathf.Deg2Rad) - occurrence.feature.waterDepth * 0.35d;
        return (float)((absolute - Route.Elevation(distance)) / Math.Max(0.1d, Math.Cos(Route.Grade(distance) * Mathf.Deg2Rad)));
    }

    void BuildLake(MeshBuffer mesh, SceneryFeatureOccurrence occurrence, double start)
    {
        double from = Math.Max(start, occurrence.start), to = Math.Min(start + Length, occurrence.end);
        int segments = Math.Max(1, (int)Math.Ceiling((to - from) / 4d));
        double middle = (occurrence.start + occurrence.end) * 0.5d, halfLength = (occurrence.end - occurrence.start) * 0.5d;
        float centre = LakeCentre(occurrence);
        for (int i = 0; i < segments; i++)
        {
            double a = from + i * (to - from) / segments, b = from + (i + 1d) * (to - from) / segments;
            float wa = occurrence.width * 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - (float)Math.Pow((a - middle) / halfLength, 2d)));
            float wb = occurrence.width * 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - (float)Math.Pow((b - middle) / halfLength, 2d)));
            mesh.Quad(Point(centre - wa, WaterHeight(occurrence, a), a, start), Point(centre - wb, WaterHeight(occurrence, b), b, start),
                Point(centre + wb, WaterHeight(occurrence, b), b, start), Point(centre + wa, WaterHeight(occurrence, a), a, start), occurrence.feature.tint);
        }
    }

    void BuildLandmark(MeshBuffer mesh, SceneryFeatureOccurrence occurrence, double start)
    {
        double middle = (occurrence.start + occurrence.end) * 0.5d;
        if (middle < start || middle >= start + Length) return;
        float x = occurrence.sideSign * occurrence.offsetFromTrack;
        Quaternion rotation = LocalRotation(middle, start);
        mesh.Box(Point(x, SampleHeight(x, middle) + occurrence.height * 0.5f, middle, start),
            new Vector3(occurrence.width, occurrence.height, Mathf.Min(20f, (float)(occurrence.end - occurrence.start))), rotation, occurrence.feature.tint);
    }

    void ClearChunks()
    {
        foreach (Chunk chunk in chunks)
        {
            if (chunk.root != null) { chunk.root.gameObject.SetActive(false); Destroy(chunk.root.gameObject); }
            if (chunk.terrain != null) Destroy(chunk.terrain);
            if (chunk.vegetation != null) Destroy(chunk.vegetation);
            if (chunk.railway != null) Destroy(chunk.railway);
            if (chunk.features != null) Destroy(chunk.features);
        }
        chunks.Clear();
    }

    void OnDestroy()
    {
        UnbindSimulation(); ClearChunks();
        if (generatedMaterial != null) Destroy(generatedMaterial);
        if (ownedSettings != null) Destroy(ownedSettings);
    }

    static uint Hash(int seed, long index, int salt)
    {
        unchecked
        {
            uint value = (uint)seed ^ (uint)index * 0x9e3779b9u ^ (uint)(index >> 32) * 0x85ebca6bu ^ (uint)salt;
            value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu;
            return value ^ (value >> 16);
        }
    }
    static double Unit(uint value) => (value >> 8) / 16777216d;

    sealed class MeshBuffer
    {
        readonly List<Vector3> vertices, normals;
        readonly List<Color> colors;
        readonly List<Vector2> wind;
        public int VertexCount => vertices.Count;
        public float WindWeight;
        readonly List<int> triangles;
        static readonly Vector3[] BoxCorners = {
            new Vector3(-1,-1,-1),new Vector3(-1,1,-1),new Vector3(1,1,-1),new Vector3(1,-1,-1),
            new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1),new Vector3(-1,-1,1) };
        static readonly int[] BoxFaces = { 0,1,2,3, 4,5,6,7, 7,6,1,0, 3,2,5,4, 1,6,5,2, 7,0,3,4 };
        static readonly Vector3[] Octa = { Vector3.up,Vector3.down,Vector3.right,Vector3.forward,Vector3.left,Vector3.back };
        static readonly int[] OctaFaces = { 0,3,2,0,4,3,0,5,4,0,2,5,1,2,3,1,3,4,1,4,5,1,5,2 };
        public MeshBuffer(int capacity)
        {
            vertices = new List<Vector3>(capacity); normals = new List<Vector3>(capacity);
            colors = new List<Color>(capacity); wind = new List<Vector2>(capacity); triangles = new List<int>(capacity * 2);
        }
        public void Clear() { vertices.Clear(); normals.Clear(); colors.Clear(); wind.Clear(); triangles.Clear(); WindWeight=0; }
        void Vertex(Vector3 point, Vector3 normal, Color color) { vertices.Add(point); normals.Add(normal); colors.Add(color); wind.Add(new Vector2(WindWeight,0)); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int first = vertices.Count; Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            Vertex(a, normal, color); Vertex(b, normal, color); Vertex(c, normal, color); Vertex(d, normal, color);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }
        public void Box(Vector3 position, Vector3 size, Quaternion rotation, Color color)
        {
            Vector3 half = size * 0.5f;
            for (int face = 0; face < 6; face++)
            {
                int f = face * 4;
                Quad(position + rotation * Vector3.Scale(BoxCorners[BoxFaces[f]], half), position + rotation * Vector3.Scale(BoxCorners[BoxFaces[f + 1]], half),
                    position + rotation * Vector3.Scale(BoxCorners[BoxFaces[f + 2]], half), position + rotation * Vector3.Scale(BoxCorners[BoxFaces[f + 3]], half), color);
            }
        }
        public void Cone(Vector3 position, Quaternion rotation, float radius, float height, Color color)
        {
            Vector3 top = position + rotation * Vector3.up * height;
            for (int side = 0; side < 8; side++)
            {
                float a = side * Mathf.PI * 0.25f, b = (side + 1) * Mathf.PI * 0.25f;
                Vector3 left = position + rotation * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Vector3 right = position + rotation * new Vector3(Mathf.Cos(b) * radius, 0f, Mathf.Sin(b) * radius);
                Triangle(left, top, right, color);
                Triangle(position, left, right, color * 0.8f);
            }
        }
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int first = vertices.Count; Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            Vertex(a, normal, color); Vertex(b, normal, color); Vertex(c, normal, color);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
        }
        public void Octahedron(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            for (int face = 0; face < OctaFaces.Length; face += 3)
                Triangle(position + rotation * Vector3.Scale(Octa[OctaFaces[face]], scale), position + rotation * Vector3.Scale(Octa[OctaFaces[face + 1]], scale),
                    position + rotation * Vector3.Scale(Octa[OctaFaces[face + 2]], scale), color);
        }
        public void Cylinder(Vector3 from,Vector3 to,float radius,Color color,int sides=6)
        {
            Vector3 axis=to-from;if(axis.sqrMagnitude<.000001f)return;
            Quaternion rotation=Quaternion.LookRotation(axis.normalized);
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                Vector3 u=rotation*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius;
                Vector3 v=rotation*new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*radius;
                Quad(from+v,to+v*.55f,to+u*.55f,from+u,color*(.88f+.12f*(i%2)));
            }
        }
        public void NormalizeHeight(int first,Vector3 origin,Quaternion rotation,float desiredHeight)
        {
            Vector3 up=rotation*Vector3.up;float min=float.MaxValue,max=float.MinValue;
            for(int i=first;i<vertices.Count;i++){float y=Vector3.Dot(vertices[i]-origin,up);min=Mathf.Min(min,y);max=Mathf.Max(max,y);}
            float factor=desiredHeight/Mathf.Max(.0001f,max-min);
            for(int i=first;i<vertices.Count;i++)vertices[i]=origin+(vertices[i]-origin)*factor;
        }
        public void SnowCanopy(Vector3 position,Quaternion rotation,Vector3 scale,Color baseColor,Color snowColor,float snow)
        {
            for(int face=0;face<OctaFaces.Length;face+=3)
            {
                Color color=face<12?Color.Lerp(baseColor,snowColor,snow):baseColor*.82f;
                Triangle(position+rotation*Vector3.Scale(Octa[OctaFaces[face]],scale),position+rotation*Vector3.Scale(Octa[OctaFaces[face+1]],scale),
                    position+rotation*Vector3.Scale(Octa[OctaFaces[face+2]],scale),color);
            }
        }
        public void Blade(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color bottom,Color top)
        {
            Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
            for(int side=0;side<2;side++)
            {
                int first=vertices.Count;Vector3 n=side==0?normal:-normal;
                WindWeight=0;Vertex(a,n,bottom);WindWeight=1;Vertex(b,n,top);Vertex(c,n,top);WindWeight=0;Vertex(d,n,bottom);
                if(side==0){triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);triangles.Add(first);triangles.Add(first+2);triangles.Add(first+3);}
                else{triangles.Add(first);triangles.Add(first+2);triangles.Add(first+1);triangles.Add(first);triangles.Add(first+3);triangles.Add(first+2);}
            }
        }
        public void Apply(Mesh mesh)
        {
            mesh.Clear(false); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0,wind);
            mesh.SetTriangles(triangles, 0, true);
        }
    }
}
