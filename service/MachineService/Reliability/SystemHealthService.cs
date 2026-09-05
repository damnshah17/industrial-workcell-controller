using MachineService.Persistence;
using MachineService.Transport;
using Microsoft.EntityFrameworkCore;

namespace MachineService.Reliability;

public sealed class SystemHealthService(
    IControllerTransportHealth controller,
    IDbContextFactory<WorkcellDbContext> contextFactory,
    HistoryWriteQueue queue,
    PersistenceHealthState persistence
) : ISystemHealthService
{
    public async Task<SystemHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var controllerSnapshot = controller.GetHealth();
        var controllerHealth = new ComponentHealth(
            controllerSnapshot.Status,
            controllerSnapshot.Message,
            new Dictionary<string, object?>
            {
                ["processId"] = controllerSnapshot.ProcessId,
                ["restartCount"] = controllerSnapshot.RestartCount,
                ["lastConnectedAt"] = controllerSnapshot.LastConnectedAt,
                ["lastError"] = controllerSnapshot.LastError
            }
        );

        ComponentHealth databaseHealth;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await using var db = await contextFactory.CreateDbContextAsync(timeout.Token);
            var connected = await db.Database.CanConnectAsync(timeout.Token);
            databaseHealth = connected
                ? new(ComponentStatus.Healthy, "PostgreSQL is available.")
                : new(ComponentStatus.Degraded, "PostgreSQL is unavailable; machine control remains available.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            databaseHealth = new(
                ComponentStatus.Degraded,
                "PostgreSQL is unavailable; machine control remains available.",
                new Dictionary<string, object?> { ["error"] = exception.Message }
            );
        }

        var writer = persistence.Snapshot();
        var persistenceStatus = writer.FailedWrites > 0 || queue.DroppedWrites > 0
            ? ComponentStatus.Degraded
            : ComponentStatus.Healthy;
        var persistenceHealth = new ComponentHealth(
            persistenceStatus,
            persistenceStatus == ComponentStatus.Healthy
                ? "Persistence queue and writer are operational."
                : "Persistence has recorded write failures or dropped records; machine control is unaffected.",
            new Dictionary<string, object?>
            {
                ["queueDepth"] = queue.Depth,
                ["queueCapacity"] = HistoryWriteQueue.Capacity,
                ["failedWrites"] = writer.FailedWrites,
                ["droppedWrites"] = queue.DroppedWrites,
                ["lastSuccess"] = writer.LastSuccess,
                ["lastError"] = writer.LastError
            }
        );

        var overall = controllerSnapshot.Status == ComponentStatus.Unhealthy
            ? ComponentStatus.Unhealthy
            : databaseHealth.Status == ComponentStatus.Degraded
                || persistenceStatus == ComponentStatus.Degraded
                ? ComponentStatus.Degraded
                : ComponentStatus.Healthy;
        return new(
            overall,
            DateTimeOffset.UtcNow,
            new(ComponentStatus.Healthy, "ASP.NET service is available."),
            controllerHealth,
            databaseHealth,
            persistenceHealth
        );
    }
}
