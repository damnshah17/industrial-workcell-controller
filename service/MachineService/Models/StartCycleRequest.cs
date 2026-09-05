namespace MachineService.Models;

public sealed record StartCycleRequest(
    string? SampleId = null,
    bool? InspectionAccepted = null
);
