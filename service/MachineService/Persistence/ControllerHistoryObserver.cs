using MachineService.Services;

namespace MachineService.Persistence;

public sealed class ControllerHistoryObserver(
    IMachineService machineService,
    ILogger<ControllerHistoryObserver> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken
    )
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(250)
        );

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await machineService.GetStatusAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (
                stoppingToken.IsCancellationRequested
            )
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Unable to observe controller telemetry for history."
                );
            }
        }
    }
}
