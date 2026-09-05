using MachineService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MachineService.Persistence;

public sealed class HistoryWriterService(
    HistoryWriteQueue queue,
    IDbContextFactory<WorkcellDbContext> contextFactory,
    PersistenceHealthState health,
    ILogger<HistoryWriterService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken
    )
    {
        await foreach (
            var write in queue.ReadAllAsync(stoppingToken)
        )
        {
            try
            {
                await PersistAsync(write, stoppingToken);
                health.RecordSuccess();
            }
            catch (OperationCanceledException) when (
                stoppingToken.IsCancellationRequested
            )
            {
                return;
            }
            catch (Exception exception)
            {
                health.RecordFailure(exception);
                logger.LogError(
                    exception,
                    "Failed to persist operational history record {RecordType}; controller operation is unaffected.",
                    write.GetType().Name
                );
            }
        }
    }

    private async Task PersistAsync(
        HistoryWrite write,
        CancellationToken cancellationToken
    )
    {
        await using var db =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        switch (write)
        {
            case MachineEventWrite item:
                db.MachineEvents.Add(new MachineEvent
                {
                    Timestamp = item.Timestamp,
                    EventType = item.EventType,
                    MachineState = item.MachineState.ToString(),
                    Message = item.Message
                });
                break;

            case CycleStartedWrite item:
                db.ProductionCycles.Add(new ProductionCycle
                {
                    Id = item.Id,
                    StartedAt = item.StartedAt,
                    Accepted = item.Accepted,
                    InspectionSampleId = item.InspectionSampleId,
                    FinalStatus = "Running"
                });
                break;

            case CycleFinishedWrite item:
                var cycle = await db.ProductionCycles.FindAsync(
                    [item.Id],
                    cancellationToken
                );
                cycle ??= new ProductionCycle
                {
                    Id = item.Id,
                    StartedAt = item.CompletedAt,
                    FinalStatus = item.FinalStatus
                };
                if (cycle.Id != Guid.Empty && db.Entry(cycle).State == EntityState.Detached)
                {
                    db.ProductionCycles.Add(cycle);
                }
                cycle.CompletedAt = item.CompletedAt;
                cycle.DurationMilliseconds = Math.Max(
                    0,
                    (long)(item.CompletedAt - cycle.StartedAt).TotalMilliseconds
                );
                cycle.FinalStatus = item.FinalStatus;
                cycle.Faulted = item.Faulted;
                if (item.FinalStatus != "Completed")
                {
                    cycle.Accepted = null;
                }
                cycle.FaultCode = item.FaultCode;
                cycle.FaultMessage = item.FaultMessage;
                cycle.Accepted = item.FinalStatus == "Completed"
                    ? item.Accepted ?? cycle.Accepted
                    : null;
                cycle.InspectionReason = item.InspectionReason;
                cycle.InspectionSampleId = item.InspectionSampleId ?? cycle.InspectionSampleId;
                cycle.InspectionFeatureCoverage = item.InspectionFeatureCoverage;
                break;

            case FaultRaisedWrite item:
                db.FaultEvents.Add(new FaultEvent
                {
                    Id = item.Id,
                    Timestamp = item.Timestamp,
                    FaultCode = item.FaultCode,
                    Message = item.Message,
                    MachineState = item.MachineState.ToString(),
                    CycleState = item.CycleState
                });
                break;

            case FaultClearedWrite item:
                var fault = await db.FaultEvents.FindAsync(
                    [item.Id],
                    cancellationToken
                );
                if (fault is not null)
                {
                    fault.ClearedAt = item.ClearedAt;
                }
                break;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
