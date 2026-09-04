using WorkcellOperatorConsole.Core.Models;

namespace WorkcellOperatorConsole.Core.Services;

public sealed class MachineCommandRejectedException(
    MachineStatus status
) : Exception("The controller rejected the command in its current state.")
{
    public MachineStatus Status { get; } = status;
}
