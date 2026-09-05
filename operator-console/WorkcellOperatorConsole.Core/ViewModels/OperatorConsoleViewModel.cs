using WorkcellOperatorConsole.Core.Models;
using WorkcellOperatorConsole.Core.Services;

namespace WorkcellOperatorConsole.Core.ViewModels;

public sealed class OperatorConsoleViewModel : ObservableObject, IDisposable
{
    private readonly IWorkcellApiClient _api;
    private readonly SynchronizationContext? _context;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CancellationTokenSource? _pollingCancellation;
    private MachineStatus? _status;
    private ProductionMetrics? _metrics;
    private IReadOnlyList<ProductionCycle> _cycles = [];
    private IReadOnlyList<FaultEvent> _faults = [];
    private IReadOnlyList<MachineEvent> _events = [];
    private string _connectionText = "CONNECTING";
    private string _operatorMessage = "Waiting for machine service…";
    private bool _isBusy;
    private DateTimeOffset? _lastUpdated;
    private string _selectedSample = "good-part";

    public OperatorConsoleViewModel(
        IWorkcellApiClient api,
        SynchronizationContext? synchronizationContext = null
    )
    {
        _api = api;
        _context = synchronizationContext ?? SynchronizationContext.Current;

        InitializeCommand = CreateCommand("initialize");
        StartCommand = CreateCommand("start");
        PauseCommand = CreateCommand("pause");
        ResumeCommand = CreateCommand("resume");
        StopCommand = CreateCommand("stop");
        ResetCommand = CreateCommand("reset");
        EmergencyStopCommand = CreateCommand("estop");
        ClearEmergencyStopCommand = CreateCommand("clear-estop");
        StartVisionCycleCommand = new AsyncRelayCommand(
            ExecuteCycleAsync,
            () => !IsBusy
        );
        RefreshHistoryCommand = new AsyncRelayCommand(
            RefreshHistoryAsync,
            () => !IsBusy
        );
    }

    public MachineStatus? Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(MachineStateText));
                OnPropertyChanged(nameof(CycleStateText));
                OnPropertyChanged(nameof(RobotPositionText));
                OnPropertyChanged(nameof(RobotMotionText));
                OnPropertyChanged(nameof(ConveyorText));
                OnPropertyChanged(nameof(GripperText));
                OnPropertyChanged(nameof(SensorText));
                OnPropertyChanged(nameof(SafetyText));
                OnPropertyChanged(nameof(FaultText));
                OnPropertyChanged(nameof(HasFault));
                OnPropertyChanged(nameof(InspectionText));
                OnPropertyChanged(nameof(InspectionDetailsText));
            }
        }
    }

    public ProductionMetrics? Metrics { get => _metrics; private set => SetProperty(ref _metrics, value); }
    public IReadOnlyList<ProductionCycle> Cycles { get => _cycles; private set => SetProperty(ref _cycles, value); }
    public IReadOnlyList<FaultEvent> Faults { get => _faults; private set => SetProperty(ref _faults, value); }
    public IReadOnlyList<MachineEvent> Events { get => _events; private set => SetProperty(ref _events, value); }
    public string ConnectionText { get => _connectionText; private set => SetProperty(ref _connectionText, value); }
    public string OperatorMessage { get => _operatorMessage; private set => SetProperty(ref _operatorMessage, value); }
    public DateTimeOffset? LastUpdated { get => _lastUpdated; private set => SetProperty(ref _lastUpdated, value); }
    public IReadOnlyList<string> InspectionSamples { get; } = ["good-part", "missing-hole", "malformed-part", "unreadable-part"];
    public string SelectedSample { get => _selectedSample; set => SetProperty(ref _selectedSample, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string MachineStateText => Status?.State.ToString().ToUpperInvariant() ?? "UNKNOWN";
    public string CycleStateText => Status?.Cycle.State ?? "Unavailable";
    public string RobotPositionText => Status?.Robot.Position ?? "—";
    public string RobotMotionText => Status?.Robot.Moving == true ? "MOVING" : "STATIONARY";
    public string ConveyorText => Status?.Conveyor.Running == true ? "RUNNING" : "STOPPED";
    public string GripperText => Status?.Gripper.Open == true ? "OPEN" : "CLOSED";
    public string SensorText => Status?.PartSensor.Active == true ? "PART PRESENT" : "CLEAR";
    public string SafetyText => Status?.EmergencyStopActive == true ? "E-STOP ACTIVE" : "SAFETY OK";
    public string FaultText => Status?.ActiveFault is { } fault ? $"{fault.Code}: {fault.Message}" : "NO ACTIVE FAULT";
    public bool HasFault => Status?.ActiveFault is not null;
    public string InspectionText => Status?.Inspection switch
    {
        { State: "Complete", Accepted: true } inspection => $"PASS — {inspection.Reason}",
        { State: "Complete", Accepted: false } inspection => $"FAIL — {inspection.Reason}",
        { } inspection => inspection.State.ToUpperInvariant(),
        null => "IDLE"
    };
    public string InspectionDetailsText => Status?.Inspection?.Details ?? "No inspection completed.";

    public AsyncRelayCommand InitializeCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand PauseCommand { get; }
    public AsyncRelayCommand ResumeCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand ResetCommand { get; }
    public AsyncRelayCommand EmergencyStopCommand { get; }
    public AsyncRelayCommand ClearEmergencyStopCommand { get; }
    public AsyncRelayCommand StartVisionCycleCommand { get; }
    public AsyncRelayCommand RefreshHistoryCommand { get; }

    public void StartPolling()
    {
        if (_pollingCancellation is not null)
        {
            return;
        }

        _pollingCancellation = new CancellationTokenSource();
        _ = PollAsync(_pollingCancellation.Token);
    }

    public async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = await _api.GetStatusAsync(cancellationToken);
        RunOnContext(() => ApplyStatus(status, "Live telemetry updated."));
    }

    public async Task RefreshHistoryAsync()
    {
        if (!await _refreshLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var cyclesTask = _api.GetCyclesAsync(1, 100);
            var faultsTask = _api.GetFaultsAsync(1, 100);
            var eventsTask = _api.GetEventsAsync(1, 100);
            var metricsTask = _api.GetMetricsAsync();
            await Task.WhenAll(cyclesTask, faultsTask, eventsTask, metricsTask);

            RunOnContext(() =>
            {
                Cycles = cyclesTask.Result.Items;
                Faults = faultsTask.Result.Items;
                Events = eventsTask.Result.Items;
                Metrics = metricsTask.Result;
            });
        }
        catch (Exception exception)
        {
            RunOnContext(() => OperatorMessage = $"History unavailable: {exception.Message}");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private AsyncRelayCommand CreateCommand(string command) => new(
        () => ExecuteCommandAsync(command),
        () => !IsBusy
    );

    private async Task ExecuteCommandAsync(string command)
    {
        await ExecuteOperatorActionAsync(
            token => _api.SendCommandAsync(command, token),
            $"Command '{command}' accepted."
        );
    }

    private async Task ExecuteCycleAsync()
    {
        await ExecuteOperatorActionAsync(
            token => _api.StartCycleAsync(SelectedSample, token),
            $"Vision cycle started with sample '{SelectedSample}'."
        );
    }

    private async Task ExecuteOperatorActionAsync(
        Func<CancellationToken, Task<MachineStatus>> operation,
        string successMessage
    )
    {
        IsBusy = true;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var status = await operation(timeout.Token);
            ApplyStatus(status, successMessage);
            _ = RefreshHistoryAsync();
        }
        catch (MachineCommandRejectedException exception)
        {
            ApplyStatus(exception.Status, exception.Message);
        }
        catch (Exception exception)
        {
            ConnectionText = "DISCONNECTED";
            OperatorMessage = $"Machine service error: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        var pollCount = 0;

        try
        {
            await RefreshStatusAsync(cancellationToken);
            await RefreshHistoryAsync();

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await RefreshStatusAsync(cancellationToken);
                    if (++pollCount % 10 == 0)
                    {
                        await RefreshHistoryAsync();
                    }
                }
                catch (Exception exception)
                {
                    RunOnContext(() =>
                    {
                        ConnectionText = "DISCONNECTED";
                        OperatorMessage = $"Telemetry unavailable: {exception.Message}";
                    });
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void ApplyStatus(MachineStatus status, string message)
    {
        Status = status;
        ConnectionText = "CONNECTED";
        OperatorMessage = message;
        LastUpdated = DateTimeOffset.Now;
    }

    private void RunOnContext(Action action)
    {
        if (_context is null || SynchronizationContext.Current == _context)
        {
            action();
            return;
        }

        _context.Post(_ => action(), null);
    }

    private void RaiseCommandStates()
    {
        InitializeCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        ResetCommand.RaiseCanExecuteChanged();
        EmergencyStopCommand.RaiseCanExecuteChanged();
        ClearEmergencyStopCommand.RaiseCanExecuteChanged();
        StartVisionCycleCommand.RaiseCanExecuteChanged();
        RefreshHistoryCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _pollingCancellation?.Cancel();
        _pollingCancellation?.Dispose();
    }
}
