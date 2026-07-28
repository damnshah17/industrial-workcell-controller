using MachineService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MachineService.Controllers;

[ApiController]
[Route("api/machine")]
public sealed class MachineController :
    ControllerBase
{
    private readonly IMachineService
        _machineService;

    public MachineController(
        IMachineService machineService
    )
    {
        _machineService =
            machineService;
    }

    [HttpGet("status")]
    public async Task<IActionResult>
        GetStatus(
            CancellationToken cancellationToken
        )
    {
        return Ok(
            await _machineService
                .GetStatusAsync(
                    cancellationToken
                )
        );
    }

    [HttpPost("initialize")]
    public async Task<IActionResult>
        Initialize(
            CancellationToken cancellationToken
        )
    {
        return await CommandResult(
            () =>
                _machineService
                    .InitializeAsync(
                        cancellationToken
                    ),
            cancellationToken
        );
    }

    [HttpPost("start")]
    public async Task<IActionResult>
        Start(
            CancellationToken cancellationToken
        )
    {
        return await CommandResult(
            () =>
                _machineService
                    .StartAsync(
                        cancellationToken
                    ),
            cancellationToken
        );
    }

    [HttpPost("pause")]
    public async Task<IActionResult>
        Pause(
            CancellationToken cancellationToken
        )
    {
        return await CommandResult(
            () =>
                _machineService
                    .PauseAsync(
                        cancellationToken
                    ),
            cancellationToken
        );
    }

    [HttpPost("resume")]
    public async Task<IActionResult>
        Resume(
            CancellationToken cancellationToken
        )
    {
        return await CommandResult(
            () =>
                _machineService
                    .ResumeAsync(
                        cancellationToken
                    ),
            cancellationToken
        );
    }

    [HttpPost("stop")]
    public async Task<IActionResult>
        Stop(
            CancellationToken cancellationToken
        )
    {
        return await CommandResult(
            () =>
                _machineService
                    .StopAsync(
                        cancellationToken
                    ),
            cancellationToken
        );
    }

    [HttpPost("reset")]
    public async Task<IActionResult>
        Reset(
            CancellationToken cancellationToken
        )
    {
        return await CommandResult(
            () =>
                _machineService
                    .ResetAsync(
                        cancellationToken
                    ),
            cancellationToken
        );
    }

    [HttpPost("estop")]
    public async Task<IActionResult>
        EmergencyStop(
            CancellationToken cancellationToken
        )
    {
        return await CommandResult(
            () =>
                _machineService
                    .EmergencyStopAsync(
                        cancellationToken
                    ),
            cancellationToken
        );
    }

    [HttpPost("clear-estop")]
    public async Task<IActionResult>
        ClearEmergencyStop(
            CancellationToken cancellationToken
        )
    {
        return await CommandResult(
            () =>
                _machineService
                    .ClearEmergencyStopAsync(
                        cancellationToken
                    ),
            cancellationToken
        );
    }

    private async Task<IActionResult>
        CommandResult(
            Func<Task<bool>> command,
            CancellationToken cancellationToken
        )
    {
        var success =
            await command();

        var status =
            await _machineService
                .GetStatusAsync(
                    cancellationToken
                );

        if (!success)
        {
            return Conflict(
                new
                {
                    success = false,
                    status
                }
            );
        }

        return Ok(
            new
            {
                success = true,
                status
            }
        );
    }
    [HttpPost("fault/motion-timeout")]
    public async Task<IActionResult>
        InjectMotionTimeoutFault(
            CancellationToken cancellationToken
        )
    {
        return await CommandResult(
            () =>
                _machineService
                    .InjectMotionTimeoutFaultAsync(
                        cancellationToken
                    ),
            cancellationToken
        );
    }
}