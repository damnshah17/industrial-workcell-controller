using WorkcellOperatorConsole.Core.Models;
using WorkcellOperatorConsole.Core.ViewModels;

namespace WorkcellOperatorConsole.Tests;

public sealed class OperatorConsoleViewModelTests
{
    [Fact]
    public async Task StatusRefreshMapsAuthoritativeTelemetryForDisplay()
    {
        var api = new FakeWorkcellApiClient
        {
            Status = TestData.Status(
                MachineState.Faulted,
                new FaultInfo("MOTION_TIMEOUT", "Robot timed out")
            )
        };
        using var viewModel = new OperatorConsoleViewModel(api);

        await viewModel.RefreshStatusAsync();

        Assert.Equal("FAULTED", viewModel.MachineStateText);
        Assert.Equal("MOTION_TIMEOUT: Robot timed out", viewModel.FaultText);
        Assert.Equal("CONNECTED", viewModel.ConnectionText);
        Assert.Equal(4, viewModel.Status?.Cycle.Total);
    }

    [Fact]
    public async Task HistoryRefreshMapsApiCollectionsAndMetrics()
    {
        var api = new FakeWorkcellApiClient();
        using var viewModel = new OperatorConsoleViewModel(api);

        await viewModel.RefreshHistoryAsync();

        Assert.Empty(viewModel.Cycles);
        Assert.Empty(viewModel.Faults);
        Assert.Empty(viewModel.Events);
        Assert.NotNull(viewModel.Metrics);
    }

    [Fact]
    public async Task HealthDistinguishesControllerOutageFromBackendOutage()
    {
        var api = new FakeWorkcellApiClient { Health = TestData.Health(controller: "Unhealthy") };
        using var viewModel = new OperatorConsoleViewModel(api);

        await viewModel.RefreshStatusAsync();

        Assert.Equal("BACKEND OK • CONTROLLER UNAVAILABLE", viewModel.ConnectionText);
    }

    [Fact]
    public async Task HealthReportsHistoryDegradationWithoutHidingControllerTelemetry()
    {
        var api = new FakeWorkcellApiClient { Health = TestData.Health(database: "Degraded") };
        using var viewModel = new OperatorConsoleViewModel(api);

        await viewModel.RefreshStatusAsync();

        Assert.Equal("CONTROLLER OK • HISTORY DEGRADED", viewModel.ConnectionText);
        Assert.Equal("RUNNING", viewModel.MachineStateText);
    }

    [Fact]
    public async Task VisionCycleUsesSelectedSampleAndDisplaysControllerResult()
    {
        var api = new FakeWorkcellApiClient
        {
            Status = TestData.Status() with
            {
                Inspection = new InspectionStatus(
                    "Complete", false, "MISSING_FEATURE", "missing-hole", 0.0,
                    "Required opening was not detected."
                )
            }
        };
        using var viewModel = new OperatorConsoleViewModel(api)
        {
            SelectedSample = "missing-hole"
        };

        await viewModel.StartVisionCycleCommand.ExecuteAsync();

        Assert.Equal("missing-hole", api.LastInspectionSample);
        Assert.Equal("FAIL — MISSING_FEATURE", viewModel.InspectionText);
        Assert.Equal("Required opening was not detected.", viewModel.InspectionDetailsText);
    }
}
