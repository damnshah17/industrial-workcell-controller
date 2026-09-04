using MachineService.Models;

namespace MachineService.Services;

public interface IHistoryService
{
    Task<PagedResult<MachineEventDto>> GetEventsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<ProductionCycleDto>> GetCyclesAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductionCycleDto?> GetCycleAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<FaultEventDto>> GetFaultsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductionMetrics> GetMetricsAsync(CancellationToken cancellationToken);
}
