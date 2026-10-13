using System;
using UnityEditor;
using UnityEngine;

public static class TrainSetupEditor
{
    [MenuItem("GameObject/Train/Add Stationary Simulation to Selected Train", false, 1)]
    public static void AddStationarySimulation()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;
        Rigidbody carriage = selected.GetComponent<Rigidbody>();
        if (carriage != null)
        {
            Undo.RecordObject(carriage, "Make stationary carriage kinematic");
            carriage.isKinematic = true;
        }
        GameObject volume = new GameObject("Stationary train influence box");
        Undo.RegisterCreatedObjectUndo(volume, "Create stationary train simulation");
        volume.transform.SetParent(selected.transform, false);
        BoxCollider box = Undo.AddComponent<BoxCollider>(volume);
        Renderer[] renderers = selected.GetComponentsInChildren<Renderer>();
        bool found = false;
        Bounds localBounds = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                Vector3 local = selected.transform.InverseTransformPoint(world);
                if (!found) { localBounds = new Bounds(local, Vector3.zero); found = true; }
                else localBounds.Encapsulate(local);
            }
        }
        box.center = found ? localBounds.center : new Vector3(0f, 1.5f, 0f);
        box.size = found ? localBounds.size + Vector3.one * 0.3f : new Vector3(4f, 4f, 10f);
        box.isTrigger = true;
        StationaryTrainSimulation simulation = Undo.AddComponent<StationaryTrainSimulation>(volume);
        simulation.trainFrame = selected.transform;
        Selection.activeGameObject = volume;
        EditorUtility.SetDirty(simulation);
    }

    [MenuItem("GameObject/Train/Add Stationary Simulation to Selected Train", true)]
    static bool CanAddStationarySimulation() => Selection.activeGameObject != null;

    [MenuItem("GameObject/Train/Add Stationary Passenger to Selected Player", false, 2)]
    public static void AddStationaryPassenger()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || selected.GetComponent<CharacterController>() == null) return;
        if (selected.GetComponent<StationaryTrainPassenger>() == null)
            Undo.AddComponent<StationaryTrainPassenger>(selected);
    }

    [MenuItem("GameObject/Train/Add Stationary Passenger to Selected Player", true)]
    static bool CanAddStationaryPassenger() => Selection.activeGameObject != null
        && Selection.activeGameObject.GetComponent<CharacterController>() != null;

    [MenuItem("GameObject/Train/Create Train and Route", false, 10)]
    public static void CreateTrainAndRoute()
    {
        TrainRoute route = CreateRoute();
        GameObject root = new GameObject("My Train");
        Undo.RegisterCreatedObjectUndo(root, "Create train rig");
        Rigidbody body = Undo.AddComponent<Rigidbody>(root);
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        TrainPathFollower follower = Undo.AddComponent<TrainPathFollower>(root);
        follower.route = route;
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(floor, "Create train floor");
        floor.name = "Placeholder floor - replace with your design";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localScale = new Vector3(4f, 0.3f, 10f);
        follower.ResetToRouteStart();
        Selection.activeGameObject = root;
        EditorUtility.SetDirty(follower);
    }

    [MenuItem("GameObject/Train/Create Route Only", false, 11)]
    public static void CreateRouteOnly() => CreateRoute();

    public static TrainRoute CreateRoute()
    {
        GameObject root = new GameObject("Train Route");
        Undo.RegisterCreatedObjectUndo(root, "Create train route");
        TrainRoute route = Undo.AddComponent<TrainRoute>(root);
        route.waypoints = new Transform[8];
        for (int i = 0; i < route.waypoints.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / route.waypoints.Length;
            GameObject point = new GameObject($"Waypoint {i + 1:00}");
            Undo.RegisterCreatedObjectUndo(point, "Create route waypoint");
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = new Vector3(Mathf.Cos(angle) * 24f, 1.5f, Mathf.Sin(angle) * 24f);
            route.waypoints[i] = point.transform;
        }
        route.Rebuild();
        Selection.activeGameObject = root;
        EditorUtility.SetDirty(route);
        return route;
    }

    [MenuItem("GameObject/Train/Make Selected Object Loose", false, 12)]
    public static void MakeSelectedObjectLoose()
    {
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected.GetComponent<Rigidbody>() == null) Undo.AddComponent<Rigidbody>(selected);
            LooseTrainObject settings = selected.GetComponent<LooseTrainObject>();
            if (settings == null) settings = Undo.AddComponent<LooseTrainObject>(selected);
            Undo.RecordObject(selected.GetComponent<Rigidbody>(), "Configure loose train object");
            settings.ApplySettings();
            EditorUtility.SetDirty(settings);
        }
    }

    [MenuItem("GameObject/Train/Make Selected Object Loose", true)]
    static bool CanMakeLoose() => Selection.gameObjects.Length > 0;
}

[CustomEditor(typeof(StationaryTrainSimulation))]
public class StationaryTrainSimulationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        StationaryTrainSimulation simulation = (StationaryTrainSimulation)target;
        EditorGUILayout.HelpBox("This train stays stationary. Edit the BoxCollider to cover the carriage interior. "
            + "Dynamic props with gravity inside it receive inertia automatically. Add Stationary Train Passenger "
            + "to your player. +Z of Train Frame is forward.", MessageType.Info);
        Transform frame = simulation.trainFrame != null ? simulation.trainFrame : simulation.transform;
        if (frame.GetComponent<TrainPathFollower>() != null && frame.GetComponent<TrainPathFollower>().enabled)
            EditorGUILayout.HelpBox("Disable the physical train movement component before using this stationary simulation.", MessageType.Warning);
        if (GUILayout.Button("Reset Journey")) simulation.ResetJourney();
        if (!Application.isPlaying && GUILayout.Button("Create Command Trigger Box"))
        {
            GameObject triggerObject = new GameObject("Train command trigger - position this box");
            Undo.RegisterCreatedObjectUndo(triggerObject, "Create train command trigger");
            triggerObject.transform.SetPositionAndRotation(frame.position + frame.forward * 3f,
                frame.rotation);
            BoxCollider box = Undo.AddComponent<BoxCollider>(triggerObject);
            box.size = Vector3.one * 2f;
            box.isTrigger = true;
            TrainSimulationTrigger trigger = Undo.AddComponent<TrainSimulationTrigger>(triggerObject);
            trigger.simulation = simulation;
            Selection.activeGameObject = triggerObject;
            EditorUtility.SetDirty(trigger);
        }
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Stage", simulation.CurrentStage);
            EditorGUILayout.LabelField("Virtual speed", $"{simulation.Speed:F2} m/s");
            EditorGUILayout.LabelField("Turn / grade", $"{simulation.TurnRate:F1}°/s / {simulation.Grade:F1}°");
            EditorGUILayout.LabelField("Virtual distance", $"{simulation.TravelDistance:F1} m");
            if (GUILayout.Button("Start / Release Brake")) simulation.StartTrain();
            if (GUILayout.Button("Stop")) simulation.StopTrain();
            if (GUILayout.Button("Emergency Brake")) simulation.EmergencyStop();
        }
    }
}

[CustomEditor(typeof(TrainRoute))]
public class TrainRouteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        TrainRoute route = (TrainRoute)target;
        EditorGUILayout.HelpBox("Move waypoint objects with the Scene move tool. Array order defines travel order. "
            + "Route points mark the train root pivot; keep the route outside the train hierarchy.", MessageType.Info);
        if (GUILayout.Button("Add Waypoint"))
        {
            Undo.RecordObject(route, "Add train waypoint");
            Transform[] points = route.waypoints ?? Array.Empty<Transform>();
            GameObject point = new GameObject($"Waypoint {points.Length + 1:00}");
            Undo.RegisterCreatedObjectUndo(point, "Add train waypoint");
            point.transform.SetParent(route.transform, false);
            point.transform.position = points.Length > 0 && points[points.Length - 1] != null
                ? points[points.Length - 1].position + Vector3.forward * 5f : route.transform.position;
            Array.Resize(ref points, points.Length + 1);
            points[points.Length - 1] = point.transform;
            route.waypoints = points;
            route.Rebuild();
            EditorUtility.SetDirty(route);
            Selection.activeGameObject = point;
        }
        if (GUILayout.Button("Use Child Objects as Waypoints (Hierarchy Order)"))
        {
            Undo.RecordObject(route, "Assign train waypoints");
            route.waypoints = new Transform[route.transform.childCount];
            for (int i = 0; i < route.waypoints.Length; i++) route.waypoints[i] = route.transform.GetChild(i);
            route.Rebuild();
            EditorUtility.SetDirty(route);
        }
        if (GUILayout.Button("Rebuild Route")) route.Rebuild();
        EditorGUILayout.LabelField("Approximate length", $"{route.Length:F1} m");
        if (!route.IsValid)
            EditorGUILayout.HelpBox("Assign at least 2 points for an open route, or 3 for a loop. "
                + "The route must have nonzero length.", MessageType.Warning);
    }
}

[CustomEditor(typeof(TrainPathFollower))]
public class TrainPathFollowerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        TrainPathFollower train = (TrainPathFollower)target;
        if (train.route == null)
            EditorGUILayout.HelpBox("Assign a Train Route to move this train.", MessageType.Warning);
        if (train.transform.lossyScale != Vector3.one)
            EditorGUILayout.HelpBox("Use scale (1,1,1) on the train root. Scale and rotate your model child instead.", MessageType.Warning);
        if (GUILayout.Button("Place Train at Route Start"))
        {
            Undo.RecordObjects(new UnityEngine.Object[] { train.transform, train.GetComponent<Rigidbody>() }, "Place train on route");
            train.ResetToRouteStart();
            EditorUtility.SetDirty(train.transform);
        }
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Current speed", $"{train.Speed:F2} m/s");
            EditorGUILayout.LabelField("Route distance", $"{train.Distance:F1} m");
            if (GUILayout.Button("Start / Release Brake")) train.StartTrain();
            if (GUILayout.Button("Stop Normally")) train.StopTrain();
            if (GUILayout.Button("Emergency Stop")) train.EmergencyStop();
        }
    }
}
