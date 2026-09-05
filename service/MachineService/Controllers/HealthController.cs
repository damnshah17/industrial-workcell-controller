using MachineService.Reliability;
using Microsoft.AspNetCore.Mvc;

namespace MachineService.Controllers;

[ApiController]
public sealed class HealthController(ISystemHealthService health) : ControllerBase
{
    [HttpGet("/health")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await health.GetHealthAsync(cancellationToken));
}
