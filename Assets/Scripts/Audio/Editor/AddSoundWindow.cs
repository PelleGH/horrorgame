using UnityEditor;
using UnityEngine;

namespace HorrorAudio.Editor
{
    public enum SoundBehaviour { ManualEvent, Ambience, Footsteps, Door, Collision, World, TriggerZone }
    public enum SoundStarter { KeepCurrent, Neutral, MetalImpact, SoftWind, SmallTrainCar }

    public class AddSoundWindow : EditorWindow
    {
        GameObject selectedObject;
        AudioClip clip;
        SoundBehaviour behaviour;
        SoundStarter starter = SoundStarter.KeepCurrent;
        string result;
        [MenuItem("Tools/Horror Audio/Add Sound...")]
        public static void Open()
        {
            var window = GetWindow<AddSoundWindow>("Add Sound");
            window.minSize = new Vector2(360f, 300f);
            window.selectedObject = Selection.activeGameObject;
        }
        [MenuItem("GameObject/Horror Audio/Add Sound...", false, 20)]
        static void OpenFromObject(MenuCommand command)
        {
            Open();
            if (command.context is GameObject target) GetWindow<AddSoundWindow>().selectedObject = target;
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("Add audio to your object", EditorStyles.boldLabel);
            selectedObject = (GameObject)EditorGUILayout.ObjectField("Object", selectedObject, typeof(GameObject), true);
            clip = (AudioClip)EditorGUILayout.ObjectField("Your audio file", clip, typeof(AudioClip), false);
            behaviour = (SoundBehaviour)EditorGUILayout.EnumPopup("Trigger", behaviour);
            starter = (SoundStarter)EditorGUILayout.EnumPopup("Starting settings", starter);
            if (clip == null && starter != SoundStarter.KeepCurrent)
                EditorGUILayout.HelpBox("Starting settings affect direct audio files. A reusable profile owns its own effects.", MessageType.Info);
            EditorGUILayout.HelpBox("Ambience starts automatically. Other behaviours connect existing components. Manual Event exposes PlayDefault for UnityEvents and animation events. TriggerZone adds a trigger box to an empty object; the entering actor needs a CharacterController or Rigidbody. A collision object needs its own solid collider and Rigidbody.", MessageType.Info);
            if (behaviour == SoundBehaviour.Collision && selectedObject != null && selectedObject.GetComponent<Rigidbody>() == null)
                EditorGUILayout.HelpBox("Add a Rigidbody to this object's physics root first. This tool does not change your object's physics.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(selectedObject == null || Application.isPlaying))
                if (GUILayout.Button("Add / Configure Sound", GUILayout.Height(32f)))
                {
                    try
                    {
                        Configure(selectedObject, clip, behaviour, starter);
                        Selection.activeGameObject = selectedObject;
                        result = "Configured. Use Preview in the SoundEmitter Inspector.";
                    }
                    catch (System.InvalidOperationException error) { result = error.Message; }
                }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
        }
        public static SoundEmitter Configure(GameObject target, AudioClip clip, SoundBehaviour behaviour, SoundStarter starter)
        {
            if (target == null) throw new System.InvalidOperationException("Select an object first.");
            if (behaviour == SoundBehaviour.World && (target.GetComponent<Door>() != null || target.GetComponent<SimpleFPSControllerNewInput>() != null || target.GetComponent<SoundImpact>() != null))
                throw new System.InvalidOperationException("Use a dedicated empty object for continuous world sound.");
            if (EditorUtility.IsPersistent(target)) throw new System.InvalidOperationException("Open this prefab in Prefab Mode before configuring its audio.");
            if (behaviour == SoundBehaviour.Footsteps && target.GetComponent<SimpleFPSControllerNewInput>() == null)
                throw new System.InvalidOperationException("Select the object containing your player controller.");
            if (behaviour == SoundBehaviour.Door && target.GetComponent<Door>() == null)
                throw new System.InvalidOperationException("Select the object containing Door; assign Door Root there.");
            if (behaviour == SoundBehaviour.Collision && (target.GetComponent<Rigidbody>() == null
                || !System.Array.Exists(target.GetComponentsInChildren<Collider>(), collider => collider.enabled && !collider.isTrigger)))
                throw new System.InvalidOperationException("Collision audio needs a Rigidbody and collider on this physics object.");
            if (behaviour == SoundBehaviour.TriggerZone && target.GetComponent<Collider>() != null &&
                !System.Array.Exists(target.GetComponents<Collider>(), collider => collider.enabled && collider.isTrigger))
                throw new System.InvalidOperationException("Use a dedicated trigger object or add a trigger collider first.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add sound");
            SoundEmitter emitter = target.GetComponent<SoundEmitter>() ?? Undo.AddComponent<SoundEmitter>(target);
            Undo.RecordObject(emitter, "Configure sound");
            if (clip != null) emitter.audioClip = clip;
            ApplyStarter(emitter, starter);
            emitter.playOnEnable = behaviour == SoundBehaviour.Ambience;
            if (behaviour == SoundBehaviour.Ambience) emitter.soundSettings.loop = true;
            else emitter.soundSettings.loop = false;
            switch (behaviour)
            {
                case SoundBehaviour.World:
                    var world = target.GetComponent<WorldSoundEmitter>() ?? Undo.AddComponent<WorldSoundEmitter>(target);
                    Undo.RecordObject(world, "Connect world sound"); world.emitter = emitter;
                    if (!emitter.HasDirectSound && emitter.defaultProfile == null) emitter.defaultProfile = SoundTools.Profile("Wind");
                    Save(world); break;
                case SoundBehaviour.Footsteps:
                    var player = target.GetComponent<PlayerFootstepAudio>() ?? Undo.AddComponent<PlayerFootstepAudio>(target);
                    Undo.RecordObject(player, "Connect footsteps"); player.soundEmitter = emitter;
                    if (emitter.priority < 0) emitter.priority = 80;
                    var footstepSource = player.footstepSource != null ? player.footstepSource : player.GetComponent<AudioSource>();
                    if (footstepSource != null) Undo.RecordObject(footstepSource, "Import footstep source");
                    player.ConnectExistingFootsteps();
                    if (footstepSource != null) Save(footstepSource);
                    if (player.footstepProfile == null && !emitter.HasDirectSound) player.footstepProfile = SoundTools.Profile("Footsteps");
                    Save(player); break;
                case SoundBehaviour.Door:
                    var doorAudio = SoundTools.ConfigureDoor(target.GetComponent<Door>(), emitter);
                    if (clip != null)
                    {
                        Undo.RecordObject(doorAudio, "Assign door file");
                        doorAudio.opening.sequence = null; doorAudio.closing.sequence = null;
                        doorAudio.opening.clip = clip; doorAudio.closing.clip = clip;
                        Save(doorAudio);
                    }
                    break;
                case SoundBehaviour.TriggerZone:
                    var trigger = target.GetComponent<SoundTrigger>() ?? Undo.AddComponent<SoundTrigger>(target);
                    var sequencePlayer = target.GetComponent<SoundSequencePlayer>();
                    Undo.RecordObject(trigger, "Configure trigger"); Undo.RecordObject(sequencePlayer, "Configure sequence player");
                    trigger.player = sequencePlayer; sequencePlayer.emitter = emitter;
                    trigger.entered.useEmitterSound = true;
                    if (target.GetComponent<Collider>() == null)
                    {
                        var box = Undo.AddComponent<BoxCollider>(target); box.isTrigger = true;
                    }
                    Save(trigger); Save(sequencePlayer); break;
                case SoundBehaviour.Collision:
                    var impact = target.GetComponent<SoundImpact>() ?? Undo.AddComponent<SoundImpact>(target);
                    Undo.RecordObject(impact, "Connect collision"); impact.emitter = emitter;
                    if (!emitter.HasDirectSound && impact.impactProfile == null) impact.impactProfile = SoundTools.Profile("Impact");
                    Save(impact); break;
            }
            Save(emitter); Undo.CollapseUndoOperations(group); return emitter;
        }
        internal static void Save(Object target)
        {
            EditorUtility.SetDirty(target); PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        public static void ApplyStarter(SoundEmitter emitter, SoundStarter starter)
        {
            if (starter == SoundStarter.KeepCurrent) return;
            var settings = new ObjectSoundSettings();
            // Mixer routing is wiring, rather than part of the sound's timbre.
            settings.output = emitter.soundSettings.output;
            switch (starter)
            {
                case SoundStarter.MetalImpact: settings.bassDb = 3f; settings.trebleDb = 2f; settings.distortion = 0.1f; settings.pitchVariation = new Vector2(0.94f, 1.06f); break;
                case SoundStarter.SoftWind: settings.loop = true; settings.volume = 0.35f; settings.lowPass = 6000f; settings.trebleDb = -3f; break;
                case SoundStarter.SmallTrainCar: settings.reverb = AudioReverbPreset.Room; settings.reverbLevel = -2200f; settings.bassDb = 2f; break;
            }
            emitter.soundSettings = settings;
        }
    }
}
