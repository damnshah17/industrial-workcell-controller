using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MachineService.Models;
using MachineService.Reliability;
using MachineService.Transport;
using Microsoft.Extensions.DependencyInjection;
using WorkcellOperatorConsole.Core.Services;

namespace MachineService.Tests.Integration;

public sealed class HostedHttpIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task RealPipelineCoversHealthLifecycleValidationConflictAndHistory()
    {
        var bridge = HostedMachineFactory.FindBridge();
        await using var factory = new HostedMachineFactory(bridge);
        await factory.PrepareDatabaseAsync();
        using var client = factory.CreateClient();

        var health = await Get<SystemHealth>(client, "/health");
        Assert.Equal(ComponentStatus.Healthy, health?.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/machine/start", null)).StatusCode);
        using var malformed = new StringContent("{", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/machine/cycle", malformed)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/machine/initialize", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/machine/start", null)).StatusCode);
        Assert.Equal(MachineState.Running, (await Get<MachineStatus>(client, "/api/machine/status"))?.State);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/metrics")).StatusCode);
    }

    [Fact]
    public async Task ControllerUnavailableUsesHostedMiddlewareAndReturns503()
    {
        var bridge = HostedMachineFactory.FindBridge();
        await using var factory = new HostedMachineFactory(bridge, controllerUnavailable: true);
        using var response = await factory.CreateClient().GetAsync("/api/machine/status");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("traceId", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DatabaseOutageDegradesHealthAndMachineControlContinues()
    {
        var bridge = HostedMachineFactory.FindBridge();
        const string unavailableDatabase =
            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1;Command Timeout=1";
        await using var factory = new HostedMachineFactory(bridge, unavailableDatabase);
        using var client = factory.CreateClient();

        var health = await Get<SystemHealth>(client, "/health");
        Assert.Equal(ComponentStatus.Degraded, health?.Status);
        Assert.Equal(ComponentStatus.Healthy, health?.Controller.Status);
        Assert.Equal(ComponentStatus.Degraded, health?.Database.Status);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/machine/initialize", null)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/metrics")).StatusCode);
    }

    [Fact]
    public async Task BridgeCrashRecoversThroughHttpWithoutResumingProduction()
    {
        var bridge = HostedMachineFactory.FindBridge();
        await using var factory = new HostedMachineFactory(bridge);
        await factory.PrepareDatabaseAsync();
        using var client = factory.CreateClient();
        await PostOk(client, "/api/machine/initialize");
        await PostOk(client, "/api/machine/start");
        var transport = factory.Services.GetRequiredService<CppProcessMachineTransport>();
        var oldProcess = transport.ProcessId;
        transport.TerminateForTest();

        var status = await WaitForAsync(
            () => Get<MachineStatus>(client, "/api/machine/status"),
            value => value?.State == MachineState.Offline
        );

        Assert.Equal(MachineState.Offline, status?.State);
        Assert.NotEqual(oldProcess, transport.ProcessId);
        Assert.True(transport.GetHealth().RestartCount >= 1);
    }

    [Fact]
    public async Task RealWpfClientOperatesAgainstHostedAspNetPipeline()
    {
        var bridge = HostedMachineFactory.FindBridge();
        await using var factory = new HostedMachineFactory(bridge);
        await factory.PrepareDatabaseAsync();
        var api = new HttpWorkcellApiClient(factory.CreateClient());

        Assert.Equal("Healthy", (await api.GetHealthAsync()).Controller.Status);
        Assert.Equal(WorkcellOperatorConsole.Core.Models.MachineState.Offline, (await api.GetStatusAsync()).State);
        Assert.Equal(WorkcellOperatorConsole.Core.Models.MachineState.Idle, (await api.SendCommandAsync("initialize")).State);
        await Assert.ThrowsAsync<MachineCommandRejectedException>(() => api.SendCommandAsync("pause"));
    }

    [Fact]
    public async Task AcceptedAndRejectedVisionCyclesPersistExactlyOnce()
    {
        var bridge = HostedMachineFactory.FindBridge();
        var connection = Environment.GetEnvironmentVariable("WORKCELL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await using var factory = new HostedMachineFactory(bridge, connection);
        await factory.PrepareDatabaseAsync();
        using var client = factory.CreateClient();
        await PostOk(client, "/api/machine/initialize");
        await PostOk(client, "/api/machine/start");

        await RunCycle(client, "good-part", true, "PASS");
        await RunCycle(client, "missing-hole", false, "MISSING_FEATURE");
        var cycles = await WaitForAsync(async () =>
            await Get<PagedResult<ProductionCycleDto>>(client, "/api/cycles?pageSize=10"),
            result => result?.Items.Count(x => x.FinalStatus == "Completed") == 2
        );
        var completed = cycles!.Items.Where(x => x.FinalStatus == "Completed").ToList();
        Assert.Equal(2, completed.Count);
        Assert.Single(completed, x => x.Accepted == true && x.InspectionSampleId == "good-part");
        Assert.Single(completed, x => x.Accepted == false && x.InspectionSampleId == "missing-hole");
        var before = await Get<PagedResult<MachineEventDto>>(client, "/api/events?pageSize=100");
        for (var index = 0; index < 10; ++index) await client.GetAsync("/api/machine/status");
        var after = await Get<PagedResult<MachineEventDto>>(client, "/api/events?pageSize=100");
        Assert.Equal(before?.TotalCount, after?.TotalCount);
        var metrics = await Get<ProductionMetrics>(client, "/api/metrics");
        Assert.Equal(2, metrics?.TotalCycles);
        Assert.Equal(1, metrics?.AcceptedCycles);
        Assert.Equal(1, metrics?.RejectedCycles);
    }

    [Fact]
    public async Task EmergencyStopAndRobotFaultPropagateAndRecoverThroughHttp()
    {
        var bridge = HostedMachineFactory.FindBridge();
        await using var factory = new HostedMachineFactory(bridge);
        await factory.PrepareDatabaseAsync();
        using var client = factory.CreateClient();
        await PostOk(client, "/api/machine/initialize");
        await PostOk(client, "/api/machine/start");
        await PostJsonOk(client, "/api/machine/cycle", new { sampleId = "good-part" });
        await PostOk(client, "/api/machine/estop");
        Assert.Equal(MachineState.EmergencyStop, (await Get<MachineStatus>(client, "/api/machine/status"))?.State);
        await PostOk(client, "/api/machine/clear-estop");
        await PostOk(client, "/api/machine/reset");
        await PostOk(client, "/api/machine/start");
        await PostOk(client, "/api/simulation/faults/robot-communication");
        await PostJsonOk(client, "/api/machine/cycle", new { sampleId = "good-part" });
        var faulted = await WaitForAsync(
            () => Get<MachineStatus>(client, "/api/machine/status"),
            status => status?.State == MachineState.Faulted
        );
        Assert.Equal("ROBOT_COMMUNICATION_LOSS", faulted?.ActiveFault?.Code);
        await PostOk(client, "/api/simulation/faults/robot-communication/clear");
        await PostOk(client, "/api/machine/reset");
        Assert.Equal(MachineState.Idle, (await Get<MachineStatus>(client, "/api/machine/status"))?.State);
    }

    private static async Task RunCycle(HttpClient client, string sample, bool accepted, string reason)
    {
        await PostJsonOk(client, "/api/machine/cycle", new { sampleId = sample });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var states = new List<string>();
        MachineStatus? status;
        do
        {
            status = await Get<MachineStatus>(client, "/api/machine/status");
            if (status is not null) states.Add(status.Cycle.State);
            if (status?.Cycle.State != "CycleComplete") await Task.Delay(25, timeout.Token);
        } while (status?.Cycle.State != "CycleComplete");
        Assert.Equal(accepted, status?.Inspection?.Accepted);
        Assert.Equal(reason, status?.Inspection?.Reason);
        Assert.Contains(accepted ? "MovingToAcceptBin" : "MovingToRejectBin", states);
    }

    private static async Task PostOk(HttpClient client, string path) =>
        (await client.PostAsync(path, null)).EnsureSuccessStatusCode();

    private static async Task PostJsonOk(HttpClient client, string path, object body) =>
        (await client.PostAsJsonAsync(path, body)).EnsureSuccessStatusCode();

    private static Task<T?> Get<T>(HttpClient client, string path) =>
        client.GetFromJsonAsync<T>(path, JsonOptions);

    private static async Task<T?> WaitForAsync<T>(Func<Task<T?>> read, Func<T?, bool> complete)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var value = await read();
            if (complete(value)) return value;
            await Task.Delay(25, timeout.Token);
        }
    }
}
