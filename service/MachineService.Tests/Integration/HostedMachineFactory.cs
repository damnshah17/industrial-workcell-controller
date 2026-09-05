using MachineService.Persistence;
using MachineService.Models;
using MachineService.Transport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace MachineService.Tests.Integration;

internal sealed class HostedMachineFactory(
    string bridgePath,
    string? postgresConnection = null,
    bool controllerUnavailable = false
) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.UseSetting("Controller:ExecutablePath", bridgePath);
        builder.UseSetting("Controller:StartupTimeoutMilliseconds", "3000");
        builder.UseSetting("Controller:CommandTimeoutMilliseconds", "3000");
        builder.UseSetting("Controller:RestartBackoffMilliseconds", "10");
        builder.UseSetting(
            "ConnectionStrings:WorkcellDatabase",
            postgresConnection ?? "Host=unused;Database=unused"
        );
        builder.ConfigureServices(services =>
        {
            if (postgresConnection is null)
            {
                services.RemoveAll<IDbContextFactory<WorkcellDbContext>>();
                services.RemoveAll<DbContextOptions<WorkcellDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<WorkcellDbContext>>();
                services.AddPooledDbContextFactory<WorkcellDbContext>(options =>
                    options.UseInMemoryDatabase($"hosted-{Guid.NewGuid():N}")
                );
            }
            if (controllerUnavailable)
            {
                services.RemoveAll<IMachineTransport>();
                services.AddSingleton<IMachineTransport, UnavailableTransport>();
            }
        });
    }

    public async Task PrepareDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkcellDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        if (postgresConnection is null)
        {
            await db.Database.EnsureCreatedAsync();
            return;
        }
        await db.Database.MigrateAsync();
        await db.MachineEvents.ExecuteDeleteAsync();
        await db.FaultEvents.ExecuteDeleteAsync();
        await db.ProductionCycles.ExecuteDeleteAsync();
    }

    internal static string FindBridge()
    {
        var configured = Environment.GetEnvironmentVariable("WORKCELL_BRIDGE_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured))
        {
            if (File.Exists(configured)) return Path.GetFullPath(configured);
            throw MissingBridge(configured);
        }
        var name = OperatingSystem.IsWindows() ? "machine_bridge.exe" : "machine_bridge";
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var configuredCandidate = Path.Combine(directory.FullName, configured);
                if (File.Exists(configuredCandidate)) return Path.GetFullPath(configuredCandidate);
            }
            var candidate = Path.Combine(directory.FullName, "controller", "build", name);
            if (File.Exists(candidate)) return candidate;
        }
        throw MissingBridge(configured);
    }

    private static FileNotFoundException MissingBridge(string? configured) => new(
        string.IsNullOrWhiteSpace(configured)
            ? "Build machine_bridge or set WORKCELL_BRIDGE_PATH before running hosted integration tests."
            : $"machine_bridge was not found at configured path '{configured}' relative to the repository root."
    );

    private sealed class UnavailableTransport : IMachineTransport
    {
        public Task<ControllerResponse> SendCommandAsync(string command, CancellationToken cancellationToken = default) =>
            throw new ControllerUnavailableException("Unavailable for hosted test.");
    }
}
