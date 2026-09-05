using System.Text.Json.Serialization;

namespace WorkcellOperatorConsole.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<MachineState>))]
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

public sealed record FaultInfo(string Code, string Message);
public sealed record CycleStatus(string State, int Total, int Accepted, int Rejected);
public sealed record RobotStatus(string Position, bool Moving, bool Initialized);
public sealed record ConveyorStatus(bool Running);
public sealed record GripperStatus(bool Open);
public sealed record PartSensorStatus(bool Active);
public sealed record InspectionStatus(
    string State,
    bool? Accepted = null,
    string? Reason = null,
    string? SampleId = null,
    double? FeatureCoverage = null,
    string? Details = null
);

public sealed record MachineStatus(
    MachineState State,
    bool EmergencyStopActive,
    FaultInfo? ActiveFault,
    CycleStatus Cycle,
    RobotStatus Robot,
    ConveyorStatus Conveyor,
    GripperStatus Gripper,
    PartSensorStatus PartSensor,
    InspectionStatus? Inspection = null
);

public sealed record CommandResponse(bool Success, MachineStatus Status);
