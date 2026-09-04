namespace MachineService.Persistence.Entities;

public sealed class FaultEvent
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string FaultCode { get; set; }
    public required string Message { get; set; }
    public required string MachineState { get; set; }
    public string? CycleState { get; set; }
    public DateTimeOffset? ClearedAt { get; set; }
}
