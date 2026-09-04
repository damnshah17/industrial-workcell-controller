using MachineService.Persistence;
using MachineService.Persistence.Entities;
using MachineService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MachineService.Tests;

public sealed class HistoryServiceTests
{
    [Fact]
    public async Task EventsArePagedNewestFirst()
    {
        var factory = CreateFactory();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.MachineEvents.AddRange(
                CreateEvent(1, DateTimeOffset.UtcNow.AddMinutes(-1)),
                CreateEvent(2, DateTimeOffset.UtcNow)
            );
            await db.SaveChangesAsync();
        }

        var result = await new HistoryService(factory)
            .GetEventsAsync(1, 1, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(2, result.Items[0].Id);
    }

    [Fact]
    public async Task MetricsUseCompletedCyclesAndFaultHistory()
    {
        var factory = CreateFactory();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.ProductionCycles.AddRange(
                CreateCycle(true, 1000),
                CreateCycle(true, 2000),
                CreateCycle(false, 3000),
                new ProductionCycle
                {
                    Id = Guid.NewGuid(),
                    StartedAt = DateTimeOffset.UtcNow,
                    FinalStatus = "Running"
                }
            );
            db.FaultEvents.Add(new FaultEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                FaultCode = "MOTION_TIMEOUT",
                Message = "Timed out",
                MachineState = "Faulted"
            });
            await db.SaveChangesAsync();
        }

        var metrics = await new HistoryService(factory)
            .GetMetricsAsync(CancellationToken.None);

        Assert.Equal(3, metrics.TotalCycles);
        Assert.Equal(2, metrics.AcceptedCycles);
        Assert.Equal(1, metrics.RejectedCycles);
        Assert.Equal(66.67, metrics.AcceptanceRate, 2);
        Assert.Equal(2000, metrics.AverageCycleDurationMilliseconds);
        Assert.Equal(1, metrics.FaultCount);
    }

    [Fact]
    public async Task BackgroundWriterPersistsCycleAndFaultLifecycle()
    {
        var factory = CreateFactory();
        var queue = new HistoryWriteQueue();
        var writer = new HistoryWriterService(
            queue,
            factory,
            NullLogger<HistoryWriterService>.Instance
        );
        var cycleId = Guid.NewGuid();
        var faultId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow.AddSeconds(-2);

        await writer.StartAsync(CancellationToken.None);
        queue.TryEnqueue(new CycleStartedWrite(cycleId, startedAt, true));
        queue.TryEnqueue(new CycleFinishedWrite(
            cycleId,
            startedAt.AddSeconds(2),
            "Completed",
            false,
            null,
            null
        ));
        queue.TryEnqueue(new FaultRaisedWrite(
            faultId,
            startedAt,
            "MOTION_TIMEOUT",
            "Timed out",
            MachineService.Models.MachineState.Faulted,
            "CycleFaulted"
        ));
        queue.TryEnqueue(new FaultClearedWrite(
            faultId,
            startedAt.AddSeconds(3)
        ));

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = await factory.CreateDbContextAsync();
            if (
                await db.ProductionCycles.AnyAsync(x => x.Id == cycleId)
                && await db.FaultEvents.AnyAsync(
                    x => x.Id == faultId && x.ClearedAt != null
                )
            )
            {
                break;
            }
            await Task.Delay(10);
        }

        await writer.StopAsync(CancellationToken.None);

        await using var verification = await factory.CreateDbContextAsync();
        var cycle = await verification.ProductionCycles.FindAsync(cycleId);
        var fault = await verification.FaultEvents.FindAsync(faultId);
        Assert.NotNull(cycle);
        Assert.Equal(2000, cycle.DurationMilliseconds);
        Assert.True(cycle.Accepted);
        Assert.NotNull(fault?.ClearedAt);
    }

    private static IDbContextFactory<WorkcellDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<WorkcellDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestContextFactory(options);
    }

    private static MachineEvent CreateEvent(long id, DateTimeOffset timestamp) => new()
    {
        Id = id,
        Timestamp = timestamp,
        EventType = "Started",
        MachineState = "Running",
        Message = "Machine started."
    };

    private static ProductionCycle CreateCycle(bool accepted, long duration) => new()
    {
        Id = Guid.NewGuid(),
        StartedAt = DateTimeOffset.UtcNow,
        CompletedAt = DateTimeOffset.UtcNow,
        Accepted = accepted,
        DurationMilliseconds = duration,
        FinalStatus = "Completed"
    };

    private sealed class TestContextFactory(
        DbContextOptions<WorkcellDbContext> options
    ) : IDbContextFactory<WorkcellDbContext>
    {
        public WorkcellDbContext CreateDbContext() => new(options);
    }
}
