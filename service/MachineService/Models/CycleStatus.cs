namespace MachineService.Models;

public sealed record CycleStatus(
    string State,
    int Total,
    int Accepted,
    int Rejected
);