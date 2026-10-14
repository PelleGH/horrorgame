using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorAudio.Editor
{
    public static class SoundTools
    {
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
                    Undo.RecordObject(player, "Configure footsteps");
                    player.soundEmitter = emitter;
                    if (player.footstepProfile == null) player.footstepProfile = Profile("Footsteps");
                }
                Door door = selected.GetComponent<Door>();
                if (door != null)
                {
                    Undo.RecordObject(door, "Configure door audio");
                    door.soundEmitter = emitter;
                    if (door.openSound == null) door.openSound = Profile("DoorOpen");
                    if (door.closeSound == null) door.closeSound = Profile("DoorClose");
                    if (door.latchSound == null) door.latchSound = Profile("Latch");
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
        public override void OnInspectorGUI()
        {
            SoundEmitter emitter = (SoundEmitter)target;
            EditorGUILayout.HelpBox("Drag your AudioClip into Audio Clip. Sound Settings affect this object's file; Default Profile remains the reusable fallback. Add Extra Layers for texture.", MessageType.Info);
            DrawDefaultInspector();
            if (emitter.HasDirectSound && GUILayout.Button("Save Object Sound As Reusable Profile")) SaveProfile(emitter);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Play Default Profile")) ((SoundEmitter)target).PlayDefault();
                if (GUILayout.Button("Stop Sounds")) ((SoundEmitter)target).StopAll();
            }
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
