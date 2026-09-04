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
}
