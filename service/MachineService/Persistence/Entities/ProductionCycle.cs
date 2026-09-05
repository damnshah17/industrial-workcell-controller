namespace MachineService.Persistence.Entities;

public sealed class ProductionCycle
{
    public Guid Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool? Accepted { get; set; }
    public long? DurationMilliseconds { get; set; }
    public required string FinalStatus { get; set; }
    public bool Faulted { get; set; }
    public string? FaultCode { get; set; }
    public string? FaultMessage { get; set; }
    public string? InspectionReason { get; set; }
    public string? InspectionSampleId { get; set; }
    public double? InspectionFeatureCoverage { get; set; }
}
