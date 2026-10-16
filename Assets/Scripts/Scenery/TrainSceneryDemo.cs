using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[AddComponentMenu("Train/Train Scenery Demo")]
public class TrainSceneryDemo : MonoBehaviour
{
    public LandscapeSettings landscapeSettings;
    [Header("Ride (metres / second)")]
    [Range(0f, 40f)] public float targetSpeed = 18f;
    [Min(0.1f)] public float acceleration = 1.5f;
    [Min(0.1f)] public float braking = 2f;
    [Header("View")]
    public bool showHelp = true;
    public float mouseSensitivity = 0.1f;
    [Header("Carriage (configure before entering Play mode)")]
    public SceneryCarriageSettings carriage = new SceneryCarriageSettings();
    public bool IsReady { get; private set; }
    public RollingLandscape Landscape { get; private set; }
    public StationaryTrainSimulation Simulation { get; private set; }
    public Transform Cabin { get; private set; }
    public Transform Player { get; private set; }
    [Header("1980s winter carriage / night lighting")]
    public Color nightAmbient = new Color(.08f, .11f, .15f);
    [Range(0, 1)] public float moonIntensity = .16f;
    [Range(0, 12)] public float interiorLampIntensity = 4.2f;
    public Color interiorLampColor = new Color(.9f, .76f, .55f);
    [Tooltip("Soft window spill as a fraction of the interior lamps. Zero keeps all light inside.")]
    [Range(0, 1)] public float windowLightLeak = .45f;
    [Tooltip("Short reach of the diffuse light escaping through the windows, in metres.")]
    [Range(2, 18)] public float windowLightReach = 9f;
    public IReadOnlyList<Light> ExteriorLights => exteriorLights;
    public TrainSceneryWeather Weather { get; private set; }
    readonly List<Light> exteriorLights = new List<Light>();
    readonly List<Light> cabinLights = new List<Light>();
    Light moon;
    readonly List<Material> materials = new List<Material>();
    SimpleFPSControllerNewInput movement;
    Camera view, overview;
    LandscapeSettings runtimeSettings;
    bool externalView, cursorFree;
    bool oldFog;
    FogMode oldFogMode;
    Color oldFogColor, oldAmbient;
    AmbientMode oldAmbientMode;
    float oldStart, oldEnd;
    GUIStyle label;

    void Start()
    {
        oldFog = RenderSettings.fog; oldFogMode = RenderSettings.fogMode; oldFogColor = RenderSettings.fogColor;
        oldStart = RenderSettings.fogStartDistance; oldEnd = RenderSettings.fogEndDistance;
        oldAmbient = RenderSettings.ambientLight; oldAmbientMode = RenderSettings.ambientMode;
        runtimeSettings = landscapeSettings != null ? Instantiate(landscapeSettings) : ScriptableObject.CreateInstance<LandscapeSettings>();
        runtimeSettings.name = "Landscape settings (play-mode copy)";
        if (carriage == null) carriage = new SceneryCarriageSettings();
        carriage.Validate();
        Landscape = Child("Rolling landscape", transform, Vector3.zero).AddComponent<RollingLandscape>();
        BuildCabin(); BuildPlayer();
        Landscape.Initialize(runtimeSettings, Simulation);
        runtimeSettings.weather.carriageInteriorCenter = Vector3.up * (carriage.ceilingHeight * .5f);
        runtimeSettings.weather.carriageInteriorSize = new Vector3(carriage.interiorWidth + .4f, carriage.ceilingHeight + .6f, carriage.length + .6f);
        Weather = Child("Exterior weather", transform, Vector3.zero).AddComponent<TrainSceneryWeather>();
        Weather.Initialize(runtimeSettings, Landscape, Simulation, Cabin);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = nightAmbient;
        Light sun = Child("Cold moonlight", transform, Vector3.zero).AddComponent<Light>(); moon = sun;
        sun.type = LightType.Directional; sun.intensity = moonIntensity; sun.color = new Color(.52f, .66f, 1f);
        sun.transform.rotation = Quaternion.Euler(32f, -38f, 0f); sun.shadows = LightShadows.Soft;
        var camera = Child("Outside overview camera", transform, new Vector3(-carriage.interiorWidth * 3.1f, 10f, -carriage.length * .85f));
        overview = camera.AddComponent<Camera>(); overview.fieldOfView = 58f; overview.farClipPlane = 650f;
        overview.transform.LookAt(Cabin.position + Vector3.up); overview.enabled = false;
        ApplyAtmosphere(); Physics.SyncTransforms(); IsReady = true;
    }
    void BuildCabin()
    {
        float width = carriage.interiorWidth, length = carriage.length, height = carriage.ceilingHeight;
        float halfWidth = width * .5f, halfLength = length * .5f;
        float bayLength = (length - carriage.vestibuleLength * 2) / carriage.compartmentCount;
        Cabin = Child("Stationary windowed carriage", transform, Vector3.up * carriage.deckHeight).transform;
        Material floor = Override(carriage.floorMaterial, carriage.floorColor);
        Material wall = Override(carriage.wallMaterial, carriage.wallColor);
        Material wood = Override(carriage.woodMaterial, carriage.woodColor);
        Material cloth = Override(carriage.seatMaterial, carriage.seatColor);
        Material frame = Override(carriage.trimMaterial, carriage.trimColor);
        Material shell = Override(carriage.shellMaterial, carriage.shellColor);
        Material curtain = Override(carriage.curtainMaterial, carriage.curtainColor);
        Material radiator = Material(new Color(.22f, .24f, .22f));
        Material worn = Material(new Color(.075f, .055f, .04f));
        Box("Carriage floor", Cabin, Vector3.zero, new Vector3(width, .2f, length), floor);
        Box("Carriage ceiling", Cabin, new Vector3(0, height, 0), new Vector3(width + .22f, .16f, length + .2f), wall);
        Box("Undercarriage", Cabin, new Vector3(0, -.43f, 0), new Vector3(width - .7f, .65f, length - 1.6f), shell, false);
        Transform furniture = Child("Forward-facing compartment seating", Cabin, Vector3.zero).transform;
        Transform details = Child("Period fittings", Cabin, Vector3.zero).transform;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * (halfWidth - .04f);
            Box("Window sill wall", Cabin, new Vector3(x, carriage.windowSillHeight * .5f, 0), new Vector3(.16f, carriage.windowSillHeight, length), wood);
            Box("Upper window wall", Cabin, new Vector3(x, (height + carriage.windowTopHeight) * .5f, 0), new Vector3(.16f, height - carriage.windowTopHeight, length), wall);
            Box("Window sill", Cabin, new Vector3(side * (halfWidth - .1f), carriage.windowSillHeight, 0), new Vector3(.24f, .075f, length), frame, false);
            Box("Upper carriage cornice", details, new Vector3(side * (halfWidth - .1f), height - .13f, 0), new Vector3(.18f, .12f, length), wood, false);
            var guard = Child("Window safety collider", Cabin, new Vector3(side * halfWidth, height * .5f, 0)).AddComponent<BoxCollider>();
            guard.size = new Vector3(.12f, height, length);
            for (int bay = 0; bay < carriage.compartmentCount; bay++)
            {
                float bayStart = -halfLength + carriage.vestibuleLength + bay * bayLength;
                float windowStep = bayLength / carriage.windowsPerCompartment;
                for (int window = 0; window <= carriage.windowsPerCompartment; window++)
                {
                    float z = bayStart + window * windowStep;
                    float openingHeight = carriage.windowTopHeight - carriage.windowSillHeight;
                    Box("Window post", Cabin, new Vector3(x, (carriage.windowTopHeight + carriage.windowSillHeight) * .5f, z), new Vector3(.18f, openingHeight + .1f, .115f), frame);
                    if (carriage.curtains && window < carriage.windowsPerCompartment)
                    {
                        for (int edge = 0; edge < 2; edge++)
                        {
                            float curtainZ = z + (edge == 0 ? .14f : windowStep - .14f);
                            Box("Gathered window curtain", details, new Vector3(side * (halfWidth - .16f), (carriage.windowTopHeight + carriage.windowSillHeight) * .5f, curtainZ), new Vector3(.09f, openingHeight - .08f, .19f), curtain, false);
                            Box("Curtain tie", details, new Vector3(side * (halfWidth - .22f), carriage.windowSillHeight + .42f, curtainZ), new Vector3(.045f, .055f, .21f), frame, false);
                        }
                        Box("Curtain rail", details, new Vector3(side * (halfWidth - .19f), carriage.windowTopHeight - .015f, z + windowStep * .5f), new Vector3(.04f, .04f, windowStep), frame, false);
                    }
                    if (carriage.radiators && window < carriage.windowsPerCompartment)
                    {
                        float radiatorLength = windowStep - .4f;
                        Box("Cast iron radiator", details, new Vector3(side * (halfWidth - .16f), .35f, z + windowStep * .5f), new Vector3(.14f, .4f, radiatorLength), radiator, false);
                        int ribs = Mathf.CeilToInt(radiatorLength / .18f);
                        for (int rib = 0; rib < ribs; rib++)
                            Box("Radiator ribs", details, new Vector3(side * (halfWidth - .26f), .35f, z + .22f + rib * radiatorLength / ribs), new Vector3(.06f, .43f, .035f), worn, false);
                    }
                }
                if (carriage.luggageRacks) BuildLuggageRack(details, side, bayStart + bayLength * .5f, bayLength - .5f, height, frame);
            }
            // Closed vestibule wall panels avoid an unframed opening at either end.
            for (int end = -1; end <= 1; end += 2)
                Box("Vestibule side panel", Cabin, new Vector3(x, height * .5f, end * (halfLength - carriage.vestibuleLength * .5f)), new Vector3(.16f, height, carriage.vestibuleLength), wall);
        }
        for (int bay = 0; bay < carriage.compartmentCount; bay++)
        {
            float start = -halfLength + carriage.vestibuleLength + bay * bayLength;
            BuildSeatRows(furniture, start, bayLength, wood, cloth, frame);
            BuildCompartmentPartition(start, width, height, wood, wall, frame, bay + 1);
            float lampZ = start + bayLength * .5f;
            for (int fixture = -1; fixture <= 1; fixture += 2)
            {
                float z = lampZ + fixture * bayLength * .22f;
                Box("Ceiling light", details, new Vector3(0, height - .12f, z), new Vector3(.65f, .08f, .82f), frame, false);
                Box("Frosted lamp diffuser", details, new Vector3(0, height - .17f, z), new Vector3(.54f, .03f, .69f), wall, false);
                Light lamp = Child("Warm cabin lamp", Cabin, new Vector3(0, height - .45f, z)).AddComponent<Light>();
                lamp.type = LightType.Point; lamp.range = Mathf.Max(width + .6f, bayLength * .68f); lamp.intensity = interiorLampIntensity;
                lamp.color = interiorLampColor; lamp.shadows = LightShadows.None; cabinLights.Add(lamp);
            }
        }
        BuildCompartmentPartition(halfLength - carriage.vestibuleLength, width, height, wood, wall, frame, 0);
        // The forward aperture remains clear so the track is visible from the aisle.
        Box("Front lower wall", Cabin, new Vector3(0, .52f, halfLength), new Vector3(width + .12f, 1.04f, .18f), shell);
        Box("Front top wall", Cabin, new Vector3(0, (height + carriage.windowTopHeight) * .5f, halfLength), new Vector3(width + .12f, height - carriage.windowTopHeight, .18f), wall);
        for (int side = -1; side <= 1; side += 2)
            Box("Front corner pillar", Cabin, new Vector3(side * (halfWidth - .12f), 1.82f, halfLength), new Vector3(.28f, 1.7f, .2f), frame);
        Box("Front window mullion", Cabin, new Vector3(0, (1.04f + carriage.windowTopHeight) * .5f, halfLength), new Vector3(.075f, carriage.windowTopHeight - 1.04f, .17f), frame, false);
        var front = Child("Front window safety collider", Cabin, new Vector3(0, height * .5f, halfLength + .04f)).AddComponent<BoxCollider>();
        front.size = new Vector3(width, height, .1f);
        Box("Rear bulkhead", Cabin, new Vector3(0, height * .5f, -halfLength), new Vector3(width + .12f, height, .16f), wall);
        Box("Rear door panel", Cabin, new Vector3(0, 1.27f, -halfLength + .1f), new Vector3(1.2f, 2.35f, .05f), wood, false);
        Box("Rear door kickplate", details, new Vector3(0, .3f, -halfLength + .14f), new Vector3(1.08f, .38f, .025f), radiator, false);
        Box("Rear door handle", details, new Vector3(.44f, 1.2f, -halfLength + .19f), new Vector3(.045f, .2f, .04f), frame, false);
        if (carriage.floorboardDetails)
        {
            int boards = Mathf.CeilToInt(width / .22f);
            for (int board = 1; board < boards; board++)
                Box("Longitudinal floor board seam", details, new Vector3(-halfWidth + width * board / boards, .103f, 0), new Vector3(.009f, .004f, length - .1f), worn, false);
            for (float z = -halfLength + 1.6f; z < halfLength; z += 1.8f)
                Box("Floor board end seam", details, new Vector3(0, .104f, z), new Vector3(width - .1f, .004f, .012f), worn, false);
        }
        BuildRunningGear(shell, frame);
        BuildWindowLightLeak();
        var influence = Child("Train ride simulation", Cabin, Vector3.zero);
        var box = influence.AddComponent<BoxCollider>(); box.isTrigger = true;
        box.center = new Vector3(0, height * .5f, 0); box.size = new Vector3(width + .3f, height + .5f, length + .3f);
        Simulation = influence.AddComponent<StationaryTrainSimulation>();
        Simulation.trainFrame = Cabin; Simulation.mode = StationaryTrainSimulation.MotionMode.Manual;
        Simulation.cruiseSpeed = targetSpeed; Simulation.acceleration = acceleration; Simulation.braking = braking;
        Simulation.manualTurnDegreesPerSecond = 0; Simulation.manualUphillDegrees = 0;
        Simulation.bounceAmplitude = 0.006f; Simulation.vibrationAmplitude = 0.00015f; Simulation.affectRigidbodies = false;
        Simulation.ResetJourney();
        if (carriage.batchStaticGeometry) StaticBatchingUtility.Combine(Cabin.gameObject);
    }
    void BuildCompartmentPartition(float z, float width, float height, Material wood, Material wall, Material brass, int number)
    {
        float door = carriage.doorwayWidth, headerBottom = 2.45f;
        float panelWidth = (width - door) * .5f;
        Transform partition = Child("Compartment " + (number > 0 ? number.ToString() : "exit") + " bulkhead", Cabin, new Vector3(0, 0, z)).transform;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * (door * .5f + panelWidth * .5f);
            Box("Compartment timber partition", partition, new Vector3(x, headerBottom * .5f, 0), new Vector3(panelWidth, headerBottom, .1f), wood);
            Box("Compartment painted panel", partition, new Vector3(x, 1.75f, -.063f), new Vector3(panelWidth - .16f, .98f, .025f), wall, false);
            Box("Doorway brass trim", partition, new Vector3(side * (door * .5f + .025f), headerBottom * .5f, 0), new Vector3(.045f, headerBottom, .14f), brass, false);
            Box("Compartment number plate", partition, new Vector3(side * (door * .5f + .23f), 2.25f, -.075f), new Vector3(.19f, .09f, .025f), brass, false);
        }
        Box("Open compartment doorway header", partition, new Vector3(0, (height + headerBottom) * .5f, 0), new Vector3(width, height - headerBottom, .1f), wood);
        Box("Door threshold", partition, new Vector3(0, .11f, 0), new Vector3(door, .015f, .17f), brass, false);
    }
    void BuildSeatRows(Transform parent, float bayStart, float bayLength, Material wood, Material cloth, Material brass)
    {
        float margin = Mathf.Min(carriage.rowEndClearance, bayLength * .23f);
        int rows = Mathf.Max(1, Mathf.FloorToInt((bayLength - margin * 2) / carriage.seatPitch) + 1);
        if (carriage.maximumRowsPerCompartment > 0) rows = Mathf.Min(rows, carriage.maximumRowsPerCompartment);
        float bankWidth = (carriage.interiorWidth - carriage.aisleWidth) * .5f;
        float width = Mathf.Min(carriage.seatWidth, (bankWidth - .16f - .055f * (carriage.seatsPerSide - 1)) / carriage.seatsPerSide);
        for (int row = 0; row < rows; row++)
        {
            float z = bayStart + margin + row * carriage.seatPitch;
            for (int side = -1; side <= 1; side += 2)
                for (int seat = 0; seat < carriage.seatsPerSide; seat++)
                {
                    float x = side * (carriage.aisleWidth * .5f + .08f + width * .5f + seat * (width + .055f));
                    Transform chair = Child("Forward-facing passenger seat", parent, new Vector3(x, 0, z)).transform;
                    if (carriage.seatPrefab != null) { Instantiate(carriage.seatPrefab, chair, false); continue; }
                    float depth = carriage.seatDepth;
                    Box("Seat plinth", chair, new Vector3(0, .30f, -.02f), new Vector3(width * .75f, .4f, depth * .68f), wood);
                    Box("Seat cushion", chair, new Vector3(0, .59f, .015f), new Vector3(width, .18f, depth), cloth);
                    Box("Seat back facing +Z", chair, new Vector3(0, 1.04f, -depth * .43f), new Vector3(width, .95f, .16f), cloth);
                    Box("Upholstered headrest", chair, new Vector3(0, 1.49f, -depth * .4f), new Vector3(width * .82f, .19f, .22f), cloth, false);
                    Box("Seat back timber rail", chair, new Vector3(0, 1.52f, -depth * .51f), new Vector3(width + .025f, .055f, .07f), wood, false);
                    for (int arm = -1; arm <= 1; arm += 2)
                    {
                        Box("Armrest support", chair, new Vector3(arm * width * .47f, .73f, .04f), new Vector3(.035f, .31f, .035f), brass, false);
                        Box("Polished timber armrest", chair, new Vector3(arm * width * .47f, .90f, .015f), new Vector3(.065f, .075f, depth * .72f), wood, false);
                    }
                    Box("Seat back pocket", chair, new Vector3(0, .97f, -depth * .53f), new Vector3(width * .65f, .26f, .035f), wood, false);
                }
        }
    }
    void BuildLuggageRack(Transform parent, int side, float z, float length, float height, Material brass)
    {
        float outerX = carriage.interiorWidth * .5f - .18f, innerX = outerX - .72f, y = height - .49f;
        Box("Luggage rack front", parent, new Vector3(side * innerX, y, z), new Vector3(.045f, .07f, length), brass, false);
        int bars = Mathf.CeilToInt(length / .29f);
        for (int bar = 0; bar <= bars; bar++)
            Box("Luggage rack bars", parent, new Vector3(side * (outerX + innerX) * .5f, y - .015f, z - length * .5f + bar * length / bars), new Vector3(outerX - innerX, .035f, .035f), brass, false);
        for (int bracket = -1; bracket <= 1; bracket += 2)
            Box("Luggage rack wall bracket", parent, new Vector3(side * outerX, y + .14f, z + bracket * length * .38f), new Vector3(.045f, .32f, .055f), brass, false);
    }
    void BuildRunningGear(Material shell, Material frame)
    {
        Transform gear = Child("Running gear", Cabin, Vector3.zero).transform;
        for (int end = -1; end <= 1; end += 2)
        {
            float z = end * carriage.length * .32f;
            Box("Bogie frame", gear, new Vector3(0, -.72f, z), new Vector3(2.7f, .36f, 2.7f), shell, false);
            for (int axle = -1; axle <= 1; axle += 2)
            {
                Box("Wheel axle", gear, new Vector3(0, -.94f, z + axle * .86f), new Vector3(3f, .16f, .16f), frame, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wheel.name = "Steel railway wheel"; wheel.transform.SetParent(gear, false);
                    wheel.transform.localPosition = new Vector3(side * 1.25f, -.94f, z + axle * .86f);
                    wheel.transform.localRotation = Quaternion.Euler(0, 0, 90); wheel.transform.localScale = new Vector3(.66f, .1f, .66f);
                    wheel.GetComponent<Renderer>().sharedMaterial = shell; wheel.GetComponent<Collider>().enabled = false; Destroy(wheel.GetComponent<Collider>());
                }
            }
        }
    }
    void BuildWindowLightLeak()
    {
        // These soft point sources approximate light escaping from the lit rooms.
        // They have no visible outdoor fixtures or forward-facing headlight cones.
        int count = Mathf.Min(carriage.compartmentCount, SceneryLighting.MaximumBeams / 2);
        float passengerLength = carriage.length - carriage.vestibuleLength * 2;
        float windowHeight = (carriage.windowSillHeight + carriage.windowTopHeight) * .5f;
        for (int side=-1;side<=1;side+=2)
            for (int bay=0;bay<count;bay++)
            {
                float z = -passengerLength * .5f + passengerLength * (bay+.5f)/count;
                Light spill = Child("Soft window light spill",Cabin,new Vector3(side*(carriage.interiorWidth*.5f+.07f),windowHeight,z)).AddComponent<Light>();
                spill.transform.localRotation = Quaternion.Euler(0,side*90,0);
                spill.type = LightType.Point; spill.color = interiorLampColor;
                spill.range = windowLightReach; spill.intensity = interiorLampIntensity * windowLightLeak;
                spill.shadows = LightShadows.None; exteriorLights.Add(spill);
            }
    }
    void BuildPlayer()
    {
        Player = Child("Scenery player", transform, new Vector3(0, carriage.deckHeight + .15f, -carriage.length * .33f)).transform; Player.gameObject.layer = 7; Player.tag = "Player";
        Player.localRotation = Quaternion.Euler(0, carriage.startingViewYaw, 0);
        var body = Player.gameObject.AddComponent<CharacterController>();
        body.height = 1.8f; body.radius = 0.28f; body.center = Vector3.up * 0.9f; body.skinWidth = 0.025f;
        var passenger = Player.gameObject.AddComponent<StationaryTrainPassenger>(); passenger.simulation = Simulation;
        view = Child("Player camera", Player, Vector3.up * 1.6f).AddComponent<Camera>();
        view.tag = "MainCamera"; view.fieldOfView = 72f; view.nearClipPlane = 0.04f; view.farClipPlane = 650f;
        view.gameObject.AddComponent<AudioListener>();
        movement = Player.gameObject.AddComponent<SimpleFPSControllerNewInput>();
        movement.cameraTransform = view.transform; movement.moveSpeed = 2.6f; movement.sprintSpeed = 4f;
        movement.mouseSensitivity = mouseSensitivity;
    }
    public void SetTargetSpeed(float speed) { targetSpeed = Mathf.Clamp(speed, 0, 40); if (Simulation != null) Simulation.SetCruiseSpeed(targetSpeed); }
    public void ToggleRunning()
    {
        if (Simulation == null) return;
        if (Simulation.running && !Simulation.emergencyBrake) Simulation.StopTrain(); else Simulation.StartTrain();
    }
    public void Regenerate()
    {
        if (!IsReady) return;
        Landscape.GenerateNewJourney(); ApplyAtmosphere();
    }
    void Update()
    {
        if (!IsReady) return;
        Simulation.cruiseSpeed = targetSpeed; Simulation.acceleration = Mathf.Max(0.1f, acceleration); Simulation.braking = Mathf.Max(0.1f, braking);
        movement.mouseSensitivity = mouseSensitivity;
        var key = Keyboard.current; if (key == null) return;
        if (key.upArrowKey.isPressed) SetTargetSpeed(targetSpeed + Time.deltaTime * 4f);
        if (key.downArrowKey.isPressed) SetTargetSpeed(targetSpeed - Time.deltaTime * 4f);
        if (key.leftBracketKey.wasPressedThisFrame || key.rightBracketKey.wasPressedThisFrame)
        {
            runtimeSettings.route.turnStrength = Mathf.Clamp(runtimeSettings.route.turnStrength + (key.rightBracketKey.wasPressedThisFrame ? .25f : -.25f), 0, 4);
            Landscape.Rebuild();
        }
        if (key.tKey.wasPressedThisFrame) ToggleRunning();
        if (key.bKey.wasPressedThisFrame) Simulation.EmergencyStop();
        if (key.rKey.wasPressedThisFrame) Regenerate();
        if (key.nKey.wasPressedThisFrame && Weather != null)
        {
            if (!Weather.ManualWeather) Weather.SetManualWeather(SceneryWeatherKind.Rain);
            else if (Weather.ManualKind == SceneryWeatherKind.Rain) Weather.SetManualWeather(SceneryWeatherKind.Clear);
            else if (Weather.ManualKind == SceneryWeatherKind.Clear) Weather.SetManualWeather(SceneryWeatherKind.Snow);
            else Weather.ResumeProceduralWeather();
        }
        if (key.hKey.wasPressedThisFrame) showHelp = !showHelp;
        if (key.f1Key.wasPressedThisFrame) { externalView = !externalView; view.enabled = !externalView; overview.enabled = externalView; }
        if (key.escapeKey.wasPressedThisFrame) cursorFree = !cursorFree;
        movement.isInspecting = externalView || cursorFree;
        Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = cursorFree;
    }
    void LateUpdate()
    {
        if (!IsReady) return;
        RenderSettings.ambientLight = nightAmbient;
        if (moon != null) moon.intensity = moonIntensity;
        foreach (Light lamp in cabinLights) { lamp.intensity = interiorLampIntensity; lamp.color = interiorLampColor; }
        foreach (Light light in exteriorLights) { light.intensity = interiorLampIntensity * windowLightLeak; light.range = windowLightReach; light.color = interiorLampColor; }
        Landscape.SetSceneLighting(nightAmbient, exteriorLights);
        if (Weather != null) Weather.SetLighting(nightAmbient, exteriorLights);
        ApplyAtmosphere();
    }
    void ApplyAtmosphere()
    {
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = runtimeSettings.fog;
        float coverage = Mathf.Min(runtimeSettings.halfWidth, (Landscape.ActiveChunkCount / 2) * Mathf.Max(30f, runtimeSettings.chunkLength)) - 20f;
        RenderSettings.fogEndDistance = Mathf.Clamp(runtimeSettings.fogEnd * (Weather != null ? Weather.VisibilityMultiplier : 1), 40f, Mathf.Max(40f, coverage));
        RenderSettings.fogStartDistance = Mathf.Clamp(runtimeSettings.fogStart * (Weather != null ? Weather.VisibilityMultiplier : 1), 10f, RenderSettings.fogEndDistance - 20f);
        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = runtimeSettings.fog;
        if (overview != null) { overview.clearFlags = CameraClearFlags.SolidColor; overview.backgroundColor = runtimeSettings.fog; }
    }
    GameObject Child(string name, Transform parent, Vector3 local)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = local; return go;
    }
    Material Material(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) {color = color};
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .18f);
        materials.Add(material); return material;
    }
    Material Override(Material authored, Color fallback) => authored != null ? authored : Material(fallback);
    void OnValidate()
    {
        if (carriage == null) carriage = new SceneryCarriageSettings();
        carriage.Validate();
    }
    void Box(string name, Transform parent, Vector3 local, Vector3 scale, Material material, bool solid = true)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent, false);
        box.transform.localPosition = local; box.transform.localScale = scale; box.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) { box.GetComponent<Collider>().enabled = false; Destroy(box.GetComponent<Collider>()); }
    }
    void OnGUI()
    {
        if (!IsReady || !showHelp) return;
        if (label == null) label = new GUIStyle(GUI.skin.label) {fontSize = 14, wordWrap = true};
        GUI.Box(new Rect(14, 14, 470, 242), GUIContent.none);
        GUI.Label(new Rect(26, 22, 445, 232), "NIGHT TRAIN / BOREAL WINTER\n" +
            "WASD + mouse / Shift sprint / Ctrl crouch\nUp / Down: speed   T: stop / depart   B: brake\nR: new journey   F1: exterior view   [ / ]: bends\nN: preview rain / clear / snow / procedural   H: help\n\n" +
            "Speed " + (Simulation.Speed * 3.6f).ToString("F0") + " km/h | target " + (targetSpeed * 3.6f).ToString("F0") +
            "\nDistance " + Landscape.DistanceTravelled.ToString("F0") + " m | sections " + Landscape.ActiveChunkCount +
            " | recycled " + Landscape.RecycledChunks +
            "\nSeed " + Landscape.ActiveSeed + " | biome " + (Landscape.CurrentBiome != null ? Landscape.CurrentBiome.name : "Default") +
            "\nTurn " + Simulation.TurnRate.ToString("F1") + " deg/s | grade " + Simulation.Grade.ToString("F1") + " deg" +
            " | bends " + runtimeSettings.route.turnStrength.ToString("F2") +
            "\nWeather: " + (Weather != null ? Weather.CurrentKind.ToString() + (Weather.ManualWeather ? " (preview)" : " (procedural)") : "None"), label);
    }
    void OnDestroy()
    {
        foreach (Material material in materials) if (material != null) Destroy(material);
        if (runtimeSettings != null) Destroy(runtimeSettings);
        RenderSettings.fog = oldFog; RenderSettings.fogMode = oldFogMode; RenderSettings.fogColor = oldFogColor;
        RenderSettings.fogStartDistance = oldStart; RenderSettings.fogEndDistance = oldEnd;
        RenderSettings.ambientMode = oldAmbientMode; RenderSettings.ambientLight = oldAmbient;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
}
