using MachineService.Models;
using MachineService.Services;
using MachineService.Tests.Fakes;

namespace MachineService.Tests;

public sealed class CppSimulationServiceTests
{
    [Theory]
    [InlineData(SimulationFaultType.RobotCommunication, "robot-communication")]
    [InlineData(SimulationFaultType.MotionTimeout, "motion-timeout")]
    [InlineData(SimulationFaultType.ConveyorStart, "conveyor-start")]
    [InlineData(SimulationFaultType.ConveyorStop, "conveyor-stop")]
    [InlineData(SimulationFaultType.GripperOpen, "gripper-open")]
    [InlineData(SimulationFaultType.GripperClose, "gripper-close")]
    [InlineData(SimulationFaultType.Sensor, "sensor")]
    [InlineData(SimulationFaultType.SafetyDoor, "safety-door")]
    public async Task ConfigureFaultMapsToSimulationBridgeCommand(
        SimulationFaultType faultType,
        string commandName
    )
    {
        var transport = new FakeMachineTransport();
        var service = new CppSimulationService(transport);

        var result = await service.ConfigureFaultAsync(faultType, true);

        Assert.True(result.Success);
        Assert.Equal(
            $"simulation-fault-{commandName}",
            transport.LastCommand
        );
    }

    [Fact]
    public async Task ClearFaultAddsClearSuffix()
    {
        var transport = new FakeMachineTransport();
        var service = new CppSimulationService(transport);

        await service.ConfigureFaultAsync(
            SimulationFaultType.GripperClose,
            false
        );

        Assert.Equal(
            "simulation-fault-gripper-close-clear",
            transport.LastCommand
        );
    }

    [Fact]
    public async Task ClearAllUsesDedicatedBridgeCommand()
    {
        var transport = new FakeMachineTransport();
        var service = new CppSimulationService(transport);

        await service.ClearAllFaultsAsync();

        Assert.Equal("simulation-faults-clear", transport.LastCommand);
    }

    [Fact]
    public async Task FaultTelemetryIsReturnedWithoutCSharpFaultLogic()
    {
        var fault = new FaultInfo(
            "ROBOT_COMMUNICATION_LOSS",
            "Failed to start robot motion to Pick."
        );
        var transport = new FakeMachineTransport
        {
            Response = new ControllerResponse(
                true,
                MachineState.Faulted,
                false,
                true,
                fault,
                new CycleStatus("CycleFaulted", 0, 0, 0),
                new RobotStatus("Home", false, true),
                new ConveyorStatus(false),
                new GripperStatus(true),
                new PartSensorStatus(false)
            )
        };

        var result = await new CppSimulationService(transport)
            .ConfigureFaultAsync(
                SimulationFaultType.RobotCommunication,
                true
            );

        Assert.Same(fault, result.Status.ActiveFault);
        Assert.Equal(MachineState.Faulted, result.Status.State);
    }
}
