using MachineService.Models;

namespace MachineService.Services;

public interface IMachineService
{
    Task<MachineStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    );

    Task<bool> InitializeAsync(
        CancellationToken cancellationToken = default
    );

    Task<bool> StartAsync(
        CancellationToken cancellationToken = default
    );

    Task<bool> PauseAsync(
        CancellationToken cancellationToken = default
    );

    Task<bool> ResumeAsync(
        CancellationToken cancellationToken = default
    );

    Task<bool> StopAsync(
        CancellationToken cancellationToken = default
    );

    Task<bool> ResetAsync(
        CancellationToken cancellationToken = default
    );

    Task<bool> EmergencyStopAsync(
        CancellationToken cancellationToken = default
    );

    Task<bool> ClearEmergencyStopAsync(
        CancellationToken cancellationToken = default
    );
    Task<bool> InjectMotionTimeoutFaultAsync(
        CancellationToken cancellationToken = default
    );
}