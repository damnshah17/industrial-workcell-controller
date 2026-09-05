namespace WorkcellOperatorConsole.Core.Models;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public sealed record MachineEvent(
    long Id,
    DateTimeOffset Timestamp,
    string EventType,
    string MachineState,
    string Message
);

public sealed record ProductionCycle(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool? Accepted,
    long? DurationMilliseconds,
    string FinalStatus,
    bool Faulted,
    string? FaultCode,
    string? FaultMessage,
    string? InspectionReason = null,
    string? InspectionSampleId = null,
    double? InspectionFeatureCoverage = null
)
{
    public string Result => Accepted switch
    {
        true => "ACCEPTED",
        false => "REJECTED",
        null => "—"
    };
}

public sealed record FaultEvent(
    Guid Id,
    DateTimeOffset Timestamp,
    string FaultCode,
    string Message,
    string MachineState,
    string? CycleState,
    DateTimeOffset? ClearedAt
);

public sealed record ProductionMetrics(
    int TotalCycles,
    int AcceptedCycles,
    int RejectedCycles,
    double AcceptanceRate,
    double AverageCycleDurationMilliseconds,
    int FaultCount
);
