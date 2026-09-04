using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MachineService.Persistence;

public sealed class WorkcellDbContextFactory :
    IDesignTimeDbContextFactory<WorkcellDbContext>
{
    public WorkcellDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__WorkcellDatabase"
            ) ?? throw new InvalidOperationException(
                "Set ConnectionStrings__WorkcellDatabase before using EF tooling."
            );

        var options = new DbContextOptionsBuilder<WorkcellDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new WorkcellDbContext(options);
    }
}
