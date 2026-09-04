using MachineService.Models;
using MachineService.Transport;

namespace MachineService.Tests.Fakes;

public sealed class FakeMachineTransport
    : IMachineTransport
{
    public string? LastCommand { get; private set; }

    public ControllerResponse Response { get; set; } =
        new(
            true,
            MachineState.Offline,
            false,
            false,
            null,
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

    public Task<ControllerResponse> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default
    )
    {
        LastCommand = command;

        return Task.FromResult(
            Response
        );
    }
}
