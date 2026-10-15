using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorAudio
{
    // Everything in this isolated test scene is generated at runtime.
    public class SoundEngineDemo : MonoBehaviour
    {
        public GameSoundProfile footsteps, metalFootsteps, doorOpen, doorClose, latch, impact, wind, hum;
        readonly List<Material> materials = new List<Material>();
        Door door;
        SoundEmitter machine;
        float trainSpeed = 0.5f;
        void Start()
        {
            GameObject floor = Box("Wood floor", new Vector3(0f, -0.2f, 0f), new Vector3(14f, 0.4f, 16f), new Color(0.2f, 0.16f, 0.12f));
            floor.AddComponent<SoundSurface>().footsteps = footsteps;
            GameObject metal = Box("Metal floor", new Vector3(3f, 0.025f, 0f), new Vector3(4f, 0.05f, 12f), new Color(0.22f, 0.3f, 0.34f));
            SoundSurface surface = metal.AddComponent<SoundSurface>();
            surface.footsteps = metalFootsteps; surface.impacts = impact;
            GameObject player = new GameObject("Audio test player");
            player.layer = 7;
            player.transform.position = new Vector3(0f, 0.1f, -3f);
            CharacterController capsule = player.AddComponent<CharacterController>();
            capsule.height = 1.8f; capsule.center = Vector3.up * 0.9f;
            Camera camera = new GameObject("Player camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = Vector3.up * 1.6f;
            camera.gameObject.AddComponent<AudioListener>();
            SimpleFPSControllerNewInput controller = player.AddComponent<SimpleFPSControllerNewInput>();
            controller.cameraTransform = camera.transform;
            var playerAudio = player.AddComponent<PlayerFootstepAudio>();
            playerAudio.soundEmitter = player.GetComponent<SoundEmitter>();
            playerAudio.soundEmitter.useEnvironment = true;
            playerAudio.footstepProfile = footsteps;
            GameObject pivot = new GameObject("Test door hinge");
            pivot.transform.position = new Vector3(-1f, 0f, 2f);
            GameObject panel = Box("Door panel", Vector3.zero, new Vector3(1.4f, 2.3f, 0.15f), new Color(0.27f, 0.18f, 0.12f));
            panel.transform.SetParent(pivot.transform, false);
            panel.transform.localPosition = new Vector3(0.7f, 1.15f, 0f);
            door = pivot.AddComponent<Door>();
            door.doorRoot = pivot.transform;
            var doorAudio = pivot.AddComponent<DoorAudio>();
            doorAudio.opening.profile = doorOpen; doorAudio.closing.profile = doorClose; doorAudio.latched.profile = latch;
            GameObject room = new GameObject("Reverb zone - right side of room");
            room.transform.position = new Vector3(3f, 1.5f, 0f);
            room.AddComponent<BoxCollider>().size = new Vector3(4f, 3f, 12f);
            SoundEnvironmentZone zone = room.AddComponent<SoundEnvironmentZone>();
            zone.reverb = AudioReverbPreset.Hangar; zone.lowPass = 6000f;
            zone.calculateFromRoomSize = true;
            machine = new GameObject("Generated train hum").AddComponent<SoundEmitter>();
            machine.transform.position = new Vector3(-3f, 0.6f, 1f);
            machine.respondToTrainMotion = true;
            machine.defaultProfile = hum; machine.PlayDefault();
            SoundEmitter air = new GameObject("Generated wind").AddComponent<SoundEmitter>();
            air.transform.position = new Vector3(4f, 1.5f, 2f);
            air.defaultProfile = wind; air.volume = 0.4f; air.PlayDefault();
            Light light = new GameObject("Test lighting").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            SoundEngine.Instance.SetTrainMotion(trainSpeed, 0f, 0f);
        }
        void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.fKey.wasPressedThisFrame) door.ToggleDoor();
            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Impact audio test ball";
                ball.transform.position = new Vector3(3f, 2.5f, 0f);
                ball.transform.localScale = Vector3.one * 0.3f;
                ball.AddComponent<Rigidbody>();
                ball.AddComponent<SoundEmitter>();
                ball.AddComponent<SoundImpact>().impactProfile = impact;
                Destroy(ball, 15f);
            }
            if (Keyboard.current.upArrowKey.isPressed) trainSpeed = Mathf.Clamp01(trainSpeed + Time.deltaTime * 0.3f);
            if (Keyboard.current.downArrowKey.isPressed) trainSpeed = Mathf.Clamp01(trainSpeed - Time.deltaTime * 0.3f);
            SoundEngine.Instance.SetTrainMotion(trainSpeed, 0f, 0f);
        }
        void OnGUI()
        {
            GUI.Box(new Rect(12f, 12f, 560f, 100f), "Audio test | WASD walk | Shift sprint | Ctrl crouch\nF open/close door | B drop impact ball\nWalk onto right-hand metal floor for surface + reverb changes\nUp/Down arrows: simulated train speed " + trainSpeed.ToString("F2"));
        }
        GameObject Box(string title, Vector3 position, Vector3 scale, Color color)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = title; box.transform.position = position; box.transform.localScale = scale;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            materials.Add(material); box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }
        void OnDestroy() { foreach (Material material in materials) if (material != null) Destroy(material); }
    }
}
