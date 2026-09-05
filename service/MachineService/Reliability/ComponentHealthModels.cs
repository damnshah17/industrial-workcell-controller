namespace MachineService.Reliability;

public enum ComponentStatus { Healthy, Degraded, Unhealthy }

public sealed record ComponentHealth(
    ComponentStatus Status,
    string Message,
    IReadOnlyDictionary<string, object?>? Details = null
);

public sealed record SystemHealth(
    ComponentStatus Status,
    DateTimeOffset Timestamp,
    ComponentHealth Service,
    ComponentHealth Controller,
    ComponentHealth Database,
    ComponentHealth Persistence
);

public sealed record ControllerTransportHealth(
    ComponentStatus Status,
    string Message,
    int? ProcessId,
    int RestartCount,
    DateTimeOffset? LastConnectedAt,
    string? LastError
);
