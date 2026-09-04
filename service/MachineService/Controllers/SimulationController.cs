using MachineService.Models;
using MachineService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MachineService.Controllers;

[ApiController]
[Route("api/simulation/faults")]
public sealed class SimulationController(
    ISimulationService simulationService
) : ControllerBase
{
    [HttpPost("{faultType}")]
    public async Task<IActionResult> EnableFault(
        string faultType,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseFault(faultType, out var parsed))
        {
            return BadRequest(new { message = $"Unknown simulation fault '{faultType}'." });
        }

        var result = await simulationService.ConfigureFaultAsync(
            parsed,
            true,
            cancellationToken
        );
        return ToActionResult(result);
    }

    [HttpPost("{faultType}/clear")]
    public async Task<IActionResult> ClearFault(
        string faultType,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseFault(faultType, out var parsed))
        {
            return BadRequest(new { message = $"Unknown simulation fault '{faultType}'." });
        }

        var result = await simulationService.ConfigureFaultAsync(
            parsed,
            false,
            cancellationToken
        );
        return ToActionResult(result);
    }

    [HttpPost("clear")]
    public async Task<IActionResult> ClearAllFaults(
        CancellationToken cancellationToken
    ) => ToActionResult(
        await simulationService.ClearAllFaultsAsync(cancellationToken)
    );

    private IActionResult ToActionResult(
        (bool Success, MachineStatus Status) result
    ) => result.Success
        ? Ok(new { success = true, status = result.Status })
        : Conflict(new { success = false, status = result.Status });

    private static bool TryParseFault(
        string value,
        out SimulationFaultType faultType
    )
    {
        var result = value.ToLowerInvariant() switch
        {
            "robot-communication" => SimulationFaultType.RobotCommunication,
            "motion-timeout" => SimulationFaultType.MotionTimeout,
            "conveyor-start" => SimulationFaultType.ConveyorStart,
            "conveyor-stop" => SimulationFaultType.ConveyorStop,
            "gripper-open" => SimulationFaultType.GripperOpen,
            "gripper-close" => SimulationFaultType.GripperClose,
            "sensor" => SimulationFaultType.Sensor,
            "safety-door" => SimulationFaultType.SafetyDoor,
            _ => (SimulationFaultType?)null
        };
        faultType = result.GetValueOrDefault();
        return result.HasValue;
    }
}
