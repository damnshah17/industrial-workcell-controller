namespace MachineService.Persistence.Entities;

public sealed class MachineEvent
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string EventType { get; set; }
    public required string MachineState { get; set; }
    public required string Message { get; set; }
}
