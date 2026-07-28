namespace MachineService.Models;

public sealed record MachineStatus(
    MachineState State,
    bool EmergencyStopActive,
    FaultInfo? ActiveFault,
    CycleStatus Cycle
);