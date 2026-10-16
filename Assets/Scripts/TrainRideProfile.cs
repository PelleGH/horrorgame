using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Train/Ride Profile", fileName = "TrainRideProfile")]
public class TrainRideProfile : ScriptableObject
{
    [Header("Journey")]
    public StationaryTrainSimulation.MotionMode mode = StationaryTrainSimulation.MotionMode.RandomJourney;
    [Min(0f)] public float cruiseSpeed = 8f;
    [Min(0.01f)] public float acceleration = 1.5f;
    [Min(0.01f)] public float braking = 2f;
    [Min(0.01f)] public float emergencyBraking = 6f;
    [Min(0.01f)] public float turnResponse = 2f;
    [Min(0.01f)] public float gradeResponse = 1f;

    [Header("Random journey (repeatable seed)")]
    public int randomSeed = 42;
    public Vector2 randomSpeedRange = new Vector2(5f, 9f);
    public Vector2 driveDurationRange = new Vector2(20f, 40f);
    public Vector2 stationDwellRange = new Vector2(5f, 10f);
    public Vector2 bendDurationRange = new Vector2(5f, 12f);
    [Min(0f)] public float maximumRandomTurn = 8f;
    [Range(0f, 30f)] public float maximumRandomGrade = 4f;

    [Header("Timetable (stage duration includes acceleration/braking)")]
    public bool repeatTimetable = true;
    public StationaryTrainSimulation.JourneyStage[] timetable = {
        new StationaryTrainSimulation.JourneyStage { label = "Depart", duration = 12f, speed = 8f },
        new StationaryTrainSimulation.JourneyStage { label = "Right bend", duration = 12f, speed = 8f, turnDegreesPerSecond = 6f },
        new StationaryTrainSimulation.JourneyStage { label = "Hill", duration = 10f, speed = 6f, uphillDegrees = 4f },
        new StationaryTrainSimulation.JourneyStage { label = "Station", duration = 12f, speed = 0f }
    };

    [Header("Optional virtual path (train stays stationary)")]
    [Min(0f)] public float routeStartDistance;

    [Header("Manual motion / trigger targets")]
    public float manualTurnDegreesPerSecond;
    [Range(-30f, 30f)] public float manualUphillDegrees;

    [Header("Heave, vibration and banking")]
    [Min(0f)] public float bounceAmplitude = 0.02f;
    [Min(0f)] public float bounceFrequency = 0.7f;
    [Min(0f)] public float vibrationAmplitude = 0.0006f;
    [Min(0f)] public float vibrationFrequency = 7f;
    [Min(0f)] public float roughnessMultiplier = 1f;
    [Range(-1f, 1f)] public float bankDegreesPerTurnRate = 0.15f;
    public bool includeRotationalForces = true;

    public void ApplyTo(StationaryTrainSimulation simulation)
    {
        simulation.mode = mode;
        simulation.cruiseSpeed = cruiseSpeed;
        simulation.acceleration = acceleration;
        simulation.braking = braking;
        simulation.emergencyBraking = emergencyBraking;
        simulation.turnResponse = turnResponse;
        simulation.gradeResponse = gradeResponse;
        simulation.randomSeed = randomSeed;
        simulation.randomSpeedRange = randomSpeedRange;
        simulation.driveDurationRange = driveDurationRange;
        simulation.stationDwellRange = stationDwellRange;
        simulation.bendDurationRange = bendDurationRange;
        simulation.maximumRandomTurn = maximumRandomTurn;
        simulation.maximumRandomGrade = maximumRandomGrade;
        simulation.repeatTimetable = repeatTimetable;
        simulation.routeStartDistance = routeStartDistance;
        simulation.manualTurnDegreesPerSecond = manualTurnDegreesPerSecond;
        simulation.manualUphillDegrees = manualUphillDegrees;
        simulation.bounceAmplitude = bounceAmplitude;
        simulation.bounceFrequency = bounceFrequency;
        simulation.vibrationAmplitude = vibrationAmplitude;
        simulation.vibrationFrequency = vibrationFrequency;
        simulation.roughnessMultiplier = roughnessMultiplier;
        simulation.bankDegreesPerTurnRate = bankDegreesPerTurnRate;
        simulation.includeRotationalForces = includeRotationalForces;
        simulation.timetable = CloneStages(timetable);
    }

    public void CaptureFrom(StationaryTrainSimulation simulation)
    {
        mode = simulation.mode;
        cruiseSpeed = simulation.cruiseSpeed;
        acceleration = simulation.acceleration;
        braking = simulation.braking;
        emergencyBraking = simulation.emergencyBraking;
        turnResponse = simulation.turnResponse;
        gradeResponse = simulation.gradeResponse;
        randomSeed = simulation.randomSeed;
        randomSpeedRange = simulation.randomSpeedRange;
        driveDurationRange = simulation.driveDurationRange;
        stationDwellRange = simulation.stationDwellRange;
        bendDurationRange = simulation.bendDurationRange;
        maximumRandomTurn = simulation.maximumRandomTurn;
        maximumRandomGrade = simulation.maximumRandomGrade;
        repeatTimetable = simulation.repeatTimetable;
        routeStartDistance = simulation.routeStartDistance;
        manualTurnDegreesPerSecond = simulation.manualTurnDegreesPerSecond;
        manualUphillDegrees = simulation.manualUphillDegrees;
        bounceAmplitude = simulation.bounceAmplitude;
        bounceFrequency = simulation.bounceFrequency;
        vibrationAmplitude = simulation.vibrationAmplitude;
        vibrationFrequency = simulation.vibrationFrequency;
        roughnessMultiplier = simulation.roughnessMultiplier;
        bankDegreesPerTurnRate = simulation.bankDegreesPerTurnRate;
        includeRotationalForces = simulation.includeRotationalForces;
        timetable = CloneStages(simulation.timetable);
    }

    static StationaryTrainSimulation.JourneyStage[] CloneStages(StationaryTrainSimulation.JourneyStage[] stages)
    {
        if (stages == null) return new StationaryTrainSimulation.JourneyStage[0];
        return Array.ConvertAll(stages, stage => stage == null ? null
            : JsonUtility.FromJson<StationaryTrainSimulation.JourneyStage>(JsonUtility.ToJson(stage)));
    }
}