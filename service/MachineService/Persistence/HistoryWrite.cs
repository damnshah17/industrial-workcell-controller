using MachineService.Models;

namespace MachineService.Persistence;

public abstract record HistoryWrite;

public sealed record MachineEventWrite(
    DateTimeOffset Timestamp,
    string EventType,
    MachineState MachineState,
    string Message
) : HistoryWrite;

public sealed record CycleStartedWrite(
    Guid Id,
    DateTimeOffset StartedAt,
    bool? Accepted,
    string? InspectionSampleId = null
) : HistoryWrite;

public sealed record CycleFinishedWrite(
    Guid Id,
    DateTimeOffset CompletedAt,
    string FinalStatus,
    bool Faulted,
    string? FaultCode,
    string? FaultMessage,
    bool? Accepted = null,
    string? InspectionReason = null,
    string? InspectionSampleId = null,
    double? InspectionFeatureCoverage = null
) : HistoryWrite;

public sealed record FaultRaisedWrite(
    Guid Id,
    DateTimeOffset Timestamp,
    string FaultCode,
    string Message,
    MachineState MachineState,
    string CycleState
) : HistoryWrite;

public sealed record FaultClearedWrite(
    Guid Id,
    DateTimeOffset ClearedAt
) : HistoryWrite;
