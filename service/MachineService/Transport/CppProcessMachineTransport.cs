using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using MachineService.Models;

namespace MachineService.Transport;

public sealed class CppProcessMachineTransport : IMachineTransport, IDisposable
{
    private readonly Process _process;
    private readonly TcpClient _client = new();
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly TimeSpan _commandTimeout;
    private readonly ILogger<CppProcessMachineTransport>? _logger;
    private bool _disposed;

    public CppProcessMachineTransport(
        IConfiguration configuration,
        ILogger<CppProcessMachineTransport>? logger = null
    )
    {
        _logger = logger;
        var executablePath = configuration["Controller:ExecutablePath"];
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("Controller executable path is not configured.");
        }
        executablePath = Path.GetFullPath(executablePath);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("Controller executable was not found.", executablePath);
        }

        var startupTimeout = TimeSpan.FromMilliseconds(
            configuration.GetValue("Controller:StartupTimeoutMilliseconds", 5000)
        );
        _commandTimeout = TimeSpan.FromMilliseconds(
            configuration.GetValue("Controller:CommandTimeoutMilliseconds", 3000)
        );
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());

        var port = ReserveLoopbackPort();
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--tcp-port {port}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        if (!_process.Start())
        {
            throw new InvalidOperationException("Failed to start C++ controller process.");
        }
        _process.OutputDataReceived += (_, args) => LogControllerLine(args.Data, false);
        _process.ErrorDataReceived += (_, args) => LogControllerLine(args.Data, true);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        try
        {
            ConnectAsync(port, startupTimeout).GetAwaiter().GetResult();
        }
        catch
        {
            StopProcess();
            throw;
        }

        var stream = _client.GetStream();
        stream.ReadTimeout = 1000;
        stream.WriteTimeout = 1000;
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    public async Task<ControllerResponse> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_commandTimeout);
        await _commandLock.WaitAsync(timeout.Token);
        try
        {
            EnsureProcessRunning();
            var request = CreateRequest(command);
            await _writer.WriteLineAsync(
                JsonSerializer.Serialize(request, _jsonOptions).AsMemory(),
                timeout.Token
            );
            var line = await _reader.ReadLineAsync(timeout.Token);
            if (line is null)
            {
                throw new IOException("Controller IPC connection closed before a response was received.");
            }
            var response = JsonSerializer.Deserialize<IpcResponse>(line, _jsonOptions)
                ?? throw new InvalidDataException("Controller returned an empty IPC response.");
            if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"IPC correlation failed: expected {request.RequestId}, received {response.RequestId}."
                );
            }
            return response.Status ?? throw new InvalidDataException(
                $"Controller IPC error {response.Error?.Code ?? "UNKNOWN"}: {response.Error?.Message ?? "No status returned."}"
            );
        }
        catch (OperationCanceledException)
        {
            StopProcess();
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            throw new TimeoutException(
                $"Controller command exceeded the {_commandTimeout.TotalMilliseconds:0} ms timeout."
            );
        }
        finally
        {
            _commandLock.Release();
        }
    }

    private static IpcRequest CreateRequest(string command)
    {
        var requestId = Guid.NewGuid().ToString("N");
        if (command.StartsWith("cycle-sample-", StringComparison.Ordinal))
        {
            return new(requestId, "start-cycle", new { sampleId = command["cycle-sample-".Length..] });
        }
        if (command.StartsWith("simulation-fault-", StringComparison.Ordinal)
            && command != "simulation-faults-clear")
        {
            var fault = command["simulation-fault-".Length..];
            var enabled = !fault.EndsWith("-clear", StringComparison.Ordinal);
            if (!enabled)
            {
                fault = fault[..^"-clear".Length];
            }
            return new(requestId, "configure-simulation-fault", new { fault, enabled });
        }
        return new(requestId, command, new { });
    }

    private async Task ConnectAsync(int port, TimeSpan timeout)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        Exception? lastError = null;
        try
        {
            while (true)
            {
                if (_process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"C++ controller exited during startup with code {_process.ExitCode}."
                    );
                }
                try
                {
                    await _client.ConnectAsync(IPAddress.Loopback, port, timeoutSource.Token);
                    return;
                }
                catch (SocketException exception)
                {
                    lastError = exception;
                    await Task.Delay(25, timeoutSource.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Timed out waiting for the C++ controller IPC server.", lastError);
        }
    }

    private void EnsureProcessRunning()
    {
        if (_process.HasExited)
        {
            throw new InvalidOperationException($"C++ controller exited with code {_process.ExitCode}.");
        }
        if (!_client.Connected)
        {
            throw new IOException("Controller IPC connection is not connected.");
        }
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    internal int ProcessId => _process.Id;

    private void LogControllerLine(string? message, bool error)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (error) _logger?.LogWarning("Controller stderr: {Message}", message);
        else _logger?.LogDebug("Controller: {Message}", message);
    }

    private void StopProcess()
    {
        if (!_process.HasExited && !_process.WaitForExit(1000))
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(1000);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_client.Connected && !_process.HasExited)
            {
                var request = new IpcRequest(Guid.NewGuid().ToString("N"), "shutdown", new { });
                _writer.WriteLine(JsonSerializer.Serialize(request, _jsonOptions));
                _reader.ReadLine();
            }
        }
        catch
        {
            // The socket or child process may already be closing.
        }
        finally
        {
            _client.Dispose();
            StopProcess();
            _process.Dispose();
            _commandLock.Dispose();
        }
    }
}
