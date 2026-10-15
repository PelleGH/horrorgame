using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorAudio
{
    /// <summary>
    /// Isolated train sound workload. Geometry and all transient profiles belong to this scene;
    /// the eight authored profile assets are never modified.
    /// </summary>
    [AddComponentMenu("Horror Audio/Tests/Audio Stress Demo")]
    public class AudioStressDemo : MonoBehaviour
    {
        [Header("Shared source profiles (copied once on startup)")]
        public GameSoundProfile footsteps, metalFootsteps, doorOpen, doorClose, latch, impact, wind, hum;
        [Header("Workload")]
        [Range(24, 512)] public int emitterPoolSize = 192;
        [Range(0, 512)] public int idleEmitterCount = 128;
        public int[] loadLevels = { 0, 24, 48, 96, 192 };
        [Min(0)] public int startingLoadLevel = 2;
        [Range(8, 128)] public int voiceBudget = 48;
        [Min(0.2f)] public float averageEventInterval = 2.5f;
        [Range(1, 64)] public int maxScheduledEventsPerFrame = 12;
        [Range(1, 256)] public int burstSize = 96;
        public bool occlusionEnabled = true;
        [Range(0f, 1f)] public float trainSpeed = 0.65f;
        [Range(0f, 0.2f)] public float backgroundGain = 0.035f;
        [Range(0f, 1f)] public float eventGain = 0.3f;
        public bool showDiagnostics = true;

        public int DesiredActiveEmitters { get; private set; }
        public int CreatedEmitterCount => entries.Count + idleEmitters.Count;
        public long ScheduledPlaybackCount { get; private set; }
        public bool IsReady { get; private set; }

        sealed class Entry
        {
            public SoundEmitter emitter;
            public bool loop;
            public float nextPlay;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly List<SoundEmitter> idleEmitters = new List<SoundEmitter>();
        readonly List<GameSoundProfile> ownedProfiles = new List<GameSoundProfile>();
        readonly Dictionary<GameSoundProfile, GameSoundProfile> copies = new Dictionary<GameSoundProfile, GameSoundProfile>();
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly List<Door> doors = new List<Door>();
        readonly System.Random random = new System.Random(84127);
        SoundEngine engine;
        SimpleFPSControllerNewInput movement;
        Transform player;
        GameSoundProfile eventProfile;
        int loadLevel, serviceCursor, originalVoiceBudget;
        float nextHudRefresh, windowDuration, windowWorst;
        int windowFrames;
        float averageFrameMs, worstFrameMs;
        string diagnosticText = "Preparing shared clips and voice pools...";
        bool cursorReleased;

        void Start()
        {
            if (loadLevels == null || loadLevels.Length == 0) loadLevels = new[] { 0, 24, 48, 96, 192 };
            BuildTrain();
            GameSoundProfile walking = CopyProfile(footsteps, "Walking", GeneratedSound.Impact, false, 64);
            GameSoundProfile metal = CopyProfile(metalFootsteps, "Metal footsteps", GeneratedSound.Impact, false, 64);
            GameSoundProfile opening = CopyProfile(doorOpen, "Door opening", GeneratedSound.Creak, false, 48);
            GameSoundProfile closing = CopyProfile(doorClose, "Door closing", GeneratedSound.Creak, false, 48);
            GameSoundProfile catchSound = CopyProfile(latch, "Door latch", GeneratedSound.Impact, false, 40);
            eventProfile = CopyProfile(impact, "Loose train objects", GeneratedSound.Impact, false, 128);
            GameSoundProfile ventilation = CopyProfile(wind, "Ventilation", GeneratedSound.Wind, true, 200);
            GameSoundProfile rolling = CopyProfile(hum, "Running gear", GeneratedSound.Hum, true, 184);

            BuildPlayer(walking);
            engine = SoundEngine.Ensure();
            originalVoiceBudget = engine.maxActiveVoices;
            engine.maxActiveVoices = voiceBudget;
            engine.listener = player.GetComponentInChildren<AudioListener>();

            for (int carriage = 0; carriage < 3; carriage++)
            {
                float z = (carriage - 1) * 12f;
                GameObject floor = Box("Carriage " + (carriage + 1) + " floor", new Vector3(0f, -0.15f, z),
                    new Vector3(6f, 0.3f, 12f), carriage == 1 ? new Color(0.24f, 0.28f, 0.3f) : new Color(0.27f, 0.2f, 0.15f));
                floor.AddComponent<SoundSurface>().footsteps = carriage == 1 ? metal : walking;
                GameObject region = Child("Carriage " + (carriage + 1) + " acoustic region", new Vector3(0f, 1.5f, z));
                region.AddComponent<BoxCollider>().size = new Vector3(5.8f, 3f, 11.8f);
                SoundEnvironmentZone zone = region.AddComponent<SoundEnvironmentZone>();
                zone.calculateFromRoomSize = true;
                zone.wallAbsorption = 0.35f; zone.floorAbsorption = 0.55f; zone.ceilingAbsorption = 0.4f;
                zone.roomLevel = -2200f; zone.tailLevel = -2200f;
            }
            BuildDoor(-6f, opening, closing, catchSound);
            BuildDoor(6f, opening, closing, catchSound);
            BuildWorkload(ventilation, rolling);
            loadLevel = Mathf.Clamp(startingLoadLevel, 0, Mathf.Max(0, loadLevels.Length - 1));
            SetLoadLevel(loadLevel);
            engine.SetTrainMotion(trainSpeed, 0f, 0f);
            engine.ResetStatistics();
            IsReady = true;
        }

        GameSoundProfile CopyProfile(GameSoundProfile source, string label, GeneratedSound fallback, bool loop, int priority)
        {
            if (source != null && copies.TryGetValue(source, out GameSoundProfile existing)) return existing;
            GameSoundProfile copy = source != null ? Instantiate(source) : ScriptableObject.CreateInstance<GameSoundProfile>();
            copy.name = "Stress test - " + label;
            copy.hideFlags = HideFlags.DontSave;
            copy.loop = loop; copy.priority = priority;
            copy.spatialBlend = 1f; copy.dopplerLevel = 0f;
            copy.minDistance = 1.2f; copy.maxDistance = 22f;
            if (source == null)
                copy.layers = new[] { new SoundLayer { generator = fallback, duration = loop ? 2f : 0.25f,
                    volume = 0.5f, frequency = fallback == GeneratedSound.Hum ? 70f : 130f,
                    noise = fallback == GeneratedSound.Wind ? 0.85f : 0.3f } };
            copy.WarmUp();
            ownedProfiles.Add(copy);
            if (source != null) copies.Add(source, copy);
            return copy;
        }

        void BuildTrain()
        {
            Color wall = new Color(0.34f, 0.38f, 0.39f);
            AddWall("Left carriage wall", new Vector3(-3.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 36f), wall);
            AddWall("Right carriage wall", new Vector3(3.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 36f), wall);
            AddWall("Front end", new Vector3(0f, 1.5f, -18.1f), new Vector3(6.4f, 3f, 0.2f), wall);
            AddWall("Rear end", new Vector3(0f, 1.5f, 18.1f), new Vector3(6.4f, 3f, 0.2f), wall);
            for (int i = 0; i < 3; i++)
            {
                float z = (i - 1) * 12f;
                Box("Ceiling", new Vector3(0f, 3.15f, z), new Vector3(6.2f, 0.3f, 12f), wall);
                for (int side = -1; side <= 1; side += 2)
                    for (int row = -1; row <= 1; row++)
                    {
                        Box("Bench", new Vector3(side * 2.25f, 0.4f, z + row * 3f), new Vector3(1f, 0.8f, 1.9f),
                            new Color(0.13f, 0.24f, 0.26f));
                        Box("Window", new Vector3(side * 2.98f, 1.85f, z + row * 3f), new Vector3(0.025f, 0.95f, 1.9f),
                            new Color(0.08f, 0.12f, 0.17f));
                    }
                Light light = Child("Carriage light", new Vector3(0f, 2.6f, z)).AddComponent<Light>();
                light.type = LightType.Point; light.range = 12f; light.intensity = 3f;
                light.color = new Color(1f, 0.87f, 0.7f); light.shadows = LightShadows.None;
            }
            Light fill = Child("Test fill light", Vector3.zero).AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.55f;
            fill.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
        }

        void BuildPlayer(GameSoundProfile walking)
        {
            GameObject body = Child("Stress test player", new Vector3(0f, 0.1f, -13f));
            body.layer = 7;
            player = body.transform;
            CharacterController capsule = body.AddComponent<CharacterController>();
            capsule.height = 1.8f; capsule.center = Vector3.up * 0.9f; capsule.radius = 0.3f;
            GameObject cameraObject = new GameObject("Player camera");
            cameraObject.transform.SetParent(player, false); cameraObject.transform.localPosition = Vector3.up * 1.65f;
            Camera view = cameraObject.AddComponent<Camera>();
            view.tag = "MainCamera"; view.fieldOfView = 68f; view.farClipPlane = 100f;
            view.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
            cameraObject.AddComponent<AudioListener>();
            movement = body.AddComponent<SimpleFPSControllerNewInput>();
            movement.cameraTransform = view.transform; movement.moveSpeed = 3f; movement.sprintSpeed = 5f;
            PlayerFootstepAudio audio = body.AddComponent<PlayerFootstepAudio>();
            audio.footstepProfile = walking; audio.soundEmitter.volume = 0.4f;
            audio.soundEmitter.priority = 64; audio.soundEmitter.prewarmVoices = 2;
            audio.soundEmitter.Prewarm();
        }

        void BuildDoor(float z, GameSoundProfile opening, GameSoundProfile closing, GameSoundProfile latched)
        {
            Color wall = new Color(0.34f, 0.38f, 0.39f);
            AddWall("Compartment partition", new Vector3(-1.95f, 1.5f, z), new Vector3(2.1f, 3f, 0.2f), wall);
            AddWall("Compartment partition", new Vector3(1.95f, 1.5f, z), new Vector3(2.1f, 3f, 0.2f), wall);
            AddWall("Door lintel", new Vector3(0f, 2.8f, z), new Vector3(1.8f, 0.4f, 0.2f), wall);
            GameObject hinge = Child("Compartment door", new Vector3(-0.9f, 0f, z));
            GameObject panel = AddWall("Door panel", Vector3.zero, new Vector3(1.8f, 2.6f, 0.16f),
                new Color(0.38f, 0.24f, 0.12f));
            panel.transform.SetParent(hinge.transform, false); panel.transform.localPosition = new Vector3(0.9f, 1.3f, 0f);
            Door door = hinge.AddComponent<Door>(); door.doorRoot = hinge.transform; door.speed = 4f;
            DoorAudio audio = hinge.AddComponent<DoorAudio>();
            audio.opening.profile = opening; audio.closing.profile = closing; audio.latched.profile = latched;
            SoundEmitter emitter = hinge.GetComponent<SoundEmitter>();
            emitter.priority = 40; emitter.volume = 0.45f; emitter.prewarmVoices = 3;
            emitter.Prewarm();
            doors.Add(door);
        }

        void BuildWorkload(GameSoundProfile ventilation, GameSoundProfile rolling)
        {
            int count = Mathf.Clamp(emitterPoolSize, 24, 512);
            for (int i = 0; i < count; i++)
            {
                bool loop = i % 4 == 0;
                SoundEmitter emitter = Child((loop ? "Background loop " : "Loose object ") + i, SourcePosition(i)).AddComponent<SoundEmitter>();
                emitter.defaultProfile = loop ? (i % 8 == 0 ? rolling : ventilation) : eventProfile;
                emitter.maxVoices = 4; emitter.prewarmVoices = loop ? 2 : 1;
                emitter.priority = loop ? (i % 8 == 0 ? 184 : 200) : 128;
                emitter.volume = loop ? backgroundGain : eventGain;
                emitter.occlusion = occlusionEnabled;
                emitter.respondToTrainMotion = loop; emitter.useSimulatedDoppler = false;
                entries.Add(new Entry { emitter = emitter, loop = loop });
            }
            for (int i = 0; i < Mathf.Clamp(idleEmitterCount, 0, 512); i++)
            {
                SoundEmitter emitter = Child("Idle emitter (no playback) " + i, SourcePosition(i + count)).AddComponent<SoundEmitter>();
                emitter.defaultProfile = eventProfile; emitter.occlusion = occlusionEnabled;
                emitter.prewarmVoices = 0;
                idleEmitters.Add(emitter);
            }
        }

        Vector3 SourcePosition(int index)
        {
            // Interleave compartments so each load level contains both nearby and distant sources.
            float z = (index % 3 - 1) * 12f + (float)random.NextDouble() * 9f - 4.5f;
            return new Vector3((index % 2 == 0 ? -1f : 1f) * 2.65f, 0.7f + (float)random.NextDouble() * 1.4f, z);
        }

        public void SetLoadLevel(int index)
        {
            if (loadLevels == null || loadLevels.Length == 0) return;
            loadLevel = Mathf.Clamp(index, 0, loadLevels.Length - 1);
            int target = Mathf.Clamp(loadLevels[loadLevel], 0, entries.Count);
            int previous = DesiredActiveEmitters;
            DesiredActiveEmitters = target;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (i >= target) { entry.emitter.StopAll(); continue; }
                if (i < previous) continue;
                entry.emitter.Prewarm();
                // Spread starts over a second; profile generation already finished before entering this workload.
                entry.nextPlay = Time.time + (float)random.NextDouble();
            }
        }

        public void TriggerBurst()
        {
            int remaining = Mathf.Clamp(burstSize, 1, 256);
            for (int i = 0; i < entries.Count && remaining > 0; i++)
            {
                Entry entry = entries[i];
                if (entry.loop) continue;
                entry.emitter.Play(eventProfile, eventGain / Mathf.Max(0.001f, entry.emitter.volume));
                ScheduledPlaybackCount++;
                remaining--;
            }
        }

        public void SetOcclusion(bool enabled)
        {
            occlusionEnabled = enabled;
            foreach (Entry entry in entries) entry.emitter.occlusion = enabled;
            foreach (SoundEmitter emitter in idleEmitters) emitter.occlusion = enabled;
        }

        public void ResetMeasurements()
        {
            if (engine != null) engine.ResetStatistics();
            ScheduledPlaybackCount = 0;
            windowDuration = windowWorst = 0f; windowFrames = 0;
            averageFrameMs = worstFrameMs = 0f;
        }

        void Update()
        {
            if (!IsReady) return;
            HandleInput();
            engine.maxActiveVoices = Mathf.Clamp(voiceBudget, 8, 128);
            engine.SetTrainMotion(trainSpeed, Mathf.Sin(Time.time * 0.4f) * 0.1f, 0f);
            int scheduled = 0;
            int examined = 0;
            while (examined++ < DesiredActiveEmitters && scheduled < maxScheduledEventsPerFrame)
            {
                if (serviceCursor >= DesiredActiveEmitters) serviceCursor = 0;
                Entry entry = entries[serviceCursor++];
                entry.emitter.volume = entry.loop ? backgroundGain : eventGain;
                if (Time.time < entry.nextPlay) continue;
                entry.nextPlay = Time.time + (entry.loop ? 2f : Mathf.Max(0.2f, averageEventInterval)) * (0.7f + (float)random.NextDouble() * 0.6f);
                if (entry.loop && entry.emitter.ActiveVoiceCount > 0) continue;
                entry.emitter.PlayDefault();
                ScheduledPlaybackCount++; scheduled++;
            }
            float dt = Time.unscaledDeltaTime;
            windowDuration += dt; windowFrames++; windowWorst = Mathf.Max(windowWorst, dt);
        }

        void LateUpdate()
        {
            // Sample after emitter Updates so the per-frame acoustic counter is complete.
            if (!IsReady || Time.unscaledTime < nextHudRefresh) return;
            averageFrameMs = windowDuration * 1000f / Mathf.Max(1, windowFrames);
            worstFrameMs = windowWorst * 1000f;
            windowDuration = windowWorst = 0f; windowFrames = 0;
            nextHudRefresh = Time.unscaledTime + 0.5f;
            RefreshDiagnostics();
        }

        void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame) SetLoadLevel(loadLevel + 1);
            if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame) SetLoadLevel(loadLevel - 1);
            if (keyboard.bKey.wasPressedThisFrame) TriggerBurst();
            if (keyboard.rKey.wasPressedThisFrame) ResetMeasurements();
            if (keyboard.oKey.wasPressedThisFrame) SetOcclusion(!occlusionEnabled);
            if (keyboard.hKey.wasPressedThisFrame) showDiagnostics = !showDiagnostics;
            if (keyboard.fKey.wasPressedThisFrame)
            {
                Door nearest = null; float distance = float.PositiveInfinity;
                foreach (Door door in doors)
                {
                    float candidate = (player.position - door.transform.position).sqrMagnitude;
                    if (candidate < distance) { nearest = door; distance = candidate; }
                }
                if (nearest != null) nearest.ToggleDoor();
            }
            if (keyboard.upArrowKey.isPressed) trainSpeed = Mathf.Clamp01(trainSpeed + Time.deltaTime * 0.25f);
            if (keyboard.downArrowKey.isPressed) trainSpeed = Mathf.Clamp01(trainSpeed - Time.deltaTime * 0.25f);
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                cursorReleased = !cursorReleased;
                movement.isInspecting = cursorReleased;
                Cursor.lockState = cursorReleased ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = cursorReleased;
            }
        }

        void RefreshDiagnostics()
        {
            diagnosticText = "TRAIN AUDIO STRESS TEST\n" +
                "WASD / mouse | Shift sprint | Ctrl crouch | F nearest door\n" +
                "+ / - load | B burst | O occlusion | R reset counters | H hide HUD\n" +
                "Up / Down train speed | Escape release / capture mouse\n\n" +
                "Requested workload: " + DesiredActiveEmitters + " / " + entries.Count + " emitters; idle: " + idleEmitters.Count + "\n" +
                "Voices: " + engine.ActiveVoiceCount + " playing / " + engine.maxActiveVoices + " budget; " + engine.PooledVoiceCount + " pooled\n" +
                "Unity virtual: " + engine.VirtualVoiceCount + " (included in playing count; silent DSP voices)\n" +
                "Deferred background loop layers: " + engine.DeferredLoopVoiceCount + " (resume when capacity returns)\n" +
                "Playback requests: " + ScheduledPlaybackCount + "; rejected: " + engine.RejectedPlaybackCount + "; stolen: " + engine.StolenVoiceCount + "\n" +
                "Occlusion: " + (occlusionEnabled ? "ON" : "OFF") + "; checks this frame: " + engine.OcclusionChecksThisFrame +
                "; total: " + engine.OcclusionChecksTotal + "\n" +
                "Whole-scene frame time (0.5 s): avg " + averageFrameMs.ToString("F2") + " ms; max " + worstFrameMs.ToString("F2") + " ms\n" +
                "Simulated train speed: " + trainSpeed.ToString("F2") + "\n\n" +
                "Frame time includes graphics and Editor overhead. DSP CPU is not measured here.\n" +
                "Use Window > Analysis > Profiler > Audio for DSP CPU, voices and memory.\n" +
                "Compare load 0, 48, 192 and occlusion OFF / ON after warm-up.\n" +
                "Generated sounds are test placeholders; this scene does not simulate portals.";
        }

        void OnGUI()
        {
            if (!showDiagnostics) return;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 720f, Screen.height / 480f), 0.5f, 1f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.Box(new Rect(12f, 12f, 690f, 374f), GUIContent.none);
            GUI.Label(new Rect(24f, 20f, 668f, 355f), diagnosticText);
            GUI.matrix = previous;
        }

        GameObject Child(string title, Vector3 position)
        {
            GameObject created = new GameObject(title);
            created.transform.SetParent(transform, false); created.transform.localPosition = position;
            return created;
        }

        GameObject AddWall(string title, Vector3 position, Vector3 scale, Color color)
        {
            GameObject wall = Box(title, position, scale, color);
            SoundAcousticMaterial material = wall.AddComponent<SoundAcousticMaterial>();
            material.absorption = 0.35f; material.baseTransmissionLossDb = 6f;
            material.lossDbPerMetre = 20f; material.transmissionLowPass = 2400f;
            return wall;
        }

        GameObject Box(string title, Vector3 position, Vector3 scale, Color color)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = title; box.transform.SetParent(transform, false);
            box.transform.localPosition = position; box.transform.localScale = scale;
            if (!materials.TryGetValue(color, out Material material))
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
                material = new Material(shader) { color = color };
                materials.Add(color, material);
            }
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        void OnDestroy()
        {
            IsReady = false;
            // Stop playback before destroying the shared generated buffers referenced by pooled voices.
            foreach (SoundEmitter emitter in GetComponentsInChildren<SoundEmitter>(true)) emitter.StopAll();
            foreach (GameSoundProfile profile in ownedProfiles) if (profile != null) Destroy(profile);
            foreach (Material material in materials.Values) if (material != null) Destroy(material);
            if (engine != null) engine.maxActiveVoices = originalVoiceBudget;
            if (cursorReleased) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
}
