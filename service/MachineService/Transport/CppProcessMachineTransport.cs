using System.Diagnostics;
using System.Text.Json;
using MachineService.Models;

namespace MachineService.Transport;

public sealed class CppProcessMachineTransport :
    IMachineTransport,
    IDisposable
{
    private const string ResponsePrefix =
        "@@RESPONSE@@";

    private readonly Process _process;

    private readonly SemaphoreSlim _commandLock =
        new(1, 1);

    private readonly JsonSerializerOptions
        _jsonOptions;

    public CppProcessMachineTransport(
        IConfiguration configuration
    )
    {
        var executablePath =
            configuration[
                "Controller:ExecutablePath"
            ];

        if (
            string.IsNullOrWhiteSpace(
                executablePath
            )
        )
        {
            throw new InvalidOperationException(
                "Controller executable path is not configured."
            );
        }

        executablePath =
            Path.GetFullPath(
                executablePath
            );

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "Controller executable was not found.",
                executablePath
            );
        }

        _jsonOptions =
            new JsonSerializerOptions(
                JsonSerializerDefaults.Web
            );

        _jsonOptions.Converters.Add(
            new System.Text.Json.Serialization
                .JsonStringEnumConverter()
        );

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    executablePath,

                RedirectStandardInput =
                    true,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    true
            };

        _process =
            new Process
            {
                StartInfo =
                    startInfo
            };

        if (!_process.Start())
        {
            throw new InvalidOperationException(
                "Failed to start C++ controller process."
            );
        }
    }

    public async Task<ControllerResponse>
        SendCommandAsync(
            string command,
            CancellationToken cancellationToken =
                default
        )
    {
        await _commandLock.WaitAsync(
            cancellationToken
        );

        try
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    "C++ controller process has exited."
                );
            }

            await _process.StandardInput
                .WriteLineAsync(command);

            await _process.StandardInput
                .FlushAsync();

            while (true)
            {
                var line =
                    await _process.StandardOutput
                        .ReadLineAsync(
                            cancellationToken
                        );

                if (line is null)
                {
                    throw new InvalidOperationException(
                        "C++ controller process closed its output."
                    );
                }

                if (
                    !line.StartsWith(
                        ResponsePrefix,
                        StringComparison.Ordinal
                    )
                )
                {
                    continue;
                }

                var json =
                    line[
                        ResponsePrefix.Length..
                    ];

                var response =
                    JsonSerializer
                        .Deserialize<
                            ControllerResponse
                        >(
                            json,
                            _jsonOptions
                        );

                if (response is null)
                {
                    throw new InvalidOperationException(
                        "Unable to deserialize controller response."
                    );
                }

                return response;
            }
        }
        finally
        {
            _commandLock.Release();
        }
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            try
            {
                _process.StandardInput
                    .WriteLine("exit");

                _process.StandardInput
                    .Flush();
            }
            catch
            {
                // Process may already be shutting down.
            }

            if (
                !_process.WaitForExit(
                    1000
                )
            )
            {
                _process.Kill(
                    entireProcessTree: true
                );
            }
        }

        _process.Dispose();

        _commandLock.Dispose();
    }
}