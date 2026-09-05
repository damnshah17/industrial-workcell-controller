using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using MachineService.Models;
using MachineService.Reliability;

namespace MachineService.Transport;

public sealed class CppProcessMachineTransport : IMachineTransport, IControllerTransportHealth, IDisposable
{
    private Process? _process;
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly TimeSpan _commandTimeout;
    private readonly ILogger<CppProcessMachineTransport>? _logger;
    private readonly string _executablePath;
    private readonly TimeSpan _startupTimeout;
    private readonly int _maxRestartAttempts;
    private readonly TimeSpan _restartBackoff;
    private readonly TimeSpan _recoveryCooldown;
    private readonly object _healthLock = new();
    private int _restartCount;
    private DateTimeOffset? _lastConnectedAt;
    private string? _lastError;
    private DateTimeOffset? _nextRestartAllowedAt;
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
        _executablePath = Path.GetFullPath(executablePath);
        if (!File.Exists(_executablePath))
        {
            throw new FileNotFoundException("Controller executable was not found.", _executablePath);
        }

        _startupTimeout = TimeSpan.FromMilliseconds(
            configuration.GetValue("Controller:StartupTimeoutMilliseconds", 5000)
        );
        _commandTimeout = TimeSpan.FromMilliseconds(
            configuration.GetValue("Controller:CommandTimeoutMilliseconds", 3000)
        );
        _maxRestartAttempts = Math.Clamp(
            configuration.GetValue("Controller:MaxRestartAttempts", 3), 1, 10
        );
        _restartBackoff = TimeSpan.FromMilliseconds(
            configuration.GetValue("Controller:RestartBackoffMilliseconds", 250)
        );
        _recoveryCooldown = TimeSpan.FromMilliseconds(
            configuration.GetValue("Controller:RecoveryCooldownMilliseconds", 5000)
        );
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());

        try
        {
            StartAndConnectAsync(false, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            StopProcess();
            RecordFailure(exception.Message);
            _logger?.LogError(exception, "Initial controller startup failed; service will remain available for health and bounded recovery.");
        }
    }

    public async Task<ControllerResponse> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_commandTimeout);
        try
        {
            await _commandLock.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Controller command could not start within the {_commandTimeout.TotalMilliseconds:0} ms timeout."
            );
        }
        var started = Stopwatch.GetTimestamp();
        string? requestId = null;
        try
        {
            await EnsureAvailableAsync(timeout.Token);
            var request = CreateRequest(command);
            requestId = request.RequestId;
            _logger?.Log(
                command == "status" ? LogLevel.Debug : LogLevel.Information,
                "Controller command {Command} starting; RequestId={RequestId} ProcessId={ProcessId}",
                command, requestId, _process?.Id
            );
            await _writer!.WriteLineAsync(
                JsonSerializer.Serialize(request, _jsonOptions).AsMemory(),
                timeout.Token
            );
            var line = await _reader!.ReadLineAsync(timeout.Token);
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
            var status = response.Status ?? throw new InvalidDataException(
                $"Controller IPC error {response.Error?.Code ?? "UNKNOWN"}: {response.Error?.Message ?? "No status returned."}"
            );
            lock (_healthLock)
            {
                _lastError = null;
            }
            _logger?.Log(
                command == "status" ? LogLevel.Debug : LogLevel.Information,
                "Controller command {Command} completed; RequestId={RequestId} ProcessId={ProcessId} Success={Success} DurationMs={DurationMs:F1}",
                command, requestId, _process?.Id, status.Success,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds
            );
            return status;
        }
        catch (OperationCanceledException)
        {
            StopProcess();
            RecordFailure($"Command '{command}' timed out.");
            _logger?.LogWarning(
                "Controller command {Command} timed out; RequestId={RequestId} DurationMs={DurationMs:F1}",
                command, requestId, Stopwatch.GetElapsedTime(started).TotalMilliseconds
            );
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            throw new TimeoutException(
                $"Controller command exceeded the {_commandTimeout.TotalMilliseconds:0} ms timeout."
            );
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or SocketException)
        {
            RecordFailure(exception.Message);
            StopProcess();
            _logger?.LogError(
                exception,
                "Controller command {Command} failed; RequestId={RequestId} DurationMs={DurationMs:F1}",
                command, requestId, Stopwatch.GetElapsedTime(started).TotalMilliseconds
            );
            throw new ControllerUnavailableException("Controller transport is unavailable.", exception);
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

    private async Task ConnectAsync(int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        Exception? lastError = null;
        try
        {
            while (true)
            {
            if (_process!.HasExited)
                {
                    throw new InvalidOperationException(
                        $"C++ controller exited during startup with code {_process.ExitCode}."
                    );
                }
                try
                {
                await _client!.ConnectAsync(IPAddress.Loopback, port, timeoutSource.Token);
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

    private async Task EnsureAvailableAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false } && _client?.Connected == true)
        {
            return;
        }

        lock (_healthLock)
        {
            if (_nextRestartAllowedAt is { } next && next > DateTimeOffset.UtcNow)
            {
                throw new ControllerUnavailableException(
                    $"Controller recovery is cooling down until {next:O}.");
            }
        }

        Exception? lastError = null;
        for (var attempt = 1; attempt <= _maxRestartAttempts; attempt++)
        {
            try
            {
                await StartAndConnectAsync(true, cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested)
            {
                lastError = exception;
                RecordFailure(exception.Message);
                StopProcess();
                if (attempt < _maxRestartAttempts)
                {
                    await Task.Delay(_restartBackoff, cancellationToken);
                }
            }
        }
        lock (_healthLock) _nextRestartAllowedAt = DateTimeOffset.UtcNow + _recoveryCooldown;
        throw new ControllerUnavailableException(
            $"Controller restart failed after {_maxRestartAttempts} attempts.", lastError
        );
    }

    private async Task StartAndConnectAsync(bool restart, CancellationToken cancellationToken)
    {
        StopProcess();
        _client = new TcpClient();
        var port = ReserveLoopbackPort();
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = $"--tcp-port {port}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        if (!_process.Start()) throw new InvalidOperationException("Failed to start C++ controller process.");
        _process.OutputDataReceived += (_, args) => LogControllerLine(args.Data, false);
        _process.ErrorDataReceived += (_, args) => LogControllerLine(args.Data, true);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        await ConnectAsync(port, _startupTimeout, cancellationToken);
        var stream = _client.GetStream();
        stream.ReadTimeout = 1000;
        stream.WriteTimeout = 1000;
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
        lock (_healthLock)
        {
            if (restart) ++_restartCount;
            _lastConnectedAt = DateTimeOffset.UtcNow;
            _lastError = null;
            _nextRestartAllowedAt = null;
        }
        _logger?.LogInformation(
            "Controller {Operation}; ProcessId={ProcessId} Port={Port}",
            restart ? "restarted" : "started", _process.Id, port
        );
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    internal int ProcessId => _process!.Id;
    internal void TerminateForTest() => _process!.Kill(entireProcessTree: true);

    public ControllerTransportHealth GetHealth()
    {
        lock (_healthLock)
        {
            var connected = !_disposed
                && _process is { HasExited: false }
                && _client?.Connected == true;
            return new(
                connected ? ComponentStatus.Healthy : ComponentStatus.Unhealthy,
                connected ? "Controller process and IPC connection are available." : "Controller transport is unavailable.",
                connected ? _process?.Id : null,
                _restartCount,
                _lastConnectedAt,
                _lastError
            );
        }
    }

    private void RecordFailure(string message)
    {
        lock (_healthLock) _lastError = message;
    }

    private void LogControllerLine(string? message, bool error)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (error) _logger?.LogWarning("Controller stderr: {Message}", message);
        else _logger?.LogDebug("Controller: {Message}", message);
    }

    private void StopProcess()
    {
        try
        {
            if (_process is { HasExited: false } && !_process.WaitForExit(1000))
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(1000);
            }
        }
        catch (InvalidOperationException)
        {
            // The process may have exited between the state check and cleanup.
        }
        finally
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _client?.Dispose();
            _process?.Dispose();
            _writer = null;
            _reader = null;
            _client = null;
            _process = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_client?.Connected == true && _process is { HasExited: false })
            {
                var request = new IpcRequest(Guid.NewGuid().ToString("N"), "shutdown", new { });
                _writer!.WriteLine(JsonSerializer.Serialize(request, _jsonOptions));
                _reader!.ReadLine();
            }
        }
        catch
        {
            // The socket or child process may already be closing.
        }
        finally
        {
            StopProcess();
            _commandLock.Dispose();
        }
    }
}
