using System.Text.Json.Serialization;
using MachineService.Services;
using MachineService.Transport;
using MachineService.Persistence;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddSingleton<IHistoryWriteQueue>(
    provider => provider.GetRequiredService<HistoryWriteQueue>()
);
builder.Services.AddSingleton<IMachineService, PersistentMachineService>();
builder.Services.AddSingleton<IHistoryService, HistoryService>();
builder.Services.AddHostedService<HistoryWriterService>();
builder.Services.AddHostedService<ControllerHistoryObserver>();

var app =
    builder.Build();

app.MapControllers();

app.Run();
