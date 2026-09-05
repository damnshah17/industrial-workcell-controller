namespace WorkcellOperatorConsole.Core.Models;

public sealed record ComponentHealth(string Status, string Message);

public sealed record SystemHealth(
    string Status,
    DateTimeOffset Timestamp,
    ComponentHealth Service,
    ComponentHealth Controller,
    ComponentHealth Database,
    ComponentHealth Persistence
);
