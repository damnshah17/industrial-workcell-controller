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
                        )
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
            )
        );
    }
}