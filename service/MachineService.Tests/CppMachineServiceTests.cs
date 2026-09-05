using MachineService.Models;
using MachineService.Services;
using MachineService.Tests.Fakes;

namespace MachineService.Tests;

public sealed class CppMachineServiceTests
{
    [Fact]
    public async Task GetStatusAsync_SendsStatusCommand()
    {
        var transport =
            new FakeMachineTransport();

        var service =
            new CppMachineService(
                transport
            );

        var status =
            await service.GetStatusAsync();

        Assert.Equal(
            "status",
            transport.LastCommand
        );

        Assert.Equal(
            MachineState.Offline,
            status.State
        );
    }

    [Fact]
    public async Task StartAsync_SendsStartCommand()
    {
        var transport =
            new FakeMachineTransport
            {
                Response =
                    CreateResponse(
                        success: true,
                        state:
                            MachineState.Running
                    )
            };

        var service =
            new CppMachineService(
                transport
            );

        var success =
            await service.StartAsync();

        Assert.True(success);

        Assert.Equal(
            "start",
            transport.LastCommand
        );
    }

    [Fact]
    public async Task EmergencyStopAsync_SendsEstopCommand()
    {
        var transport =
            new FakeMachineTransport
            {
                Response =
                    CreateResponse(
                        success: true,
                        state:
                            MachineState.EmergencyStop,
                        emergencyStopActive:
                            true
                    )
            };

        var service =
            new CppMachineService(
                transport
            );

        var success =
            await service
                .EmergencyStopAsync();

        Assert.True(success);

        Assert.Equal(
            "estop",
            transport.LastCommand
        );
    }

    [Fact]
    public async Task FaultInjection_SendsMotionTimeoutCommand()
    {
        var transport =
            new FakeMachineTransport
            {
                Response =
                    CreateResponse(
                        success: true,
                        state:
                            MachineState.Faulted,
                        fault:
                            new FaultInfo(
                                "MOTION_TIMEOUT",
                                "Injected motion timeout"
                            )
                    )
            };

        var service =
            new CppMachineService(
                transport
            );

        var success =
            await service
                .InjectMotionTimeoutFaultAsync();

        Assert.True(success);

        Assert.Equal(
            "fault-motion-timeout",
            transport.LastCommand
        );
    }

    [Fact]
    public async Task GetStatusAsync_MapsFaultInformation()
    {
        var fault =
            new FaultInfo(
                "MOTION_TIMEOUT",
                "Robot motion timed out"
            );

        var transport =
            new FakeMachineTransport
            {
                Response =
                    CreateResponse(
                        success: true,
                        state:
                            MachineState.Faulted,
                        fault: fault
                    )
            };

        var service =
            new CppMachineService(
                transport
            );

        var status =
            await service.GetStatusAsync();

        Assert.Equal(
            MachineState.Faulted,
            status.State
        );

        Assert.NotNull(
            status.ActiveFault
        );

        Assert.Equal(
            "MOTION_TIMEOUT",
            status.ActiveFault.Code
        );

        Assert.Equal(
            "Robot motion timed out",
            status.ActiveFault.Message
        );
    }

    [Fact]
    public async Task GetStatusAsync_MapsCycleTelemetry()
    {
        var transport =
            new FakeMachineTransport
            {
                Response =
                    new ControllerResponse(
                        true,
                        MachineState.Running,
                        false,
                        false,
                        null,
                        new CycleStatus(
                            "CycleComplete",
                            12,
                            10,
                            2
                        ),
                        new RobotStatus("Home", false, true),
                        new ConveyorStatus(true),
                        new GripperStatus(true),
                        new PartSensorStatus(false)
                    )
            };

        var service =
            new CppMachineService(
                transport
            );

        var status =
            await service.GetStatusAsync();

        Assert.Equal(
            "CycleComplete",
            status.Cycle.State
        );

        Assert.Equal(
            12,
            status.Cycle.Total
        );

        Assert.Equal(
            10,
            status.Cycle.Accepted
        );

        Assert.Equal(
            2,
            status.Cycle.Rejected
        );
    }

    [Theory]
    [InlineData(true, "cycle-accepted")]
    [InlineData(false, "cycle-rejected")]
    public async Task StartCycleAsync_SendsInspectionDecision(
        bool inspectionAccepted,
        string expectedCommand
    )
    {
        var transport = new FakeMachineTransport();
        var service = new CppMachineService(transport);

        var success = await service.StartCycleAsync(
            inspectionAccepted
        );

        Assert.True(success);
        Assert.Equal(expectedCommand, transport.LastCommand);
    }

    [Fact]
    public async Task StartVisionCycle_SendsKnownSampleWithoutCallerDecision()
    {
        var transport = new FakeMachineTransport();
        var service = new CppMachineService(transport);

        Assert.True(await service.StartCycleAsync("missing-hole"));
        Assert.Equal("cycle-sample-missing-hole", transport.LastCommand);
        Assert.False(await service.StartCycleAsync("../../unsafe"));
        Assert.Equal("cycle-sample-missing-hole", transport.LastCommand);
    }

    [Fact]
    public async Task GetStatusAsync_MapsInspectionTelemetry()
    {
        var transport = new FakeMachineTransport
        {
            Response = new ControllerResponse(
                true, MachineState.Running, false, false, null,
                new CycleStatus("CycleComplete", 1, 0, 1),
                new RobotStatus("Home", false, true),
                new ConveyorStatus(true), new GripperStatus(true),
                new PartSensorStatus(false),
                new InspectionStatus("Complete", false, "MISSING_FEATURE", "missing-hole", 0.0, "Opening missing")
            )
        };

        var status = await new CppMachineService(transport).GetStatusAsync();

        Assert.False(status.Inspection?.Accepted);
        Assert.Equal("MISSING_FEATURE", status.Inspection?.Reason);
        Assert.Equal("missing-hole", status.Inspection?.SampleId);
    }

    [Fact]
    public async Task GetStatusAsync_MapsHardwareTelemetry()
    {
        var transport = new FakeMachineTransport
        {
            Response = new ControllerResponse(
                true,
                MachineState.Running,
                false,
                false,
                null,
                new CycleStatus("MovingToPick", 3, 2, 1),
                new RobotStatus("Home", true, true),
                new ConveyorStatus(false),
                new GripperStatus(true),
                new PartSensorStatus(true)
            )
        };

        var status = await new CppMachineService(transport)
            .GetStatusAsync();

        Assert.True(status.Robot.Moving);
        Assert.False(status.Conveyor.Running);
        Assert.True(status.Gripper.Open);
        Assert.True(status.PartSensor.Active);
    }

    private static ControllerResponse
        CreateResponse(
            bool success,
            MachineState state,
            bool emergencyStopActive = false,
            FaultInfo? fault = null
        )
    {
        return new ControllerResponse(
            success,
            state,
            emergencyStopActive,
            fault is not null,
            fault,
            new CycleStatus(
                "WaitingForPart",
                0,
                0,
                0
            ),
            new RobotStatus("Home", false, true),
            new ConveyorStatus(false),
            new GripperStatus(true),
            new PartSensorStatus(false)
        );
    }
}
