namespace MachineService.Models;

public sealed record MachineStatus(
    MachineState State,
    bool EmergencyStopActive,
    FaultInfo? ActiveFault,
    CycleStatus Cycle,
    RobotStatus Robot,
    ConveyorStatus Conveyor,
    GripperStatus Gripper,
    PartSensorStatus PartSensor
);
