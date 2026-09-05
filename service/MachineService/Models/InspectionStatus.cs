namespace MachineService.Models;

public sealed record InspectionStatus(
    string State,
    bool? Accepted = null,
    string? Reason = null,
    string? SampleId = null,
    double? FeatureCoverage = null,
    string? Details = null
);
