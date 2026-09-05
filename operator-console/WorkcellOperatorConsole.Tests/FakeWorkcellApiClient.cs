using WorkcellOperatorConsole.Core.Models;
using WorkcellOperatorConsole.Core.Services;

namespace WorkcellOperatorConsole.Tests;

internal sealed class FakeWorkcellApiClient : IWorkcellApiClient
{
    public MachineStatus Status { get; set; } = TestData.Status();
    public string? LastCommand { get; private set; }
    public string? LastInspectionSample { get; private set; }

    public Task<MachineStatus> GetStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(Status);

    public Task<MachineStatus> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        LastCommand = command;
        return Task.FromResult(Status);
    }

    public Task<MachineStatus> StartCycleAsync(string sampleId, CancellationToken cancellationToken = default)
    {
        LastInspectionSample = sampleId;
        return Task.FromResult(Status);
    }

    public Task<PagedResult<MachineEvent>> GetEventsAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<MachineEvent>([], page, pageSize, 0));

    public Task<PagedResult<ProductionCycle>> GetCyclesAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<ProductionCycle>([], page, pageSize, 0));

    public Task<PagedResult<FaultEvent>> GetFaultsAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<FaultEvent>([], page, pageSize, 0));

    public Task<ProductionMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProductionMetrics(0, 0, 0, 0, 0, 0));
}

internal static class TestData
{
    public static MachineStatus Status(
        MachineState state = MachineState.Running,
        FaultInfo? fault = null
    ) => new(
        state,
        state == MachineState.EmergencyStop,
        fault,
        new CycleStatus("WaitingForPart", 4, 3, 1),
        new RobotStatus("Home", false, true),
        new ConveyorStatus(true),
        new GripperStatus(true),
        new PartSensorStatus(false)
    );
}
