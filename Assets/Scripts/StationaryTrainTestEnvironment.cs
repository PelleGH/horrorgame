using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// This bootstrap builds only the dedicated test scene, at Play time.
public class StationaryTrainTestEnvironment : MonoBehaviour
{
    public bool startWithRandomJourney;
    private StationaryTrainSimulation simulatedTrain;
    private Transform train;
    private SimpleFPSControllerNewInput player;
    private CharacterController playerCollider;
    private Camera playerCamera;
    private Camera overviewCamera;
    private readonly List<Rigidbody> props = new List<Rigidbody>();
    private readonly List<Vector3> propPositions = new List<Vector3>();
    private readonly List<Material> materials = new List<Material>();
    private PhysicsMaterial grip;
    private PhysicsMaterial slippery;
    private bool overview;

    void Start()
    {
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.8f);
        Material ground = MakeMaterial(new Color(0.13f, 0.18f, 0.16f));
        Material steel = MakeMaterial(new Color(0.32f, 0.38f, 0.43f));
        Material floor = MakeMaterial(new Color(0.65f, 0.52f, 0.32f));
        Material red = MakeMaterial(new Color(0.65f, 0.12f, 0.08f));
        Material marker = MakeMaterial(new Color(0.95f, 0.75f, 0.15f));
        Material bottleMaterial = MakeMaterial(new Color(0.1f, 0.7f, 0.45f));
        Material ballMaterial = MakeMaterial(new Color(0.3f, 0.5f, 1f));
        grip = new PhysicsMaterial("Test floor friction")
            { staticFriction = 0.5f, dynamicFriction = 0.4f, bounciness = 0f };
        slippery = new PhysicsMaterial("Loose object friction")
            { staticFriction = 0.2f, dynamicFriction = 0.15f, bounciness = 0f,
              frictionCombine = PhysicsMaterialCombine.Minimum };

        Box("Lab ground", null, new Vector3(0f, -0.25f, 0f), new Vector3(24f, 0.5f, 28f), ground);
        Box("Boarding and command platform", null, new Vector3(5f, 0.8f, 0f), new Vector3(6f, 1.6f, 12f), steel);
        for (int i = -4; i <= 4; i++)
            Box("Lab floor marking", null, new Vector3(i * 2f, 0.015f, 0f), new Vector3(0.025f, 0.02f, 24f), marker);

        train = new GameObject("Stationary test carriage").transform;
        train.position = new Vector3(0f, 1.5f, 0f);
        Transform carriage = train;
        GameObject volume = new GameObject("Stationary train influence box");
        volume.transform.SetParent(carriage, false);
        BoxCollider box = volume.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 1.5f, 0f);
        box.size = new Vector3(4.3f, 4f, 10.5f);
        box.isTrigger = true;
        simulatedTrain = volume.AddComponent<StationaryTrainSimulation>();
        simulatedTrain.trainFrame = carriage;
        simulatedTrain.mode = startWithRandomJourney
            ? StationaryTrainSimulation.MotionMode.RandomJourney
            : StationaryTrainSimulation.MotionMode.Timetable;
        simulatedTrain.timetable = new[] {
            new StationaryTrainSimulation.JourneyStage { label = "Departure", duration = 10f, speed = 8f },
            new StationaryTrainSimulation.JourneyStage { label = "Right bend", duration = 8f, speed = 8f, turnDegreesPerSecond = 8f },
            new StationaryTrainSimulation.JourneyStage { label = "Left bend", duration = 8f, speed = 8f, turnDegreesPerSecond = -8f },
            new StationaryTrainSimulation.JourneyStage { label = "Hill and rough track", duration = 8f, speed = 6f, uphillDegrees = 5f, roughness = 2f },
            new StationaryTrainSimulation.JourneyStage { label = "Station stop", duration = 12f, speed = 0f }
        };
        MakeCommandPad("Red pad - emergency brake", new Vector3(3.4f, 1.62f, -2f), red,
            TrainSimulationTrigger.Command.EmergencyStop);
        MakeCommandPad("Green pad - depart", new Vector3(5.8f, 1.62f, 2f), bottleMaterial,
            TrainSimulationTrigger.Command.Start);
        Box("Carriage floor", carriage, Vector3.zero, new Vector3(4f, 0.3f, 10f), floor);
        Box("Inner side wall", carriage, new Vector3(-2f, 0.65f, 0f), new Vector3(0.15f, 1.3f, 10f), red);
        // A two-metre opening faces the boarding platform at the initial position.
        Box("Outer wall front", carriage, new Vector3(2f, 0.65f, 3f), new Vector3(0.15f, 1.3f, 4f), red);
        Box("Outer wall rear", carriage, new Vector3(2f, 0.65f, -3f), new Vector3(0.15f, 1.3f, 4f), red);
        Box("Front wall", carriage, new Vector3(0f, 0.65f, 5f), new Vector3(4f, 1.3f, 0.15f), red);
        Box("Rear wall", carriage, new Vector3(0f, 0.65f, -5f), new Vector3(4f, 1.3f, 0.15f), red);
        Box("Table top", carriage, new Vector3(0.8f, 1.05f, 1.7f), new Vector3(1.7f, 0.15f, 2.5f), floor);
        Box("Table support", carriage, new Vector3(0.8f, 0.55f, 1.7f), new Vector3(0.2f, 1f, 0.2f), steel);
        Box("Front direction marker", carriage, new Vector3(0f, 0.17f, 4.2f), new Vector3(0.8f, 0.02f, 0.4f), marker);

        for (int i = 0; i < 3; i++)
            MakeBottle(new Vector3(0.45f + i * 0.4f, 1.41f, 1.1f + i * 0.45f), bottleMaterial);
        MakeProp(PrimitiveType.Cube, "Loose crate", new Vector3(-0.9f, 0.47f, 2.8f),
            Vector3.one * 0.6f, marker, 4f);
        MakeProp(PrimitiveType.Sphere, "Rolling ball", new Vector3(0.7f, 0.38f, -1.5f),
            Vector3.one * 0.4f, ballMaterial, 0.5f);

        GameObject playerObject = new GameObject("Test player");
        // Ignore this layer in head-space checks, as in the existing controller.
        playerObject.layer = 2;
        playerCollider = playerObject.AddComponent<CharacterController>();
        playerCollider.height = 1.8f;
        playerCollider.radius = 0.28f;
        playerCollider.center = new Vector3(0f, 0.9f, 0f);
        playerCollider.skinWidth = 0.025f;
        playerCollider.stepOffset = 0.25f;
        playerObject.AddComponent<StationaryTrainPassenger>();
        GameObject cameraObject = new GameObject("Player camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(playerObject.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        playerCamera = cameraObject.AddComponent<Camera>();
        playerCamera.nearClipPlane = 0.05f;
        playerCamera.farClipPlane = 150f;
        cameraObject.AddComponent<AudioListener>();
        player = playerObject.AddComponent<SimpleFPSControllerNewInput>();
        player.cameraTransform = cameraObject.transform;
        player.moveSpeed = 3f;
        player.sprintSpeed = 5f;

        GameObject overviewObject = new GameObject("Overview camera");
        overviewCamera = overviewObject.AddComponent<Camera>();
        overviewObject.transform.position = new Vector3(9f, 15f, -14f);
        overviewObject.transform.rotation = Quaternion.Euler(42f, -24f, 0f);
        overviewCamera.orthographic = true;
        overviewCamera.orthographicSize = 9f;
        overviewCamera.enabled = false;
        GameObject sun = new GameObject("Test sunlight");
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        ResetPlayer();
        Physics.SyncTransforms();
    }

    Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { color = color };
        materials.Add(material);
        return material;
    }

    void MakeCommandPad(string label, Vector3 position, Material material, TrainSimulationTrigger.Command command)
    {
        Box(label, null, position, new Vector3(1.6f, 0.02f, 1.6f), material);
        GameObject triggerObject = new GameObject(label + " trigger");
        triggerObject.transform.position = position + Vector3.up;
        BoxCollider triggerBox = triggerObject.AddComponent<BoxCollider>();
        triggerBox.size = new Vector3(1.6f, 2f, 1.6f);
        triggerBox.isTrigger = true;
        TrainSimulationTrigger trigger = triggerObject.AddComponent<TrainSimulationTrigger>();
        trigger.simulation = simulatedTrain;
        trigger.command = command;
    }

    GameObject Box(string label, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = label;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        box.GetComponent<Collider>().sharedMaterial = grip;
        return box;
    }

    void MakeBottle(Vector3 position, Material material)
    {
        GameObject bottle = new GameObject("Loose bottle");
        GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shape.name = "Bottle body";
        shape.transform.SetParent(bottle.transform, false);
        shape.transform.localScale = new Vector3(0.2f, 0.28f, 0.2f);
        shape.GetComponent<Renderer>().sharedMaterial = material;
        shape.GetComponent<Collider>().sharedMaterial = slippery;
        GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        neck.name = "Bottle neck";
        neck.transform.SetParent(bottle.transform, false);
        neck.transform.localPosition = Vector3.up * 0.32f;
        neck.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
        neck.GetComponent<Renderer>().sharedMaterial = material;
        neck.GetComponent<Collider>().sharedMaterial = slippery;
        RegisterProp(bottle, position, 0.6f).centerOfMass = Vector3.down * 0.05f;
    }

    void MakeProp(PrimitiveType type, string label, Vector3 position, Vector3 size, Material material, float mass)
    {
        GameObject prop = GameObject.CreatePrimitive(type);
        prop.name = label;
        prop.transform.localScale = size;
        prop.GetComponent<Renderer>().sharedMaterial = material;
        prop.GetComponent<Collider>().sharedMaterial = slippery;
        RegisterProp(prop, position, mass);
    }

    Rigidbody RegisterProp(GameObject prop, Vector3 localPosition, float mass)
    {
        // Loose bodies remain world-space objects, independent of the train hierarchy.
        prop.transform.position = train.TransformPoint(localPosition);
        Rigidbody body = prop.AddComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping = 0.02f;
        body.angularDamping = 0.05f;
        body.solverIterations = 12;
        body.solverVelocityIterations = 6;
        props.Add(body);
        propPositions.Add(localPosition);
        return body;
    }

    void ResetPlayer()
    {
        playerCollider.enabled = false;
        player.transform.SetPositionAndRotation(train.TransformPoint(new Vector3(-0.8f, 0.22f, -2.5f)),
            train.rotation);
        playerCollider.enabled = true;
        player.ResetMotion();
    }

    void ResetTest()
    {
        if (simulatedTrain != null)
        {
            simulatedTrain.running = true;
            simulatedTrain.emergencyBrake = false;
            simulatedTrain.ResetJourney();
        }
        for (int i = 0; i < props.Count; i++)
        {
            props[i].position = train.TransformPoint(propPositions[i]);
            props[i].rotation = train.rotation;
            props[i].linearVelocity = Vector3.zero;
            props[i].angularVelocity = Vector3.zero;
            props[i].WakeUp();
        }
        ResetPlayer();
        Physics.SyncTransforms();
    }

    void Update()
    {
        if (train == null) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.bKey.wasPressedThisFrame)
            {
                if (simulatedTrain != null) simulatedTrain.emergencyBrake = !simulatedTrain.emergencyBrake;
            }
            if (keyboard.tKey.wasPressedThisFrame)
            {
                if (simulatedTrain != null) simulatedTrain.running = !simulatedTrain.running;
            }
            if (keyboard.rKey.wasPressedThisFrame) ResetTest();
            if (keyboard.pKey.wasPressedThisFrame)
            {
                simulatedTrain.mode = simulatedTrain.mode == StationaryTrainSimulation.MotionMode.RandomJourney
                    ? StationaryTrainSimulation.MotionMode.Timetable : StationaryTrainSimulation.MotionMode.RandomJourney;
                ResetTest();
            }
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                overview = !overview;
                player.isInspecting = overview;
                playerCamera.enabled = !overview;
                overviewCamera.enabled = overview;
            }
        }
        if (player.transform.position.y < -5f) ResetPlayer();
    }

    void OnGUI()
    {
        if (simulatedTrain == null) return;
        GUI.Box(new Rect(12f, 12f, 640f, 145f), "");
        GUI.Label(new Rect(24f, 20f, 610f, 25f), "STATIONARY TRAIN SIMULATION TEST");
        GUI.Label(new Rect(24f, 45f, 610f, 25f), "WASD / mouse / Space | B brake | T start/stop | R reset | F1 overview | P journey mode");
        GUI.Label(new Rect(24f, 70f, 610f, 25f),
            $"Virtual speed: {simulatedTrain.Speed:F1} m/s | Turn: {simulatedTrain.TurnRate:F1} deg/s | Grade: {simulatedTrain.Grade:F1} deg");
        GUI.Label(new Rect(24f, 95f, 610f, 25f), $"{simulatedTrain.mode}: {simulatedTrain.CurrentStage} | "
            + (simulatedTrain.emergencyBrake ? "EMERGENCY BRAKE" : simulatedTrain.running ? "RUNNING" : "STOPPED / STOPPING"));
        GUI.Label(new Rect(24f, 120f, 610f, 25f), "Walk through the red platform pad to brake, or the green pad to depart.");
    }
    void OnDestroy()
    {
        foreach (Material material in materials) Destroy(material);
        if (grip != null) Destroy(grip);
        if (slippery != null) Destroy(slippery);
    }
}
