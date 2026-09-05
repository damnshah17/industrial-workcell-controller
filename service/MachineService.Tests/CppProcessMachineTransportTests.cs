using System.Diagnostics;
using MachineService.Transport;
using Microsoft.Extensions.Configuration;

namespace MachineService.Tests;

public sealed class CppProcessMachineTransportTests
{
    [Fact]
    public void MissingExecutableProducesClearStartupError()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Controller:ExecutablePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
            }
        ).Build();

        var error = Assert.Throws<FileNotFoundException>(
            () => new CppProcessMachineTransport(configuration)
        );
        Assert.Contains("Controller executable was not found", error.Message);
    }

    [Fact]
    public async Task ProcessConnectsRoundTripsAndShutsDownCleanly()
    {
        var executable = BridgeExecutable();
        if (executable is null) return;

        int processId;
        using (var transport = CreateTransport(executable, 3000))
        {
            processId = transport.ProcessId;
            Assert.Equal("Offline", (await transport.SendCommandAsync("status")).State.ToString());
            Assert.True((await transport.SendCommandAsync("initialize")).Success);
            Assert.True((await transport.SendCommandAsync("start")).Success);

            var commands = Enumerable.Range(0, 20)
                .Select(_ => transport.SendCommandAsync("status"));
            var responses = await Task.WhenAll(commands);
            Assert.All(responses, response => Assert.Equal("Running", response.State.ToString()));
        }

        await Task.Delay(100);
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(processId));
    }

    [Fact]
    public async Task TimeoutTerminatesTransportToPreventCorrelationCorruption()
    {
        var executable = BridgeExecutable();
        if (executable is null) return;

        using var transport = CreateTransport(executable, 50);
        await Assert.ThrowsAsync<TimeoutException>(
            () => transport.SendCommandAsync("diagnostic-delay")
        );
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.SendCommandAsync("status")
        );
    }

    [Fact]
    public async Task ControllerShutdownIsDetectedOnNextCommand()
    {
        var executable = BridgeExecutable();
        if (executable is null) return;

        using var transport = CreateTransport(executable, 3000);
        Assert.True((await transport.SendCommandAsync("shutdown")).Success);
        await Task.Delay(100);
        await Assert.ThrowsAnyAsync<Exception>(() => transport.SendCommandAsync("status"));
    }

    private static CppProcessMachineTransport CreateTransport(string executable, int timeout) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Controller:ExecutablePath"] = executable,
            ["Controller:StartupTimeoutMilliseconds"] = "3000",
            ["Controller:CommandTimeoutMilliseconds"] = timeout.ToString()
        }).Build());

    private static string? BridgeExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("WORKCELL_BRIDGE_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }
        var name = OperatingSystem.IsWindows() ? "machine_bridge.exe" : "machine_bridge";
        var local = Path.GetFullPath(Path.Combine("controller", "build", name));
        return File.Exists(local) ? local : null;
    }
}
