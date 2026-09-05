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

        Assert.Throws<ArgumentException>(() => Process.GetProcessById(processId));
    }

    [Fact]
    public async Task CommandTimeoutIsBoundedAndMarksTransportUnavailable()
    {
        var executable = BridgeExecutable();

        using var transport = CreateTransport(executable, 150);
        await Assert.ThrowsAsync<TimeoutException>(
            () => transport.SendCommandAsync("diagnostic-delay")
        );
        Assert.Equal("Unhealthy", transport.GetHealth().Status.ToString());
    }

    [Fact]
    public async Task ControllerShutdownRecoversOnNextCommand()
    {
        var executable = BridgeExecutable();

        using var transport = CreateTransport(executable, 3000);
        var processId = transport.ProcessId;
        Assert.True((await transport.SendCommandAsync("shutdown")).Success);
        await WaitForExitAsync(processId);
        var status = await transport.SendCommandAsync("status");
        Assert.Equal("Offline", status.State.ToString());
        Assert.True(transport.GetHealth().RestartCount >= 1);
    }

    [Fact]
    public async Task UnexpectedProcessExitRecoversOnNextCommand()
    {
        var executable = BridgeExecutable();

        using var transport = CreateTransport(executable, 3000);
        var originalProcessId = transport.ProcessId;
        transport.TerminateForTest();
        await WaitForExitAsync(originalProcessId);
        Assert.Equal("Unhealthy", transport.GetHealth().Status.ToString());

        var status = await transport.SendCommandAsync("status");

        Assert.Equal("Offline", status.State.ToString());
        Assert.NotEqual(originalProcessId, transport.ProcessId);
        Assert.Equal(1, transport.GetHealth().RestartCount);
    }

    [Fact]
    public async Task UnrecoverableOutageFailsWithinBoundedRestartPolicy()
    {
        var executable = BridgeExecutable();
        var testDirectory = Path.Combine(Path.GetTempPath(), $"workcell-bridge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var testExecutable = Path.Combine(testDirectory, Path.GetFileName(executable));
        File.Copy(executable, testExecutable);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(testExecutable, File.GetUnixFileMode(executable));
        }
        try
        {
            using var transport = CreateTransport(testExecutable, 3000, 2, 10);
            transport.TerminateForTest();
            await WaitForExitAsync(transport.ProcessId);
            File.Delete(testExecutable);
            var started = Stopwatch.StartNew();

            await Assert.ThrowsAsync<ControllerUnavailableException>(
                () => transport.SendCommandAsync("status")
            );

            Assert.True(started.Elapsed < TimeSpan.FromSeconds(2));
            Assert.Equal("Unhealthy", transport.GetHealth().Status.ToString());
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CanceledInFlightRequestDoesNotCorruptRequestAfterReconnect()
    {
        using var transport = CreateTransport(BridgeExecutable(), 3000);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => transport.SendCommandAsync("diagnostic-delay", cancellation.Token)
        );
        var status = await transport.SendCommandAsync("status");

        Assert.Equal("Offline", status.State.ToString());
        Assert.Equal(1, transport.GetHealth().RestartCount);
    }

    private static CppProcessMachineTransport CreateTransport(
        string executable,
        int timeout,
        int restartAttempts = 3,
        int restartBackoff = 250
    ) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Controller:ExecutablePath"] = executable,
            ["Controller:StartupTimeoutMilliseconds"] = "3000",
            ["Controller:CommandTimeoutMilliseconds"] = timeout.ToString(),
            ["Controller:MaxRestartAttempts"] = restartAttempts.ToString(),
            ["Controller:RestartBackoffMilliseconds"] = restartBackoff.ToString()
        }).Build());

    private static async Task WaitForExitAsync(int processId)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return;
        }
        using (process)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await process.WaitForExitAsync(timeout.Token);
        }
    }

    private static string BridgeExecutable() => Integration.HostedMachineFactory.FindBridge();
}
