namespace MachineService.Models;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public sealed record MachineEventDto(
    long Id,
    DateTimeOffset Timestamp,
    string EventType,
    string MachineState,
    string Message
);

public sealed record ProductionCycleDto(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool? Accepted,
    long? DurationMilliseconds,
    string FinalStatus,
    bool Faulted,
    string? FaultCode,
    string? FaultMessage
);

public sealed record FaultEventDto(
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
