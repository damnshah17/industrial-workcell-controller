using MachineService.Models;
using MachineService.Transport;

namespace MachineService.Services;

public sealed class CppMachineService :
    IMachineService
{
    private readonly IMachineTransport
        _transport;

    public CppMachineService(
        IMachineTransport transport
    )
    {
        _transport = transport;
    }

    public async Task<MachineStatus>
        GetStatusAsync(
            CancellationToken cancellationToken =
                default
        )
    {
        var response =
            await _transport.SendCommandAsync(
                "status",
                cancellationToken
            );

        return ToStatus(response);
    }

    public async Task<bool>
        InitializeAsync(
            CancellationToken cancellationToken =
                default
        )
    {
        return await ExecuteAsync(
            "initialize",
            cancellationToken
        );
    }

    public async Task<bool>
        StartAsync(
            CancellationToken cancellationToken =
                default
        )
    {
        return await ExecuteAsync(
            "start",
            cancellationToken
        );
    }

    public async Task<bool>
        StartCycleAsync(
            bool inspectionAccepted,
            CancellationToken cancellationToken = default
        )
    {
        return await ExecuteAsync(
            inspectionAccepted
                ? "cycle-accepted"
                : "cycle-rejected",
            cancellationToken
        );
    }

    public async Task<bool>
        PauseAsync(
            CancellationToken cancellationToken =
                default
        )
    {
        return await ExecuteAsync(
            "pause",
            cancellationToken
        );
    }

    public async Task<bool>
        ResumeAsync(
            CancellationToken cancellationToken =
                default
        )
    {
        return await ExecuteAsync(
            "resume",
            cancellationToken
        );
    }

    public async Task<bool>
        StopAsync(
            CancellationToken cancellationToken =
                default
        )
    {
        return await ExecuteAsync(
            "stop",
            cancellationToken
        );
    }

    public async Task<bool>
        ResetAsync(
            CancellationToken cancellationToken =
                default
        )
    {
        return await ExecuteAsync(
            "reset",
            cancellationToken
        );
    }

    public async Task<bool>
        EmergencyStopAsync(
            CancellationToken cancellationToken =
                default
        )
    {
        return await ExecuteAsync(
            "estop",
            cancellationToken
        );
    }

    public async Task<bool>
        ClearEmergencyStopAsync(
            CancellationToken cancellationToken =
                default
        )
    {
        return await ExecuteAsync(
            "clear-estop",
            cancellationToken
        );
    }

    private async Task<bool> ExecuteAsync(
        string command,
        CancellationToken cancellationToken
    )
    {
        var response =
            await _transport.SendCommandAsync(
                command,
                cancellationToken
            );

        return response.Success;
    }

    internal static MachineStatus ToStatus(
        ControllerResponse response
    )
    {
        return new MachineStatus(
            response.State,
            response.EmergencyStopActive,
            response.Fault,
            response.Cycle,
            response.Robot,
            response.Conveyor,
            response.Gripper,
            response.PartSensor
        );
    }
    public async Task<bool>
        InjectMotionTimeoutFaultAsync(
            CancellationToken cancellationToken =
                default
        )
    {
        return await ExecuteAsync(
            "fault-motion-timeout",
            cancellationToken
        );
    }
}
