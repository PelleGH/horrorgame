using UnityEngine;

[AddComponentMenu("Train/Train Simulation Trigger")]
[RequireComponent(typeof(BoxCollider))]
public class TrainSimulationTrigger : MonoBehaviour
{
    public enum Command { Start, Stop, EmergencyStop, SetSpeed, SetTurn, SetGrade }
    public StationaryTrainSimulation simulation;
    public Command command = Command.Stop;
    public float value = 8f;
    public bool onlyCharacterControllers = true;
    public LayerMask actorLayers = ~0;
    public bool onceOnly;
    [Tooltip("For speed/turn/grade commands, switch to Manual so the journey does not override the setting.")]
    public bool switchToManual = true;
    private bool used;

    void Awake() => GetComponent<BoxCollider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        if ((actorLayers.value & (1 << other.gameObject.layer)) == 0) return;
        if (onlyCharacterControllers && other.GetComponentInParent<CharacterController>() == null) return;
        Activate();
    }

    // Can also be called by UnityEvents, buttons, or other gameplay scripts.
    public void Activate()
    {
        if (simulation == null || (onceOnly && used)) return;
        used = true;
        if (switchToManual && (command == Command.SetSpeed || command == Command.SetTurn || command == Command.SetGrade))
            simulation.mode = StationaryTrainSimulation.MotionMode.Manual;
        switch (command)
        {
            case Command.Start: simulation.StartTrain(); break;
            case Command.Stop: simulation.StopTrain(); break;
            case Command.EmergencyStop: simulation.EmergencyStop(); break;
            case Command.SetSpeed: simulation.SetCruiseSpeed(value); break;
            case Command.SetTurn: simulation.SetManualTurn(value); break;
            case Command.SetGrade: simulation.SetManualGrade(value); break;
        }
    }
    public void ResetTrigger() => used = false;
}
