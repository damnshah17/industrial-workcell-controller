using MachineService.Models;

namespace MachineService.Transport;

public interface IMachineTransport
{
    Task<ControllerResponse> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default
    );
}