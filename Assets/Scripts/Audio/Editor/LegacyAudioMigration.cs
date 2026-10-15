using UnityEditor;
using UnityEngine;

namespace HorrorAudio.Editor
{
    public static class LegacyAudioMigration
    {
        [MenuItem("Tools/Horror Audio/Import Existing Audio In Open Scenes")]
        public static void ImportOpenScenes()
        {
            if (Application.isPlaying) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            int players = 0, sources = 0;
            foreach (var player in Object.FindObjectsByType<SimpleFPSControllerNewInput>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var footsteps = player.GetComponent<PlayerFootstepAudio>() ?? Undo.AddComponent<PlayerFootstepAudio>(player.gameObject);
                Undo.RecordObject(footsteps, "Import existing footsteps");
                SoundEmitter emitter = player.GetComponent<SoundEmitter>() ?? Undo.AddComponent<SoundEmitter>(player.gameObject);
                Undo.RecordObject(emitter, "Import existing footsteps");
                if (footsteps.footstepSource == null) footsteps.footstepSource = player.GetComponent<AudioSource>();
                if (footsteps.footstepSource != null) Undo.RecordObject(footsteps.footstepSource, "Disable old footstep output");
                footsteps.soundEmitter = emitter; footsteps.ConnectExistingFootsteps();
                AddSoundWindow.Save(footsteps); AddSoundWindow.Save(emitter);
                if (footsteps.footstepSource != null) AddSoundWindow.Save(footsteps.footstepSource);
                players++;
            }
            foreach (var door in Object.FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var emitter = door.GetComponent<SoundEmitter>() ?? Undo.AddComponent<SoundEmitter>(door.gameObject);
                SoundTools.ConfigureDoor(door, emitter); AddSoundWindow.Save(emitter);
            }
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source.clip == null || source.GetComponent<SoundToneFilter>() != null || source.GetComponent<SimpleFPSControllerNewInput>() != null) continue;
                bool imported = false;
                foreach (var bridge in source.GetComponents<LegacySoundSource>()) if (bridge.originalSource == source) imported = true;
                if (imported) continue;
                var bridgeComponent = Undo.AddComponent<LegacySoundSource>(source.gameObject);
                GameObject root = new GameObject("Imported audio - " + source.clip.name);
                Undo.RegisterCreatedObjectUndo(root, "Import existing audio"); root.transform.SetParent(source.transform, false);
                var emitter = Undo.AddComponent<SoundEmitter>(root);
                Undo.RecordObject(emitter, "Copy source configuration"); emitter.ImportSourceSettings(source);
                bridgeComponent.originalSource = source; bridgeComponent.emitter = emitter;
                bridgeComponent.startAutomatically = source.playOnAwake;
                Undo.RecordObject(source, "Disable old audio output");
                source.Stop(); source.playOnAwake = false; source.enabled = false;
                // Preserve explicit UnityEvent calls to Play/Stop/PlayOneShot on the old source.
                foreach (Component component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (component == null || component == bridgeComponent) continue;
                    var serialized = new SerializedObject(component);
                    SerializedProperty iterator = serialized.GetIterator();
                    while (iterator.Next(true))
                    {
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.name != "m_Target" || iterator.objectReferenceValue != source) continue;
                        string parent = iterator.propertyPath.Substring(0, iterator.propertyPath.Length - "m_Target".Length);
                        string method = serialized.FindProperty(parent + "m_MethodName")?.stringValue;
                        if (method != "Play" && method != "Stop" && method != "PlayOneShot") continue;
                        Undo.RecordObject(component, "Redirect audio event"); iterator.objectReferenceValue = bridgeComponent;
                        var type = serialized.FindProperty(parent + "m_TargetAssemblyTypeName");
                        if (type != null) type.stringValue = typeof(LegacySoundSource).AssemblyQualifiedName;
                    }
                    serialized.ApplyModifiedProperties();
                }
                AddSoundWindow.Save(emitter); AddSoundWindow.Save(bridgeComponent); AddSoundWindow.Save(source); sources++;
            }
            Undo.CollapseUndoOperations(group);
            Debug.Log("Horror Audio imported " + players + " player(s) and " + sources + " source(s). Save the modified scenes/prefabs. Script calls must target the adapter or SoundEmitter.");
        }
    }
}
