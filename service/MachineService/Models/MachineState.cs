namespace MachineService.Models;

public enum MachineState
{
    Offline,
    Initializing,
    Idle,
    Running,
    Paused,
    Faulted,
    EmergencyStop,
    Stopping
}