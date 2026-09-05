using MachineService.Models;
using MachineService.Controllers;
using MachineService.Persistence;
using MachineService.Reliability;
using MachineService.Transport;
using MachineService.Services;
using MachineService.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MachineService.Tests;

public sealed class ReliabilityTests
{
    [Fact]
    public void BoundedHistoryQueueReportsDepthAndDroppedWrites()
    {
        var queue = new HistoryWriteQueue();
        var write = new MachineEventWrite(
            DateTimeOffset.UtcNow, "Started", MachineState.Running, "Started."
        );

        for (var index = 0; index < HistoryWriteQueue.Capacity; ++index)
        {
            Assert.True(queue.TryEnqueue(write));
        }

        Assert.False(queue.TryEnqueue(write));
        Assert.Equal(HistoryWriteQueue.Capacity, queue.Depth);
        Assert.Equal(1, queue.DroppedWrites);
    }

    [Fact]
    public async Task FullPersistenceQueueDoesNotBreakMachineControl()
    {
        var queue = new HistoryWriteQueue();
        var filler = new MachineEventWrite(
            DateTimeOffset.UtcNow, "Started", MachineState.Running, "Started."
        );
        for (var index = 0; index < HistoryWriteQueue.Capacity; ++index)
        {
            queue.TryEnqueue(filler);
        }
        var transport = new FakeMachineTransport
        {
            Response = new(
                true, MachineState.Idle, false, false, null,
                new("WaitingForPart", 0, 0, 0),
                new("Home", false, true), new(false), new(true), new(false)
            )
        };
        var controller = new CppMachineService(transport);
        var tracker = new MachineHistoryTracker(
            queue, TimeProvider.System, NullLogger<MachineHistoryTracker>.Instance
        );
        var service = new PersistentMachineService(controller, tracker);

        var accepted = await service.InitializeAsync();

        Assert.True(accepted);
        Assert.True(queue.DroppedWrites > 0);
    }

    [Fact]
    public async Task HealthIsHealthyWhenControllerAndDatabaseAreAvailable()
    {
        var service = CreateHealthService(
            new ControllerTransportHealth(
                ComponentStatus.Healthy, "Connected.", 42, 0, DateTimeOffset.UtcNow, null
            ),
            CreateFactory()
        );

        var health = await service.GetHealthAsync();

        Assert.Equal(ComponentStatus.Healthy, health.Status);
        Assert.Equal(ComponentStatus.Healthy, health.Controller.Status);
        Assert.Equal(ComponentStatus.Healthy, health.Database.Status);
    }

    [Fact]
    public async Task DatabaseOutageDegradesHealthWithoutMarkingControllerUnavailable()
    {
        var service = CreateHealthService(
            new ControllerTransportHealth(
                ComponentStatus.Healthy, "Connected.", 42, 0, DateTimeOffset.UtcNow, null
            ),
            new ThrowingContextFactory()
        );

        var health = await service.GetHealthAsync();

        Assert.Equal(ComponentStatus.Degraded, health.Status);
        Assert.Equal(ComponentStatus.Healthy, health.Controller.Status);
        Assert.Equal(ComponentStatus.Degraded, health.Database.Status);
    }

    [Fact]
    public async Task HealthEndpointReturnsStructuredHealthyResponse()
    {
        var expected = await CreateHealthService(
            new ControllerTransportHealth(
                ComponentStatus.Healthy, "Connected.", 42, 0, DateTimeOffset.UtcNow, null
            ),
            CreateFactory()
        ).GetHealthAsync();
        var controller = new HealthController(new StaticSystemHealth(expected));

        var result = Assert.IsType<OkObjectResult>(
            await controller.Get(CancellationToken.None)
        );

        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task ControllerTransportFailureMapsToSafeServiceUnavailableResponse()
    {
        var middleware = new ApiExceptionMiddleware(
            _ => throw new ControllerUnavailableException("sensitive diagnostic"),
            NullLogger<ApiExceptionMiddleware>.Instance
        );
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var response = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("sensitive diagnostic", response);
        Assert.Contains("traceId", response);
    }

    [Fact]
    public async Task PersistenceFailureIsRecordedAndWriterContinuesRunning()
    {
        var queue = new HistoryWriteQueue();
        var persistence = new PersistenceHealthState();
        var writer = new HistoryWriterService(
            queue, new ThrowingContextFactory(), persistence,
            NullLogger<HistoryWriterService>.Instance
        );
        await writer.StartAsync(CancellationToken.None);
        queue.TryEnqueue(new MachineEventWrite(
            DateTimeOffset.UtcNow, "Started", MachineState.Running, "Started."
        ));

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (persistence.Snapshot().FailedWrites == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(1, persistence.Snapshot().FailedWrites);
        await writer.StopAsync(CancellationToken.None);
    }

    private static SystemHealthService CreateHealthService(
        ControllerTransportHealth health,
        IDbContextFactory<WorkcellDbContext> factory
    ) => new(new StaticControllerHealth(health), factory, new HistoryWriteQueue(), new PersistenceHealthState());

    private static IDbContextFactory<WorkcellDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<WorkcellDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestContextFactory(options);
    }

    private sealed class StaticControllerHealth(ControllerTransportHealth health)
        : IControllerTransportHealth
    {
        public ControllerTransportHealth GetHealth() => health;
    }

    private sealed class StaticSystemHealth(SystemHealth health) : ISystemHealthService
    {
        public Task<SystemHealth> GetHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(health);
    }

    private sealed class TestContextFactory(DbContextOptions<WorkcellDbContext> options)
        : IDbContextFactory<WorkcellDbContext>
    {
        public WorkcellDbContext CreateDbContext() => new(options);
    }

    private sealed class ThrowingContextFactory : IDbContextFactory<WorkcellDbContext>
    {
        public WorkcellDbContext CreateDbContext() =>
            throw new InvalidOperationException("Database unavailable for test.");
    }
}
