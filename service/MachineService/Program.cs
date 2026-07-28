using System.Text.Json.Serialization;
using MachineService.Services;
using MachineService.Transport;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions
            .Converters
            .Add(
                new JsonStringEnumConverter()
            );
    });

builder.Services.AddSingleton<
    IMachineTransport,
    CppProcessMachineTransport
>();

builder.Services.AddSingleton<
    IMachineService,
    CppMachineService
>();

var app =
    builder.Build();

app.MapControllers();

app.Run();