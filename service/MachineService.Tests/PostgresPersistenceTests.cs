using MachineService.Persistence;
using MachineService.Persistence.Entities;
using MachineService.Services;
using Microsoft.EntityFrameworkCore;

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
            await db.SaveChangesAsync();
        }

        var events = await new HistoryService(factory)
            .GetEventsAsync(1, 10, CancellationToken.None);

        Assert.Contains(
            events.Items,
            item => item.EventType == "IntegrationTest"
        );
    }

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
