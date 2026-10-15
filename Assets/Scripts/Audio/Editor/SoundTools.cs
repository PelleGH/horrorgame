using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorAudio.Editor
{
    public static class SoundTools
    {
        [MenuItem("GameObject/Horror Audio/World Emitter", false, 21)]
        public static void CreateWorldEmitter()
        {
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            GameObject root = new GameObject("World Sound");
            Undo.RegisterCreatedObjectUndo(root, "Create world emitter");
            AddSoundWindow.Configure(root, null, SoundBehaviour.World, SoundStarter.KeepCurrent);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = root;
        }
        public static DoorAudio ConfigureDoor(Door door, SoundEmitter emitter)
        {
            var player = door.GetComponent<SoundSequencePlayer>() ?? Undo.AddComponent<SoundSequencePlayer>(door.gameObject);
            var audio = door.GetComponent<DoorAudio>() ?? Undo.AddComponent<DoorAudio>(door.gameObject);
            Undo.RecordObject(player, "Connect door sound"); Undo.RecordObject(audio, "Configure door sound");
            player.emitter = emitter; audio.player = player;
            Undo.RecordObject(emitter, "Set door priority");
            if (emitter.priority < 0) emitter.priority = 96;
            AddSoundWindow.Save(emitter);
            SetDefault(audio.opening, "DoorOpen", emitter.HasDirectSound);
            SetDefault(audio.closing, "DoorClose", emitter.HasDirectSound);
            SetDefault(audio.latched, "Latch", false);
            AddSoundWindow.Save(player); AddSoundWindow.Save(audio);
            return audio;
        }
        static void SetDefault(SoundEvent sound, string profile, bool useEmitter)
        {
            if (sound.sequence != null || sound.clip != null || sound.profile != null || sound.useEmitterSound) return;
            sound.useEmitterSound = useEmitter;
            if (!useEmitter) sound.profile = Profile(profile);
        }
        public static GameSoundProfile Profile(string name) => AssetDatabase.LoadAssetAtPath<GameSoundProfile>("Assets/Audio/Profiles/" + name + ".asset");
        [MenuItem("Tools/Horror Audio/Set Up Selected Objects")]
        public static void SetUpSelected()
        {
            foreach (GameObject selected in Selection.gameObjects)
            {
                SoundEmitter emitter = selected.GetComponent<SoundEmitter>();
                if (emitter == null) emitter = Undo.AddComponent<SoundEmitter>(selected);
                Undo.RecordObject(emitter, "Configure audio");
                var player = selected.GetComponent<SimpleFPSControllerNewInput>();
                if (player != null)
                {
                    var footsteps = selected.GetComponent<PlayerFootstepAudio>() ?? Undo.AddComponent<PlayerFootstepAudio>(selected);
                    Undo.RecordObject(footsteps, "Configure footsteps");
                    footsteps.soundEmitter = emitter;
                    if (emitter.priority < 0) emitter.priority = 80;
                    var source = footsteps.footstepSource != null ? footsteps.footstepSource : footsteps.GetComponent<AudioSource>();
                    if (source != null) Undo.RecordObject(source, "Import footstep source");
                    footsteps.ConnectExistingFootsteps();
                    if (source != null) AddSoundWindow.Save(source);
                    if (footsteps.footstepProfile == null && !emitter.HasDirectSound) footsteps.footstepProfile = Profile("Footsteps");
                    AddSoundWindow.Save(footsteps);
                }
                Door door = selected.GetComponent<Door>();
                if (door != null)
                {
                    ConfigureDoor(door, emitter);
                }
                if (player == null && door == null)
                {
                    if (emitter.defaultProfile == null) emitter.defaultProfile = Profile("Impact");
                    if (selected.GetComponent<Rigidbody>() != null)
                    {
                        SoundImpact impact = selected.GetComponent<SoundImpact>();
                        if (impact == null) impact = Undo.AddComponent<SoundImpact>(selected);
                        Undo.RecordObject(impact, "Configure impact sound");
                        if (impact.impactProfile == null) impact.impactProfile = Profile("Impact");
                        EditorUtility.SetDirty(impact);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(impact);
                    }
                }
                EditorUtility.SetDirty(emitter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);
                if (player != null) EditorUtility.SetDirty(player);
                if (door != null) EditorUtility.SetDirty(door);
                if (player != null) PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                if (door != null) PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            }
        }
        [MenuItem("Tools/Horror Audio/Open Audio Test Scene")]
        public static void OpenTest()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Scenes/AudioTest.unity");
        }
    }

    [CustomEditor(typeof(SoundEmitter))]
    public class SoundEmitterEditor : UnityEditor.Editor
    {
        bool layers, effects, space, train, advanced;
        SoundStarter starter;
        void Field(string name, string label = null)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, label == null ? null : new GUIContent(label), true);
        }
        public override void OnInspectorGUI()
        {
            SoundEmitter emitter = (SoundEmitter)target;
            WorldSoundEmitter worldController = emitter.GetComponent<WorldSoundEmitter>();
            bool managedWorld = worldController != null && worldController.enabled;
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true)) Field("m_Script");
            EditorGUILayout.LabelField("Audio & Playback", EditorStyles.boldLabel);
            Field("audioClip", "Your Audio Clip"); Field("defaultProfile", "Reusable Profile (fallback)");
            if (managedWorld) EditorGUILayout.HelpBox("World Sound Emitter owns automatic looping, global/3D mode and fades. Configure those on its component.", MessageType.Info);
            else Field("playOnEnable");
            Field("volume", "Object Volume"); Field("pitch", "Object Pitch");
            if (emitter.HasDirectSound)
            { Field("soundSettings.volume", "Clip Gain"); if (!managedWorld) Field("soundSettings.loop"); Field("soundSettings.pitchVariation"); }
            layers = EditorGUILayout.Foldout(layers, "Variations & Layers", true);
            if (layers) { Field("clipVariations"); Field("extraLayers"); }
            effects = EditorGUILayout.Foldout(effects, "Tone & Effects", true);
            if (effects && emitter.HasDirectSound)
                foreach (string name in new[] {"bassDb", "trebleDb", "distortion", "lowPass", "highPass", "reverb", "reverbLevel", "echoWet", "echoDelay", "echoDecay"}) Field("soundSettings." + name);
            else if (effects) EditorGUILayout.HelpBox("A reusable profile owns its tone and effects. Select that profile to edit them, or assign your own Audio Clip here.", MessageType.Info);
            space = EditorGUILayout.Foldout(space, "3D Sound & Environment", true);
            if (space)
            {
                if (emitter.HasDirectSound)
                    foreach (string name in new[] {"spatialBlend", "minDistance", "maxDistance", "doppler", "output"}) Field("soundSettings." + name);
                Field("useEnvironment"); Field("occlusion");
                if (emitter.occlusion) { Field("occlusionMask"); Field("occludedVolume"); Field("occludedLowPass"); }
            }
            train = EditorGUILayout.Foldout(train, "Train Adaptation", true);
            if (train)
            {
                Field("respondToTrainMotion");
                if (emitter.respondToTrainMotion) { Field("rideVolume"); Field("ridePitch"); }
                Field("useSimulatedDoppler"); if (emitter.useSimulatedDoppler) Field("relativeSpeed");
            }
            advanced = EditorGUILayout.Foldout(advanced, "Advanced", true);
            if (advanced) { Field("maxVoices"); Field("priority"); Field("prewarmVoices"); Field("occlusionInterval"); Field("effectSmoothing"); Field("legacyTemplate"); }
            serializedObject.ApplyModifiedProperties();
            if (emitter.HasDirectSound)
            {
                starter = (SoundStarter)EditorGUILayout.EnumPopup("Starting Settings", starter);
                using (new EditorGUI.DisabledScope(starter == SoundStarter.KeepCurrent))
                    if (GUILayout.Button("Apply Starting Settings"))
                    {
                        Undo.RecordObject(emitter, "Apply sound settings");
                        AddSoundWindow.ApplyStarter(emitter, starter); AddSoundWindow.Save(emitter);
                    }
            }
            if (emitter.HasDirectSound && GUILayout.Button("Save Object Sound As Reusable Profile")) SaveProfile(emitter);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Application.isPlaying ? "Play Sound" : "Preview 2D"))
            {
                if (Application.isPlaying)
                {
                    var world = emitter.GetComponent<WorldSoundEmitter>();
                    if (world != null && world.isActiveAndEnabled) world.StartWorldSound(); else emitter.PlayDefault();
                }
                else SoundPreview.Play(emitter);
            }
            if (GUILayout.Button("Stop"))
            {
                if (Application.isPlaying)
                {
                    var world = emitter.GetComponent<WorldSoundEmitter>();
                    if (world != null && world.isActiveAndEnabled) world.StopWorldSound(); else emitter.StopAll();
                }
                else SoundPreview.Stop();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(Application.isPlaying ? emitter.LastPlaybackStatus + " Active voices: " + emitter.ActiveVoiceCount : SoundPreview.Status, MessageType.Info);
            DrawDiagnostics(emitter);
        }
        static void DrawDiagnostics(SoundEmitter emitter)
        {
            if (!emitter.HasDirectSound && emitter.defaultProfile == null)
                EditorGUILayout.HelpBox("No default sound assigned. An external door/footstep/impact profile can still trigger this emitter.", MessageType.Info);
            if (!emitter.playOnEnable && emitter.GetComponent<WorldSoundEmitter>() == null && emitter.GetComponent<Door>() == null && emitter.GetComponent<SimpleFPSControllerNewInput>() == null && emitter.GetComponent<SoundImpact>() == null && emitter.GetComponent<SoundSequencePlayer>() == null)
                EditorGUILayout.HelpBox("Manual trigger: call PlayDefault from a UnityEvent or animation event. Use Add Sound to connect another behaviour.", MessageType.Info);
            var impact = emitter.GetComponent<SoundImpact>();
            if (impact != null && (emitter.GetComponent<Rigidbody>() == null
                || !System.Array.Exists(emitter.GetComponentsInChildren<Collider>(), collider => collider.enabled && !collider.isTrigger)))
                EditorGUILayout.HelpBox("Collision audio needs a Rigidbody and solid collider on this physics object.", MessageType.Warning);
            if (emitter.GetComponent<Door>() is Door door && door.doorRoot == null)
                EditorGUILayout.HelpBox("Assign Door Root on the Door component.", MessageType.Warning);
            if (Application.isPlaying && !System.Array.Exists(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None), listener => listener.isActiveAndEnabled))
                EditorGUILayout.HelpBox("No enabled AudioListener found in the scene.", MessageType.Warning);
            if (emitter.HasDirectSound && emitter.audioClip != null && emitter.soundSettings.spatialBlend > 0f && emitter.audioClip.channels > 1)
                EditorGUILayout.HelpBox("Stereo clip: consider a mono import for a tightly positioned 3D source.", MessageType.Info);
        }
        void OnSceneGUI()
        {
            var emitter = (SoundEmitter)target;
            float distance = emitter.HasDirectSound ? emitter.soundSettings.maxDistance : emitter.defaultProfile != null ? emitter.defaultProfile.maxDistance : 0f;
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Handles.DrawWireDisc(emitter.transform.position, Vector3.up, distance);
            if (Application.isPlaying && SoundEngine.Instance != null && SoundEngine.Instance.listener != null && emitter.occlusion)
                Handles.DrawDottedLine(emitter.transform.position, SoundEngine.Instance.listener.transform.position, 5f);
        }
        static void SaveProfile(SoundEmitter emitter)
        {
            string path = EditorUtility.SaveFilePanelInProject("Save sound profile", emitter.name + " Sound", "asset", "Choose a reusable profile location.");
            if (string.IsNullOrEmpty(path)) return;
            var profile = ScriptableObject.CreateInstance<GameSoundProfile>();
            emitter.soundSettings.Apply(profile);
            var layers = new System.Collections.Generic.List<SoundLayer>();
            var clips = new System.Collections.Generic.List<AudioClip>();
            if (emitter.audioClip != null) clips.Add(emitter.audioClip);
            if (emitter.clipVariations != null) foreach (AudioClip clip in emitter.clipVariations) if (clip != null) clips.Add(clip);
            if (clips.Count > 0) layers.Add(new SoundLayer { name = "Your audio", clips = clips.ToArray(), volume = 1f, pitch = emitter.soundSettings.pitchVariation });
            if (emitter.extraLayers != null)
                foreach (SoundLayer layer in emitter.extraLayers)
                    if (layer != null) layers.Add(JsonUtility.FromJson<SoundLayer>(JsonUtility.ToJson(layer)));
            profile.layers = layers.ToArray();
            AssetDatabase.CreateAsset(profile, path); AssetDatabase.SaveAssets(); EditorGUIUtility.PingObject(profile);
        }
    }

    [CustomEditor(typeof(SoundEnvironmentZone))]
    public class SoundRoomEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var room = (SoundEnvironmentZone)target;
            EditorGUILayout.HelpBox("Room size " + room.RoomSize.ToString("F1") + " m | Volume " + room.RoomVolume.ToString("F1")
                + " m³ | Estimated RT60 " + room.EstimatedDecayTime.ToString("F2") + " s. Size and absorption are approximations; furniture/openings are not traced.", MessageType.Info);
            if (GUILayout.Button("Measure Room From Walls"))
            {
                Undo.RecordObjects(new Object[] {room, room.GetComponent<BoxCollider>()}, "Measure acoustic room");
                if (!room.MeasureRoomFromWalls()) Debug.LogWarning("Measurement needs walls, floor and ceiling in all six directions. Check the probe origin and Measurement Mask.", room);
                EditorUtility.SetDirty(room); EditorUtility.SetDirty(room.GetComponent<BoxCollider>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(room);
                PrefabUtility.RecordPrefabInstancePropertyModifications(room.GetComponent<BoxCollider>());
            }
        }
    }

    [CustomEditor(typeof(SoundAcousticMaterial))]
    public class SoundMaterialEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var material = (SoundAcousticMaterial)target;
            EditorGUILayout.HelpBox("Closed collider thickness is measured along the sound ray. Open/hollow meshes need a manual thickness. Absorption and transmission loss describe different effects.", MessageType.Info);
            if (GUILayout.Button("Use Smallest Collider Dimension As Fallback"))
            {
                Undo.RecordObject(material, "Estimate acoustic thickness");
                material.fallbackThickness = material.EstimateSmallestDimension();
                EditorUtility.SetDirty(material); PrefabUtility.RecordPrefabInstancePropertyModifications(material);
            }
        }
    }
}
