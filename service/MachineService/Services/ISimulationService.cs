using MachineService.Models;

namespace MachineService.Services;

public interface ISimulationService
{
    Task<(bool Success, MachineStatus Status)> ConfigureFaultAsync(
        SimulationFaultType faultType,
        bool enabled,
        CancellationToken cancellationToken = default
    );

    Task<(bool Success, MachineStatus Status)> ClearAllFaultsAsync(
        CancellationToken cancellationToken = default
    );
}
