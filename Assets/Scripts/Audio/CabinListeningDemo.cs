using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorAudio
{
    [AddComponentMenu("Horror Audio/Tests/Cabin Listening Demo")]
    public class CabinListeningDemo : MonoBehaviour
    {
        [Header("Source sounds (runtime copies; original assets stay editable)")]
        public GameSoundProfile footsteps, metalFootsteps, doorOpen, doorClose, latch, impact, wind, hum;
        [Tooltip("Optional: reuse this player's existing footstep files and EQ in the listening room.")]
        public GameObject playerAudioTemplate;
        [Header("Listening mix")]
        [Range(0f, 1f)] public float trainSpeed = 0.45f;
        [Range(0f, 1f)] public float runningGearVolume = 0.22f;
        [Range(0f, 1f)] public float ventilationVolume = 0.12f;
        [Range(0f, 1f)] public float windowWindVolume = 0.14f;
        [Range(0f, 1f)] public float rattleVolume = 0.35f;
        [Range(0f, 1f)] public float probeVolume = 0.45f;
        public bool occlusionEnabled = true;
        public bool roomAcousticsEnabled = true;
        public bool showDiagnostics;
        public bool showHelp = true;
        public bool IsReady { get; private set; }
        public Transform Player { get; private set; }
        public Door CabinDoor { get; private set; }
        public SoundEmitter CorridorProbe { get; private set; }
        public Rigidbody TestBall { get; private set; }
        public bool ExtraLoadEnabled { get; private set; }

        sealed class Source
        {
            public SoundEmitter emitter;
            public int category;
            public bool loop;
            public float next;
        }
        readonly List<Source> sources = new List<Source>();
        readonly List<Source> extraLoad = new List<Source>();
        readonly List<GameSoundProfile> ownedProfiles = new List<GameSoundProfile>();
        readonly List<SoundEnvironmentZone> rooms = new List<SoundEnvironmentZone>();
        readonly bool[] enabledCategories = { true, true, true, true };
        readonly System.Random random = new System.Random(6409);
        CabinLayout layout;
        SoundEngine engine;
        SimpleFPSControllerNewInput movement;
        AudioListener previousListener;
        float previousSpeed, previousStress;
        float nextHud, sampleTime, sampleMax;
        int sampleFrames;
        bool cursorFree, probeRequested;
        float probeRetry;
        string diagnostics = "";
        GUIStyle small, title;

        void Start()
        {
            layout = CabinListeningGeometry.Build(transform);
            Physics.SyncTransforms();
            var walking = Copy(footsteps, "Carpet steps", false, 80);
            var template = playerAudioTemplate != null ? playerAudioTemplate.GetComponent<SoundEmitter>() : null;
            if (template != null && template.HasDirectSound)
            {
                var clips = new List<AudioClip>();
                if (template.audioClip != null) clips.Add(template.audioClip);
                if (template.clipVariations != null) foreach (var clip in template.clipVariations) if (clip != null) clips.Add(clip);
                if (clips.Count > 0)
                {
                    walking.ReleaseGenerated(); template.soundSettings.Apply(walking);
                    walking.layers = new[] {new SoundLayer {clips = clips.ToArray(), volume = 1f, pitch = template.soundSettings.pitchVariation}};
                    walking.loop = false; walking.spatialBlend = 1f; walking.maxDistance = 20f; walking.priority = 80;
                }
            }
            var metal = Copy(metalFootsteps, "Vestibule steps", false, 80);
            var opening = Copy(doorOpen, "Door opening", false, 64);
            var closing = Copy(doorClose, "Door closing", false, 64);
            var catchSound = Copy(latch, "Door latch", false, 64);
            var knock = Copy(impact, "Loose objects", false, 128);
            var air = Copy(wind, "Air movement", true, 192);
            var wheels = Copy(hum, "Running gear", true, 184);
            MakeRoom(layout.CabinRegion, walking, 0.55f);
            MakeRoom(layout.CorridorRegion, metal, 0.2f);
            BuildPlayer(walking);
            engine = SoundEngine.Ensure();
            previousListener = engine.listener; previousSpeed = engine.RideSpeed; previousStress = engine.RideStress;
            engine.listener = Player.GetComponentInChildren<AudioListener>();
            CabinDoor = layout.DoorHinge.gameObject.AddComponent<Door>();
            CabinDoor.doorRoot = layout.DoorHinge; CabinDoor.speed = 4f;
            var doorAudio = layout.DoorHinge.gameObject.AddComponent<DoorAudio>();
            doorAudio.opening.profile = opening; doorAudio.closing.profile = closing; doorAudio.latched.profile = catchSound;
            Setup(doorAudio.GetComponent<SoundEmitter>(), 64, 0.45f, 3);
            AddSource(layout.RunningGearPoint, wheels, 0, true);
            AddSource(layout.VentPoint, air, 1, true);
            AddSource(layout.WindowPoint, air, 2, true);
            AddSource(layout.RattlePoint, knock, 3, false);
            AddSource(layout.RunningGearPoint, knock, 0, false);
            MakeBottle();
            BuildBall(knock);
            CorridorProbe = At("Corridor comparison speaker", layout.CorridorSpeakerPoint.position).AddComponent<SoundEmitter>();
            CorridorProbe.defaultProfile = MakeProbe(); Setup(CorridorProbe, 48, probeVolume, 1);
            for (int i = 0; i < 24; i++)
            {
                var point = At("Optional detail load " + (i + 1), transform.TransformPoint(new Vector3(
                    (i % 2 == 0 ? -1f : 1f) * 1.9f, 0.7f, -3f + (i / 2) * 0.8f)));
                var emitter = point.AddComponent<SoundEmitter>(); emitter.defaultProfile = knock;
                Setup(emitter, 176, 0.08f, 0);
                extraLoad.Add(new Source {emitter = emitter, next = Time.time + i * 0.04f});
            }
            foreach (Source source in sources)
                if (source.loop) source.emitter.PlayDefault();
            SetRoomAcoustics(roomAcousticsEnabled); SetOcclusion(occlusionEnabled);
            engine.ResetStatistics(); IsReady = true;
        }

        GameSoundProfile Copy(GameSoundProfile original, string label, bool loop, int importance)
        {
            var copy = original != null ? Instantiate(original) : ScriptableObject.CreateInstance<GameSoundProfile>();
            copy.name = "Cabin listening - " + label; copy.hideFlags = HideFlags.DontSave;
            copy.loop = loop; copy.spatialBlend = 1f; copy.minDistance = 1f; copy.maxDistance = 20f;
            copy.priority = importance; copy.dopplerLevel = 0f; copy.reverb = AudioReverbPreset.Off;
            if (original == null) copy.layers = new[] {new SoundLayer {generator = loop ? GeneratedSound.Hum : GeneratedSound.Impact, volume = 0.5f}};
            copy.WarmUp(); ownedProfiles.Add(copy); return copy;
        }
        GameSoundProfile MakeProbe()
        {
            var profile = ScriptableObject.CreateInstance<GameSoundProfile>();
            profile.name = "Cabin listening - Door comparison"; profile.hideFlags = HideFlags.DontSave;
            profile.loop = true; profile.volume = 0.65f; profile.spatialBlend = 1f;
            profile.minDistance = 1.5f; profile.maxDistance = 18f; profile.priority = 48;
            profile.layers = new[] {new SoundLayer {generator = GeneratedSound.Hum, frequency = 520f,
                noise = 0.45f, duration = 2f, volume = 1f, pitch = Vector2.one}};
            profile.WarmUp(); ownedProfiles.Add(profile); return profile;
        }
        void MakeRoom(BoxCollider region, GameSoundProfile floorSound, float absorption)
        {
            var zone = region.gameObject.AddComponent<SoundEnvironmentZone>();
            zone.calculateFromRoomSize = true; zone.wallAbsorption = absorption;
            zone.floorAbsorption = absorption; zone.ceilingAbsorption = 0.4f;
            zone.roomLevel = -2100f; zone.tailLevel = -2000f; rooms.Add(zone);
            // A thin floor trigger is not needed: the solid floor is found below its region.
            Vector3 center = region.bounds.center;
            if (Physics.Raycast(center, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                hit.collider.gameObject.AddComponent<SoundSurface>().footsteps = floorSound;
        }
        void BuildPlayer(GameSoundProfile walking)
        {
            var body = At("Listening player", layout.PlayerSpawn); body.layer = 7; Player = body.transform;
            var controller = body.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.center = Vector3.up * 0.9f; controller.radius = 0.28f;
            var camera = new GameObject("Listening camera").AddComponent<Camera>();
            camera.transform.SetParent(Player, false); camera.transform.localPosition = Vector3.up * 1.65f;
            camera.tag = "MainCamera"; camera.fieldOfView = 68f; camera.nearClipPlane = 0.04f; camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.035f, 0.045f, 0.055f);
            camera.gameObject.AddComponent<AudioListener>();
            movement = body.AddComponent<SimpleFPSControllerNewInput>();
            movement.cameraTransform = camera.transform; movement.moveSpeed = 2.4f; movement.sprintSpeed = 3.6f;
            var steps = body.AddComponent<PlayerFootstepAudio>(); steps.footstepProfile = walking;
            Setup(steps.soundEmitter, 80, 0.5f, 2); steps.soundEmitter.occlusion = false;
        }
        void Setup(SoundEmitter emitter, int importance, float gain, int warm)
        {
            emitter.priority = importance; emitter.volume = gain; emitter.occlusion = occlusionEnabled;
            emitter.maxVoices = 4; emitter.prewarmVoices = warm; emitter.Prewarm();
        }
        void AddSource(Transform point, GameSoundProfile profile, int category, bool loop)
        {
            var emitter = At(profile.name, point.position).AddComponent<SoundEmitter>();
            emitter.defaultProfile = profile; Setup(emitter, profile.priority, Gain(category), loop ? 2 : 1);
            emitter.respondToTrainMotion = category == 0;
            sources.Add(new Source {emitter = emitter, category = category, loop = loop, next = Time.time + 1f});
        }
        void BuildBall(GameSoundProfile profile)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Impact test ball"; ball.transform.SetParent(transform, false);
            ball.transform.position = layout.BallSpawn.position - Vector3.up * 1.38f; ball.transform.localScale = Vector3.one * 0.22f;
            TestBall = ball.AddComponent<Rigidbody>(); TestBall.mass = 0.4f; TestBall.isKinematic = true;
            var emitter = ball.AddComponent<SoundEmitter>(); Setup(emitter, 112, 0.75f, 2);
            var collision = ball.AddComponent<SoundImpact>(); collision.emitter = emitter;
            collision.impactProfile = profile; collision.minimumSpeed = 0.15f; collision.fullVolumeSpeed = 3f;
        }
        void MakeBottle()
        {
            var bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottle.name = "Bottle on table"; bottle.transform.SetParent(layout.RattlePoint, false);
            bottle.transform.localPosition = Vector3.up * 0.09f; bottle.transform.localScale = new Vector3(0.11f, 0.18f, 0.11f);
        }
        GameObject At(string label, Vector3 position)
        {
            var obj = new GameObject(label); obj.transform.SetParent(transform, false); obj.transform.position = position; return obj;
        }
        float Gain(int category) => category == 0 ? runningGearVolume : category == 1 ? ventilationVolume : category == 2 ? windowWindVolume : rattleVolume;
        public void SetCategoryEnabled(int category, bool enabled)
        {
            if (category < 0 || category >= enabledCategories.Length) return;
            enabledCategories[category] = enabled;
            foreach (Source source in sources)
                if (source.category == category) { source.emitter.StopAll(); if (enabled && source.loop) source.emitter.PlayDefault(); }
        }
        public void SetExtraLoad(bool enabled)
        {
            ExtraLoadEnabled = enabled;
            foreach (Source source in extraLoad) { source.emitter.StopAll(); source.next = Time.time + (float)random.NextDouble(); }
        }
        public void SetOcclusion(bool enabled)
        {
            occlusionEnabled = enabled;
            foreach (var emitter in GetComponentsInChildren<SoundEmitter>()) emitter.occlusion = enabled;
            if (movement != null) movement.GetComponent<SoundEmitter>().occlusion = false;
        }
        public void SetRoomAcoustics(bool enabled)
        {
            roomAcousticsEnabled = enabled;
            foreach (var room in rooms) room.enabled = enabled;
        }
        public void PlayProbe() { probeRequested = true; if (CorridorProbe != null) CorridorProbe.PlayDefault(); }
        public void ToggleProbe()
        {
            probeRequested = !probeRequested;
            if (CorridorProbe == null) return;
            if (probeRequested) CorridorProbe.PlayDefault(); else CorridorProbe.StopAll();
        }
        public void DropBall()
        {
            if (TestBall == null) return;
            TestBall.isKinematic = false; TestBall.linearVelocity = Vector3.zero; TestBall.angularVelocity = Vector3.zero;
            TestBall.position = layout.BallSpawn.position; TestBall.WakeUp();
        }
        public void ToggleDoor()
        {
            if (CabinDoor != null && Player != null && Vector3.Distance(Player.position, CabinDoor.transform.position) <= 3.2f) CabinDoor.ToggleDoor();
        }
        public void ResetMeasurements() { if (engine != null) engine.ResetStatistics(); sampleTime = sampleMax = 0f; sampleFrames = 0; }

        void Update()
        {
            if (!IsReady) return;
            HandleInput();
            CorridorProbe.volume = probeVolume;
            engine.SetTrainMotion(trainSpeed, Mathf.Sin(Time.time * 0.35f) * 0.08f * trainSpeed, 0f);
            foreach (Source source in sources)
            {
                source.emitter.volume = Gain(source.category);
                if (!enabledCategories[source.category] || Time.time < source.next) continue;
                if (source.loop)
                {
                    source.next = Time.time + 1f;
                    if (source.emitter.ActiveVoiceCount == 0 && source.emitter.DeferredLoopVoiceCount == 0) source.emitter.PlayDefault();
                }
                else
                {
                    source.next = Time.time + (source.category == 0 ? Mathf.Lerp(1.8f, 0.4f, trainSpeed) : 2.6f + (float)random.NextDouble() * 2.5f);
                    if (source.category != 0 || trainSpeed > 0.01f) source.emitter.PlayDefaultSound(0.6f, 1f, true);
                }
            }
            if (ExtraLoadEnabled) foreach (Source source in extraLoad)
                if (Time.time >= source.next) { source.next = Time.time + 0.45f + (float)random.NextDouble() * 0.5f; source.emitter.PlayDefaultSound(forceOneShot: true); }
            if (probeRequested && Time.time >= probeRetry)
            {
                probeRetry = Time.time + 1f;
                if (CorridorProbe.ActiveVoiceCount == 0 && CorridorProbe.DeferredLoopVoiceCount == 0) CorridorProbe.PlayDefault();
            }
            sampleTime += Time.unscaledDeltaTime; sampleFrames++; sampleMax = Mathf.Max(sampleMax, Time.unscaledDeltaTime);
        }
        void HandleInput()
        {
            var keys = Keyboard.current; if (keys == null) return;
            if (keys.fKey.wasPressedThisFrame) ToggleDoor();
            if (keys.bKey.wasPressedThisFrame) DropBall();
            if (keys.tKey.wasPressedThisFrame) ToggleProbe();
            if (keys.lKey.wasPressedThisFrame) SetExtraLoad(!ExtraLoadEnabled);
            if (keys.oKey.wasPressedThisFrame) SetOcclusion(!occlusionEnabled);
            if (keys.rKey.wasPressedThisFrame) SetRoomAcoustics(!roomAcousticsEnabled);
            if (keys.hKey.wasPressedThisFrame) showHelp = !showHelp;
            if (keys.pKey.wasPressedThisFrame) showDiagnostics = !showDiagnostics;
            if (keys.backspaceKey.wasPressedThisFrame) ResetMeasurements();
            for (int i = 0; i < 4; i++) if (keys[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) SetCategoryEnabled(i, !enabledCategories[i]);
            if (keys.upArrowKey.isPressed) trainSpeed = Mathf.Clamp01(trainSpeed + Time.deltaTime * 0.2f);
            if (keys.downArrowKey.isPressed) trainSpeed = Mathf.Clamp01(trainSpeed - Time.deltaTime * 0.2f);
            if (keys.escapeKey.wasPressedThisFrame)
            {
                cursorFree = !cursorFree; movement.isInspecting = cursorFree;
                Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = cursorFree;
            }
        }
        void LateUpdate()
        {
            if (!IsReady || Time.unscaledTime < nextHud) return; nextHud = Time.unscaledTime + 0.4f;
            diagnostics = "Voices " + engine.ActiveVoiceCount + "/" + engine.maxActiveVoices + " | virtual " + engine.VirtualVoiceCount +
                " | deferred " + engine.DeferredLoopVoiceCount + " | pooled " + engine.PooledVoiceCount +
                "\nWall checks " + engine.OcclusionChecksThisFrame + " | rejected " + engine.RejectedPlaybackCount + " | replaced " + engine.StolenVoiceCount +
                "\nWhole-scene frame: avg " + (sampleTime * 1000f / Mathf.Max(1, sampleFrames)).ToString("F1") + " ms, max " + (sampleMax * 1000f).ToString("F1") +
                " ms. DSP CPU: use Unity Audio Profiler.";
            sampleTime = sampleMax = 0f; sampleFrames = 0;
        }
        string State(int category) => enabledCategories[category] ? "ON" : "OFF";
        void OnGUI()
        {
            if (!IsReady) return;
            if (small == null) { small = new GUIStyle(GUI.skin.label) {fontSize = 13, wordWrap = true}; title = new GUIStyle(small) {fontSize = 17, fontStyle = FontStyle.Bold}; }
            GUI.Box(new Rect(16, 16, 430, showHelp ? 192 : 48), GUIContent.none);
            GUI.Label(new Rect(28, 23, 405, 28), "CABIN / listening room", title);
            if (showHelp)
            {
                GUI.Label(new Rect(28, 54, 405, 145),
                    "WASD + mouse / Shift sprint / Ctrl crouch\nF door (nearby)   B drop ball   T corridor test sound\n1 wheels  2 ventilation  3 window wind  4 loose bottle\nO wall damping: " + (occlusionEnabled ? "ON" : "OFF") + "   R room reverb: " + (roomAcousticsEnabled ? "ON" : "OFF") +
                    "\nL extra activity: " + (ExtraLoadEnabled ? "ON" : "OFF") + "   Up/Down train speed\nP diagnostics   H help   Backspace reset counters\nEscape release/capture mouse", small);
            }
            if (showDiagnostics) { GUI.Box(new Rect(16, Screen.height - 100, Mathf.Min(820, Screen.width - 32), 84), GUIContent.none); GUI.Label(new Rect(28, Screen.height - 92, Mathf.Min(796, Screen.width - 56), 76), diagnostics, small); }
            if (CabinDoor != null && Vector3.Distance(Player.position, CabinDoor.transform.position) < 3.2f)
                GUI.Label(new Rect(Screen.width * 0.5f - 100, Screen.height * 0.65f, 200, 26), CabinDoor.IsOpen ? "F - Close compartment door" : "F - Open compartment door", small);
        }
        void OnDestroy()
        {
            IsReady = false;
            foreach (var emitter in GetComponentsInChildren<SoundEmitter>(true)) emitter.StopAll();
            foreach (var profile in ownedProfiles) if (profile != null) Destroy(profile);
            if (engine != null) { engine.listener = previousListener; engine.SetTrainMotion(previousSpeed, previousStress, 0f); }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
}
