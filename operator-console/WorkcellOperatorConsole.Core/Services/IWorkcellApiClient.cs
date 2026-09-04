using WorkcellOperatorConsole.Core.Models;

namespace WorkcellOperatorConsole.Core.Services;

public interface IWorkcellApiClient
{
    Task<MachineStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<MachineStatus> SendCommandAsync(string command, CancellationToken cancellationToken = default);
    Task<MachineStatus> StartCycleAsync(bool inspectionAccepted, CancellationToken cancellationToken = default);
    Task<PagedResult<MachineEvent>> GetEventsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<ProductionCycle>> GetCyclesAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<FaultEvent>> GetFaultsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ProductionMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);
}
