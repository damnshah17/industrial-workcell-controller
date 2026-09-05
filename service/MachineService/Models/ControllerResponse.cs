using System.Text.Json.Serialization;

namespace MachineService.Models;

public sealed record ControllerResponse(
    [property: JsonPropertyName("success")]
    bool Success,

    [property: JsonPropertyName("state")]
    MachineState State,

    [property: JsonPropertyName("emergencyStopActive")]
    bool EmergencyStopActive,

    [property: JsonPropertyName("hasActiveFault")]
    bool HasActiveFault,

    [property: JsonPropertyName("fault")]
    FaultInfo? Fault,

    [property: JsonPropertyName("cycle")]
    CycleStatus Cycle,

    [property: JsonPropertyName("robot")]
    RobotStatus Robot,

    [property: JsonPropertyName("conveyor")]
    ConveyorStatus Conveyor,

    [property: JsonPropertyName("gripper")]
    GripperStatus Gripper,

    [property: JsonPropertyName("partSensor")]
    PartSensorStatus PartSensor,

    [property: JsonPropertyName("inspection")]
    InspectionStatus? Inspection = null
);
