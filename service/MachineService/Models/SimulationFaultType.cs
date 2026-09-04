namespace MachineService.Models;

public enum SimulationFaultType
{
    RobotCommunication,
    MotionTimeout,
    ConveyorStart,
    ConveyorStop,
    GripperOpen,
    GripperClose,
    Sensor,
    SafetyDoor
}
