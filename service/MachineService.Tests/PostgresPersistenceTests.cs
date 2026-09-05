using MachineService.Persistence;
using MachineService.Persistence.Entities;
using MachineService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MachineService.Models;

namespace MachineService.Tests;

public sealed class PostgresPersistenceTests
{
    [Fact]
    public async Task MigrationSchemaSupportsHistoryQueries()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "WORKCELL_TEST_CONNECTION_STRING"
        );

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<WorkcellDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var factory = new TestContextFactory(options);

        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
            db.MachineEvents.Add(new MachineEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                EventType = "IntegrationTest",
                MachineState = "Idle",
                Message = "PostgreSQL persistence verified."
            });
            db.ProductionCycles.Add(new ProductionCycle
            {
                Id = Guid.NewGuid(),
                StartedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
                CompletedAt = DateTimeOffset.UtcNow,
                Accepted = false,
                FinalStatus = "Completed",
                InspectionReason = "MISSING_FEATURE",
                InspectionSampleId = "missing-hole",
                InspectionFeatureCoverage = 0.0
            });
            await db.SaveChangesAsync();
        }

        var events = await new HistoryService(factory)
            .GetEventsAsync(1, 10, CancellationToken.None);

        Assert.Contains(
            events.Items,
            item => item.EventType == "IntegrationTest"
        );
        var cycles = await new HistoryService(factory)
            .GetCyclesAsync(1, 10, CancellationToken.None);
        Assert.Contains(
            cycles.Items,
            item => item.InspectionReason == "MISSING_FEATURE"
                && item.InspectionSampleId == "missing-hole"
                && item.Accepted == false
        );
    }

    [Fact]
    public async Task SimulatedControllerFaultIsPersistedAndClearedOnce()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "WORKCELL_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<WorkcellDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var factory = new TestContextFactory(options);
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
        }

        var queue = new HistoryWriteQueue();
        var writer = new HistoryWriterService(
            queue,
            factory,
            new PersistenceHealthState(),
            NullLogger<HistoryWriterService>.Instance
        );
        var tracker = new MachineHistoryTracker(
            queue,
            TimeProvider.System,
            NullLogger<MachineHistoryTracker>.Instance
        );
        var faultStatus = Status(
            MachineState.Faulted,
            new FaultInfo(
                "ROBOT_COMMUNICATION_LOSS",
                "Robot communication lost during motion."
            )
        );

        await writer.StartAsync(CancellationToken.None);
        tracker.Observe(faultStatus);
        tracker.Observe(faultStatus);
        tracker.Observe(Status(MachineState.Idle, null));

        var deadline = DateTime.UtcNow.AddSeconds(3);
        FaultEvent? persisted = null;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = await factory.CreateDbContextAsync();
            persisted = await db.FaultEvents
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefaultAsync(
                    x => x.FaultCode == "ROBOT_COMMUNICATION_LOSS"
                        && x.ClearedAt != null
                );
            if (persisted is not null)
            {
                break;
            }
            await Task.Delay(25);
        }
        await writer.StopAsync(CancellationToken.None);

        Assert.NotNull(persisted);
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Equal(
            1,
            await verification.FaultEvents.CountAsync(
                x => x.Id == persisted.Id
            )
        );
    }

    private static MachineStatus Status(
        MachineState state,
        FaultInfo? fault
    ) => new(
        state,
        false,
        fault,
        new CycleStatus("CycleFaulted", 0, 0, 0),
        new RobotStatus("Home", false, true),
        new ConveyorStatus(false),
        new GripperStatus(true),
        new PartSensorStatus(false)
    );

    private sealed class TestContextFactory(
        DbContextOptions<WorkcellDbContext> options
    ) : IDbContextFactory<WorkcellDbContext>
    {
        public WorkcellDbContext CreateDbContext() => new(options);

        public Task<WorkcellDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult(CreateDbContext());
    }
}
