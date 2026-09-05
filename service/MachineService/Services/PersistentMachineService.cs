using MachineService.Models;
using MachineService.Persistence;

namespace MachineService.Services;

public sealed class PersistentMachineService(
    CppMachineService controller,
    MachineHistoryTracker history
) : IMachineService
{
    public async Task<MachineStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        var status = await controller.GetStatusAsync(cancellationToken);
        history.Observe(status);
        return status;
    }

    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(controller.InitializeAsync, "Initialized", "Machine initialized.", cancellationToken);

    public Task<bool> StartAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(controller.StartAsync, "Started", "Machine started.", cancellationToken);

    public async Task<bool> StartCycleAsync(
        bool inspectionAccepted,
        CancellationToken cancellationToken = default
    )
    {
        var success = await controller.StartCycleAsync(
            inspectionAccepted,
            cancellationToken
        );
        if (success)
        {
            var status = await controller.GetStatusAsync(cancellationToken);
            history.RecordCycleStarted(inspectionAccepted, status);
        }
        return success;
    }

    public async Task<bool> StartCycleAsync(
        string sampleId,
        CancellationToken cancellationToken = default
    )
    {
        var success = await controller.StartCycleAsync(sampleId, cancellationToken);
        if (success)
        {
            var status = await controller.GetStatusAsync(cancellationToken);
            history.RecordCycleStarted(sampleId, status);
        }
        return success;
    }

    public Task<bool> PauseAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(controller.PauseAsync, "Paused", "Machine paused.", cancellationToken);

    public Task<bool> ResumeAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(controller.ResumeAsync, "Resumed", "Machine resumed.", cancellationToken);

    public Task<bool> StopAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(controller.StopAsync, "Stopped", "Machine stopped.", cancellationToken);

    public Task<bool> ResetAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(controller.ResetAsync, "Reset", "Machine reset.", cancellationToken);

    public Task<bool> EmergencyStopAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(controller.EmergencyStopAsync, "EmergencyStopActivated", "Emergency Stop activated.", cancellationToken);

    public Task<bool> ClearEmergencyStopAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(controller.ClearEmergencyStopAsync, "EmergencyStopCleared", "Emergency Stop condition cleared.", cancellationToken);

    public Task<bool> InjectMotionTimeoutFaultAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(controller.InjectMotionTimeoutFaultAsync, "FaultInjected", "Motion-timeout fault injected.", cancellationToken);

    private async Task<bool> ExecuteAsync(
        Func<CancellationToken, Task<bool>> command,
        string eventType,
        string message,
        CancellationToken cancellationToken
    )
    {
        var success = await command(cancellationToken);
        if (success)
        {
            var status = await controller.GetStatusAsync(cancellationToken);
            history.RecordCommand(eventType, message, status);
        }
        return success;
    }
}
