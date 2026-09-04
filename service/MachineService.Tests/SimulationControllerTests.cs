using MachineService.Controllers;
using MachineService.Models;
using MachineService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MachineService.Tests;

public sealed class SimulationControllerTests
{
    [Fact]
    public async Task UnknownFaultReturnsBadRequestWithoutCallingService()
    {
        var service = new FakeSimulationService();
        var controller = new SimulationController(service);

        var result = await controller.EnableFault(
            "not-a-fault",
            CancellationToken.None
        );

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(service.LastFaultType);
    }

    [Fact]
    public async Task ClearEndpointDisablesRequestedSimulationFault()
    {
        var service = new FakeSimulationService();
        var controller = new SimulationController(service);

        var result = await controller.ClearFault(
            "conveyor-stop",
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(SimulationFaultType.ConveyorStop, service.LastFaultType);
        Assert.False(service.LastEnabled);
    }

    private sealed class FakeSimulationService : ISimulationService
    {
        public SimulationFaultType? LastFaultType { get; private set; }
        public bool? LastEnabled { get; private set; }

        public Task<(bool Success, MachineStatus Status)> ConfigureFaultAsync(
            SimulationFaultType faultType,
            bool enabled,
            CancellationToken cancellationToken = default
        )
        {
            LastFaultType = faultType;
            LastEnabled = enabled;
            return Task.FromResult((true, Status()));
        }

        public Task<(bool Success, MachineStatus Status)> ClearAllFaultsAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult((true, Status()));

        private static MachineStatus Status() => new(
            MachineState.Idle,
            false,
            null,
            new CycleStatus("WaitingForPart", 0, 0, 0),
            new RobotStatus("Home", false, true),
            new ConveyorStatus(false),
            new GripperStatus(true),
            new PartSensorStatus(false)
        );
    }
}
