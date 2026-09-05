using MachineService.Models;
using MachineService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MachineService.Services;

public sealed class HistoryService(
    IDbContextFactory<WorkcellDbContext> contextFactory
) : IHistoryService
{
    public async Task<PagedResult<MachineEventDto>> GetEventsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.MachineEvents.AsNoTracking();
        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MachineEventDto(x.Id, x.Timestamp, x.EventType, x.MachineState, x.Message))
            .ToListAsync(cancellationToken);
        return new(items, page, pageSize, count);
    }

    public async Task<PagedResult<ProductionCycleDto>> GetCyclesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ProductionCycles.AsNoTracking();
        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductionCycleDto(x.Id, x.StartedAt, x.CompletedAt, x.Accepted, x.DurationMilliseconds, x.FinalStatus, x.Faulted, x.FaultCode, x.FaultMessage, x.InspectionReason, x.InspectionSampleId, x.InspectionFeatureCoverage))
            .ToListAsync(cancellationToken);
        return new(items, page, pageSize, count);
    }

    public async Task<ProductionCycleDto?> GetCycleAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ProductionCycles
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ProductionCycleDto(x.Id, x.StartedAt, x.CompletedAt, x.Accepted, x.DurationMilliseconds, x.FinalStatus, x.Faulted, x.FaultCode, x.FaultMessage, x.InspectionReason, x.InspectionSampleId, x.InspectionFeatureCoverage))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<FaultEventDto>> GetFaultsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.FaultEvents.AsNoTracking();
        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FaultEventDto(x.Id, x.Timestamp, x.FaultCode, x.Message, x.MachineState, x.CycleState, x.ClearedAt))
            .ToListAsync(cancellationToken);
        return new(items, page, pageSize, count);
    }

    public async Task<ProductionMetrics> GetMetricsAsync(
        CancellationToken cancellationToken
    )
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var completed = db.ProductionCycles
            .AsNoTracking()
            .Where(x => x.FinalStatus == "Completed");
        var total = await completed.CountAsync(cancellationToken);
        var accepted = await completed.CountAsync(x => x.Accepted == true, cancellationToken);
        var rejected = await completed.CountAsync(x => x.Accepted == false, cancellationToken);
        var average = await completed
            .Where(x => x.DurationMilliseconds.HasValue)
            .Select(x => (double?)x.DurationMilliseconds)
            .AverageAsync(cancellationToken) ?? 0;
        var faultCount = await db.FaultEvents.CountAsync(cancellationToken);

        return new(
            total,
            accepted,
            rejected,
            total == 0 ? 0 : accepted * 100.0 / total,
            average,
            faultCount
        );
    }
}
