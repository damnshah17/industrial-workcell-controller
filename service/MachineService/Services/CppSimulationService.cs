using MachineService.Models;
using MachineService.Transport;

namespace MachineService.Services;

public sealed class CppSimulationService(
    IMachineTransport transport
) : ISimulationService
{
    public async Task<(bool Success, MachineStatus Status)> ConfigureFaultAsync(
        SimulationFaultType faultType,
        bool enabled,
        CancellationToken cancellationToken = default
    )
    {
        var faultName = faultType switch
        {
            SimulationFaultType.RobotCommunication => "robot-communication",
            SimulationFaultType.MotionTimeout => "motion-timeout",
            SimulationFaultType.ConveyorStart => "conveyor-start",
            SimulationFaultType.ConveyorStop => "conveyor-stop",
            SimulationFaultType.GripperOpen => "gripper-open",
            SimulationFaultType.GripperClose => "gripper-close",
            SimulationFaultType.Sensor => "sensor",
            SimulationFaultType.SafetyDoor => "safety-door",
            _ => throw new ArgumentOutOfRangeException(nameof(faultType))
        };
        var suffix = enabled ? string.Empty : "-clear";
        var response = await transport.SendCommandAsync(
            $"simulation-fault-{faultName}{suffix}",
            cancellationToken
        );
        return (response.Success, CppMachineService.ToStatus(response));
    }

    public async Task<(bool Success, MachineStatus Status)> ClearAllFaultsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var response = await transport.SendCommandAsync(
            "simulation-faults-clear",
            cancellationToken
        );
        return (response.Success, CppMachineService.ToStatus(response));
    }
}
