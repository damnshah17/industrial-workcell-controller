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
    CycleStatus Cycle
);