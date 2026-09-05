using System.Text.Json.Serialization;
using MachineService.Services;
using MachineService.Transport;
using MachineService.Persistence;
using Microsoft.EntityFrameworkCore;
using MachineService.Reliability;

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

builder.Services.AddSingleton<CppProcessMachineTransport>();
builder.Services.AddSingleton<IMachineTransport>(provider =>
    provider.GetRequiredService<CppProcessMachineTransport>()
);
builder.Services.AddSingleton<IControllerTransportHealth>(provider =>
    provider.GetRequiredService<CppProcessMachineTransport>()
);

var connectionString = builder.Configuration.GetConnectionString(
    "WorkcellDatabase"
) ?? throw new InvalidOperationException(
    "Connection string 'WorkcellDatabase' is not configured."
);

builder.Services.AddPooledDbContextFactory<WorkcellDbContext>(
    options => options.UseNpgsql(connectionString)
);
builder.Services.AddSingleton<CppMachineService>();
builder.Services.AddSingleton<MachineHistoryTracker>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<HistoryWriteQueue>();
builder.Services.AddSingleton<PersistenceHealthState>();
builder.Services.AddSingleton<IHistoryWriteQueue>(
    provider => provider.GetRequiredService<HistoryWriteQueue>()
);
builder.Services.AddSingleton<IMachineService, PersistentMachineService>();
builder.Services.AddSingleton<IHistoryService, HistoryService>();
builder.Services.AddSingleton<ISimulationService, CppSimulationService>();
builder.Services.AddSingleton<ISystemHealthService, SystemHealthService>();
builder.Services.AddHostedService<HistoryWriterService>();
builder.Services.AddHostedService<ControllerHistoryObserver>();

var app =
    builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.MapControllers();

app.Run();
