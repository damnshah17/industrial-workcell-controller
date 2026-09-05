namespace MachineService.Reliability;

public interface ISystemHealthService
{
    Task<SystemHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}
