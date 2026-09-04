using MachineService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MachineService.Controllers;

[ApiController]
public sealed class HistoryController(IHistoryService history) : ControllerBase
{
    [HttpGet("api/events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default
    ) => Ok(await history.GetEventsAsync(NormalizePage(page), NormalizePageSize(pageSize), cancellationToken));

    [HttpGet("api/cycles")]
    public async Task<IActionResult> GetCycles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default
    ) => Ok(await history.GetCyclesAsync(NormalizePage(page), NormalizePageSize(pageSize), cancellationToken));

    [HttpGet("api/cycles/{id:guid}")]
    public async Task<IActionResult> GetCycle(Guid id, CancellationToken cancellationToken)
    {
        var cycle = await history.GetCycleAsync(id, cancellationToken);
        return cycle is null ? NotFound() : Ok(cycle);
    }

    [HttpGet("api/faults")]
    public async Task<IActionResult> GetFaults(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default
    ) => Ok(await history.GetFaultsAsync(NormalizePage(page), NormalizePageSize(pageSize), cancellationToken));

    [HttpGet("api/metrics")]
    public async Task<IActionResult> GetMetrics(CancellationToken cancellationToken) =>
        Ok(await history.GetMetricsAsync(cancellationToken));

    private static int NormalizePage(int page) => Math.Max(1, page);
    private static int NormalizePageSize(int pageSize) => Math.Clamp(pageSize, 1, 100);
}
