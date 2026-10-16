using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LandscapeSettings))]
public class LandscapeSettingsEditor : Editor
{
    bool generation = true, biomes = true, features, weather, geometry, budgets, legacy;
    void Fields(params string[] names)
    {
        foreach (string name in names)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("Use biome layers for each population category. Drop your models onto a layer and adjust density, rarity and placement independently. The same route drives scenery and train inertia.", MessageType.Info);
        generation = EditorGUILayout.BeginFoldoutHeaderGroup(generation, "Journey seed and shared route");
        if (generation) Fields("randomizeSeedOnStart", "seed", "route");
        EditorGUILayout.EndFoldoutHeaderGroup();
        biomes = EditorGUILayout.BeginFoldoutHeaderGroup(biomes, "Population and biomes");
        if (biomes)
        {
            Fields("populationDensity", "foliageWindStrength", "biomes", "biomeLength", "biomeBlendDistance");
            if (GUILayout.Button("Create and assign a biome...")) CreateBiome();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        weather = EditorGUILayout.BeginFoldoutHeaderGroup(weather, "Snow, rain and clear weather");
        if (weather) Fields("weather");
        EditorGUILayout.EndFoldoutHeaderGroup();
        features = EditorGUILayout.BeginFoldoutHeaderGroup(features, "Tunnels, lakes and landmarks");
        if (features) Fields("features", "featureLookAhead", "maxFeaturesPerChunk");
        EditorGUILayout.EndFoldoutHeaderGroup();
        geometry = EditorGUILayout.BeginFoldoutHeaderGroup(geometry, "Terrain, streaming and fog");
        if (geometry) Fields("chunkCount", "chunkLength", "halfWidth", "sideResolution", "lengthResolution", "hillHeight", "hillFrequency", "trackClearance", "fog", "fogStart", "fogEnd", "generatedShader");
        EditorGUILayout.EndFoldoutHeaderGroup();
        budgets = EditorGUILayout.BeginFoldoutHeaderGroup(budgets, "Population performance limits");
        if (budgets)
        {
            Fields("maxCopiesPerPrefabPerChunk", "maxCustomPrefabInstancesPerChunk", "maxBuiltInVerticesPerChunk", "maxPlacementAttemptsPerChunk");
            EditorGUILayout.HelpBox("Limits apply per streamed section. Earlier layers have priority when a limit is reached. Increasing prefab choices can increase prewarm memory even when a model is rare.", MessageType.None);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        legacy = EditorGUILayout.BeginFoldoutHeaderGroup(legacy, "Fallback visuals (when no biomes are assigned)");
        if (legacy) Fields("treesPerChunk", "treeHeight", "treePrefab", "houseChance", "meadow", "distantGrass", "foliage", "bark");
        EditorGUILayout.EndFoldoutHeaderGroup();
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        if (GUILayout.Button("Open scenery documentation")) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<Object>("Assets/Scenery/README.md"));
    }
    void CreateBiome()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create scenery biome", "NewBiome", "asset", "Choose where to store this reusable biome.");
        if (string.IsNullOrEmpty(path)) return;
        serializedObject.ApplyModifiedProperties();
        SceneryBiome biome = CreateInstance<SceneryBiome>();
        ScenerySpawnLayer trees = new ScenerySpawnLayer { name = "Trees", id = System.Guid.NewGuid().ToString("N"), normalizeHeight = true };
        trees.prefabs.Add(new SceneryPrefabChoice { builtin = SceneryBuiltinVisual.Pine });
        biome.layers.Add(trees); AssetDatabase.CreateAsset(biome, path);
        Undo.RecordObject(target, "Assign scenery biome");
        ((LandscapeSettings)target).biomes.Add(new SceneryBiomeChoice { biome = biome });
        EditorUtility.SetDirty(target); AssetDatabase.SaveAssets(); serializedObject.Update(); EditorGUIUtility.PingObject(biome);
    }
}

[CustomEditor(typeof(SceneryBiome))]
public class SceneryBiomeEditor : Editor
{
    int tab, selectedLayer;
    static readonly string[] tabs = { "Population", "Terrain and palette" };
    void Fields(params string[] names)
    {
        foreach (string name in names)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        tab = GUILayout.Toolbar(tab, tabs);
        EditorGUILayout.Space();
        if (tab == 1)
        {
            Fields("hillHeightMultiplier", "hillFrequencyMultiplier", "microterrainHeight", "microterrainScale", "meadow", "distantGrass", "foliage", "bark", "snowCover", "snowColor");
            EditorGUILayout.HelpBox("Built-in shapes use this palette and snow cover. Imported models keep their own materials, including their snow and wind settings.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Density controls how much appears. Prefab weights control which model appears: weights 9 and 1 mean approximately 90% and 10%. Expand a layer for placement, size and prefab choices.", MessageType.Info);
            Fields("densityMultiplier", "variation", "layers");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add a population layer", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Trees")) AddLayer("Trees", SceneryBuiltinVisual.Pine);
            if (GUILayout.Button("Bushes")) AddLayer("Bushes", SceneryBuiltinVisual.Bush);
            if (GUILayout.Button("Rocks")) AddLayer("Rocks", SceneryBuiltinVisual.Rock);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Grass")) AddLayer("Grass", SceneryBuiltinVisual.Grass);
            if (GUILayout.Button("Leaves")) AddLayer("Leaf litter", SceneryBuiltinVisual.FallenLeaves);
            if (GUILayout.Button("Branches")) AddLayer("Fallen branches", SceneryBuiltinVisual.Branch);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ferns")) AddLayer("Ferns", SceneryBuiltinVisual.Fern);
            if (GUILayout.Button("Dead trees")) AddLayer("Dead trees", SceneryBuiltinVisual.DeadTree);
            if (GUILayout.Button("Broadleaf trees")) AddLayer("Broadleaf trees", SceneryBuiltinVisual.BroadleafTree);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Add forest floor set (grass, leaves, branches, ferns)"))
            {
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Add forest floor layers");
                AddLayer("Grass", SceneryBuiltinVisual.Grass);
                AddLayer("Leaf litter", SceneryBuiltinVisual.FallenLeaves);
                AddLayer("Fallen branches", SceneryBuiltinVisual.Branch);
                AddLayer("Ferns", SceneryBuiltinVisual.Fern);
                Undo.CollapseUndoOperations(group);
            }
            DrawSelectedPrefabImport();
            serializedObject.Update();
        }
        serializedObject.ApplyModifiedProperties();
    }
    void DrawSelectedPrefabImport()
    {
        SceneryBiome biome = (SceneryBiome)target;
        if (biome.layers.Count == 0) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Import your models", EditorStyles.boldLabel);
        string[] names = new string[biome.layers.Count];
        for (int i = 0; i < names.Length; i++) names[i] = biome.layers[i] != null ? biome.layers[i].name : "Empty layer";
        selectedLayer = EditorGUILayout.Popup("Destination layer", Mathf.Clamp(selectedLayer, 0, names.Length - 1), names);
        List<GameObject> prefabs = SceneryPrefabImport.FindPrefabs(Selection.objects);
        using (new EditorGUI.DisabledScope(prefabs.Count == 0 || biome.layers[selectedLayer] == null))
        {
            if (GUILayout.Button("Add selected Project prefabs (" + prefabs.Count + ")"))
            {
                Undo.RecordObject(target, "Add scenery prefab choices");
                ScenerySpawnLayer layer = biome.layers[selectedLayer];
                foreach (GameObject prefab in prefabs)
                {
                    if (layer.prefabs.Exists(choice => choice != null && choice.prefab == prefab)) continue;
                    layer.prefabs.Add(new SceneryPrefabChoice { prefab = prefab });
                }
                EditorUtility.SetDirty(target);
            }
        }
        EditorGUILayout.HelpBox("You can also drag multiple prefab assets onto a layer's drop area. Existing model entries are skipped. Scene objects are not imported.", MessageType.None);
    }
    void AddLayer(string layerName, SceneryBuiltinVisual visual)
    {
        Undo.RecordObject(target, "Add scenery layer");
        ScenerySpawnLayer layer = new ScenerySpawnLayer { name = layerName, id = System.Guid.NewGuid().ToString("N") };
        bool tree = visual == SceneryBuiltinVisual.Pine || visual == SceneryBuiltinVisual.DeadTree || visual == SceneryBuiltinVisual.BroadleafTree;
        bool floor = visual == SceneryBuiltinVisual.Grass || visual == SceneryBuiltinVisual.FallenLeaves || visual == SceneryBuiltinVisual.Fern;
        layer.normalizeHeight = tree;
        layer.attemptsPerChunk = tree ? 72 : floor ? 160 : 48;
        layer.distanceFromTrack = new Vector2(7f, tree ? 110f : 38f);
        layer.alignToSlope = !tree;
        layer.placement = SceneryPlacementMode.Clusters;
        layer.clusterRadius = tree ? 9f : 3f;
        layer.scale = tree ? Vector2.one : new Vector2(.7f, 1.4f);
        layer.prefabs.Add(new SceneryPrefabChoice { builtin = visual });
        ((SceneryBiome)target).layers.Add(layer);
        EditorUtility.SetDirty(target);
        serializedObject.Update();
        SerializedProperty layers = serializedObject.FindProperty("layers");
        layers.isExpanded = true;
        layers.GetArrayElementAtIndex(layers.arraySize - 1).isExpanded = true;
        selectedLayer = layers.arraySize - 1;
    }
}

static class SceneryPrefabImport
{
    public static List<GameObject> FindPrefabs(Object[] objects)
    {
        List<GameObject> result = new List<GameObject>();
        foreach (Object item in objects)
        {
            if (!(item is GameObject) || !EditorUtility.IsPersistent(item) || !PrefabUtility.IsPartOfPrefabAsset(item)) continue;
            string path = AssetDatabase.GetAssetPath(item);
            if (string.IsNullOrEmpty(path)) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && !result.Contains(prefab)) result.Add(prefab);
        }
        return result;
    }
}

[CustomPropertyDrawer(typeof(ScenerySpawnLayer))]
public class ScenerySpawnLayerDrawer : PropertyDrawer
{
    const float Gap = 3f;
    static float Row => EditorGUIUtility.singleLineHeight;
    static readonly string[] population = { "attemptsPerChunk", "spawnChance", "densityMultiplier" };
    static readonly string[] placement = { "distanceFromTrack", "side", "placement", "clusterSize", "clusterRadius", "minSpacing", "noiseScale", "noiseThreshold", "distanceDensityCurve", "slopeRange", "heightRange", "alignToSlope", "maxSlope", "surfaceOffset" };
    static readonly string[] size = { "scale", "normalizeHeight", "height", "yawRange", "tiltDegrees" };
    static bool Visible(SerializedProperty property, string name)
    {
        if (name == "height") return property.FindPropertyRelative("normalizeHeight").boolValue;
        if (name == "clusterSize" || name == "clusterRadius")
        {
            SerializedProperty mode = property.FindPropertyRelative("placement");
            return mode != null && mode.enumValueIndex != 0;
        }
        return true;
    }
    static float FieldsHeight(SerializedProperty property, string[] names)
    {
        float height = 0f;
        foreach (string name in names)
        {
            SerializedProperty field = property.FindPropertyRelative(name);
            if (field != null && Visible(property, name)) height += EditorGUI.GetPropertyHeight(field, true) + Gap;
        }
        return height;
    }
    static bool HasDuplicateId(SerializedProperty property)
    {
        SerializedProperty id = property.FindPropertyRelative("id");
        if (id == null || string.IsNullOrEmpty(id.stringValue)) return false;
        SerializedProperty layers = property.serializedObject.FindProperty("layers");
        if (layers == null) return false;
        int copies = 0;
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty other = layers.GetArrayElementAtIndex(i).FindPropertyRelative("id");
            if (other != null && other.stringValue == id.stringValue) copies++;
        }
        return copies > 1;
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = Row + Gap;
        if (!property.isExpanded) return height;
        height += FieldsHeight(property, population) + (Row + Gap) * 2f;
        if (property.FindPropertyRelative("distanceFromTrack").isExpanded) height += FieldsHeight(property, placement);
        if (property.FindPropertyRelative("scale").isExpanded) height += FieldsHeight(property, size);
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("prefabs"), true) + Gap + Row * 1.7f + Gap;
        if (HasDuplicateId(property)) height += Row + Gap;
        return height;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect row = new Rect(position.x, position.y, position.width, Row);
        property.isExpanded = EditorGUI.Foldout(new Rect(row.x, row.y, 12f, row.height), property.isExpanded, GUIContent.none, true);
        EditorGUI.PropertyField(new Rect(row.x + 14f, row.y, 18f, row.height), property.FindPropertyRelative("enabled"), GUIContent.none);
        EditorGUI.PropertyField(new Rect(row.x + 35f, row.y, Mathf.Max(40f, row.width - 118f), row.height), property.FindPropertyRelative("name"), GUIContent.none);
        SerializedProperty choices = property.FindPropertyRelative("prefabs");
        EditorGUI.LabelField(new Rect(row.xMax - 78f, row.y, 78f, row.height), choices.arraySize + " choices", EditorStyles.miniLabel);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            row.y += Row + Gap;
            DrawFields(ref row, property, population);
            DrawGroup(ref row, property, "distanceFromTrack", "Placement and distribution", placement);
            DrawGroup(ref row, property, "scale", "Size and rotation", size);
            row.height = EditorGUI.GetPropertyHeight(choices, true);
            EditorGUI.PropertyField(row, choices, new GUIContent("Weighted prefab choices"), true);
            row.y += row.height + Gap;
            row.height = Row * 1.7f;
            DrawDropZone(EditorGUI.IndentedRect(row), choices);
            if (HasDuplicateId(property))
            {
                row.y += row.height + Gap;
                row.height = Row;
                if (GUI.Button(EditorGUI.IndentedRect(row), "Duplicated variation ID: create unique layout"))
                    property.FindPropertyRelative("id").stringValue = System.Guid.NewGuid().ToString("N");
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
    static void DrawFields(ref Rect row, SerializedProperty property, string[] names)
    {
        foreach (string name in names)
        {
            SerializedProperty field = property.FindPropertyRelative(name);
            if (field == null || !Visible(property, name)) continue;
            row.height = EditorGUI.GetPropertyHeight(field, true);
            EditorGUI.PropertyField(row, field, true);
            row.y += row.height + Gap;
        }
        row.height = Row;
    }
    static void DrawGroup(ref Rect row, SerializedProperty property, string stateField, string label, string[] names)
    {
        SerializedProperty state = property.FindPropertyRelative(stateField);
        state.isExpanded = EditorGUI.Foldout(row, state.isExpanded, label, true);
        row.y += Row + Gap;
        if (state.isExpanded) DrawFields(ref row, property, names);
    }
    static void DrawDropZone(Rect rect, SerializedProperty choices)
    {
        GUI.Box(rect, "Drop prefab assets here", EditorStyles.helpBox);
        Event current = Event.current;
        if (!rect.Contains(current.mousePosition) || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)) return;
        List<GameObject> prefabs = SceneryPrefabImport.FindPrefabs(DragAndDrop.objectReferences);
        if (prefabs.Count == 0) return;
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (current.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (GameObject prefab in prefabs)
            {
                bool exists = false;
                for (int i = 0; i < choices.arraySize; i++) if (choices.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == prefab) exists = true;
                if (exists) continue;
                int index = choices.arraySize;
                choices.InsertArrayElementAtIndex(index);
                SerializedProperty choice = choices.GetArrayElementAtIndex(index);
                choice.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                choice.FindPropertyRelative("weight").floatValue = 1f;
                choice.FindPropertyRelative("builtin").enumValueIndex = 0;
                choice.FindPropertyRelative("scaleMultiplier").floatValue = 1f;
                choice.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
                SerializedProperty tint = choice.FindPropertyRelative("tint");
                if (tint != null) tint.colorValue = Color.white;
                choice.isExpanded = false;
            }
            choices.isExpanded = true;
        }
        current.Use();
    }
}

[CustomPropertyDrawer(typeof(SceneryPrefabChoice))]
public class SceneryPrefabChoiceDrawer : PropertyDrawer
{
    const float Gap = 3f;
    static float Row => EditorGUIUtility.singleLineHeight;
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = (Row + Gap) * 2f;
        if (!property.isExpanded) return height;
        if (property.FindPropertyRelative("prefab").objectReferenceValue == null) height += Row + Gap;
        height += Row + Gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("rotationOffset"), true) + Gap;
        if (property.FindPropertyRelative("prefab").objectReferenceValue == null && property.FindPropertyRelative("tint") != null) height += Row + Gap;
        return height;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect row = new Rect(position.x, position.y, position.width, Row);
        SerializedProperty prefab = property.FindPropertyRelative("prefab");
        SerializedProperty builtin = property.FindPropertyRelative("builtin");
        string name = prefab.objectReferenceValue != null ? prefab.objectReferenceValue.name : builtin.enumDisplayNames[builtin.enumValueIndex];
        string percent = RelativePercentage(property).ToString("0.#") + "%";
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, name + "  (" + percent + ")", true);
        row.y += Row + Gap;
        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 48f;
        float split = row.width * .65f;
        EditorGUI.PropertyField(new Rect(row.x, row.y, split - 4f, row.height), prefab, new GUIContent("Model"));
        EditorGUI.PropertyField(new Rect(row.x + split, row.y, row.width - split, row.height), property.FindPropertyRelative("weight"), new GUIContent("Weight"));
        EditorGUIUtility.labelWidth = labelWidth;
        if (property.isExpanded)
        {
            row.y += Row + Gap;
            if (prefab.objectReferenceValue == null) Draw(ref row, builtin);
            Draw(ref row, property.FindPropertyRelative("scaleMultiplier"));
            Draw(ref row, property.FindPropertyRelative("rotationOffset"));
            if (prefab.objectReferenceValue == null) Draw(ref row, property.FindPropertyRelative("tint"));
        }
        EditorGUI.EndProperty();
    }
    static void Draw(ref Rect row, SerializedProperty field)
    {
        if (field == null) return;
        row.height = EditorGUI.GetPropertyHeight(field, true);
        EditorGUI.PropertyField(row, field, true);
        row.y += row.height + Gap;
    }
    static float RelativePercentage(SerializedProperty property)
    {
        int end = property.propertyPath.LastIndexOf(".Array.data[", System.StringComparison.Ordinal);
        if (end < 0) return 0f;
        SerializedProperty list = property.serializedObject.FindProperty(property.propertyPath.Substring(0, end));
        float total = 0f;
        for (int i = 0; i < list.arraySize; i++) total += Mathf.Max(0f, list.GetArrayElementAtIndex(i).FindPropertyRelative("weight").floatValue);
        return total > 0f ? 100f * Mathf.Max(0f, property.FindPropertyRelative("weight").floatValue) / total : 0f;
    }
}

[CustomEditor(typeof(RollingLandscape))]
public class RollingLandscapeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        RollingLandscape landscape = (RollingLandscape)target;
        if (!Application.isPlaying || !landscape.IsReady) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime seed", landscape.ActiveSeed.ToString());
        EditorGUILayout.LabelField("Distance", landscape.DistanceTravelled.ToString("F1") + " m");
        EditorGUILayout.LabelField("Current biome", landscape.CurrentBiome != null ? landscape.CurrentBiome.name : "Fallback");
        EditorGUILayout.LabelField("Visible population", landscape.PopulationCount.ToString("N0"));
        EditorGUILayout.LabelField("Built-in vegetation vertices", landscape.PopulationVertexCount.ToString("N0"));
        if (landscape.PopulationBudgetLimited) EditorGUILayout.HelpBox("A streamed section reached a population limit. Reduce an earlier layer's density or increase the relevant section budget.", MessageType.Warning);
        if (landscape.Route != null) EditorGUILayout.LabelField("Effective bend strength", landscape.Route.EffectiveTurnStrength.ToString("F2"));
        if (GUILayout.Button("Apply population / rebuild with the same seed")) landscape.Rebuild();
        if (GUILayout.Button("Generate a new journey")) landscape.GenerateNewJourney();
        if (GUILayout.Button("Copy runtime seed")) EditorGUIUtility.systemCopyBuffer = landscape.ActiveSeed.ToString();
        EditorGUILayout.HelpBox("Biome assets remain shared while playing. Duplicate assets before experimenting if you want to preserve their saved settings.", MessageType.None);
    }
    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
