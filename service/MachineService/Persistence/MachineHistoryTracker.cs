using MachineService.Models;

namespace MachineService.Persistence;

public sealed class MachineHistoryTracker(
    IHistoryWriteQueue queue,
    TimeProvider timeProvider,
    ILogger<MachineHistoryTracker> logger
)
{
    private readonly object _sync = new();
    private Guid? _activeCycleId;
    private Guid? _activeFaultId;
    private string? _activeFaultCode;

    public void RecordCommand(
        string eventType,
        string message,
        MachineStatus status
    )
    {
        lock (_sync)
        {
            Enqueue(new MachineEventWrite(
                timeProvider.GetUtcNow(),
                eventType,
                status.State,
                message
            ));

            if (
                eventType == "Stopped"
                && _activeCycleId.HasValue
            )
            {
                var now = timeProvider.GetUtcNow();
                Enqueue(new CycleFinishedWrite(
                    _activeCycleId.Value,
                    now,
                    "Aborted",
                    false,
                    null,
                    null
                ));
                Enqueue(new MachineEventWrite(
                    now,
                    "CycleAborted",
                    status.State,
                    "Production cycle aborted."
                ));
                _activeCycleId = null;
            }

            ObserveCore(status);
        }
    }

    public void RecordCycleStarted(
        bool accepted,
        MachineStatus status
    )
    {
        lock (_sync)
        {
            if (_activeCycleId.HasValue)
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            _activeCycleId = Guid.NewGuid();
            Enqueue(new CycleStartedWrite(
                _activeCycleId.Value,
                now,
                accepted
            ));
            Enqueue(new MachineEventWrite(
                now,
                "CycleStarted",
                status.State,
                accepted
                    ? "Accepted-part production cycle started."
                    : "Rejected-part production cycle started."
            ));
            ObserveCore(status);
        }
    }

    public void RecordCycleStarted(string sampleId, MachineStatus status)
    {
        lock (_sync)
        {
            if (_activeCycleId.HasValue)
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            _activeCycleId = Guid.NewGuid();
            Enqueue(new CycleStartedWrite(_activeCycleId.Value, now, null, sampleId));
            Enqueue(new MachineEventWrite(
                now,
                "CycleStarted",
                status.State,
                $"Vision production cycle started with sample '{sampleId}'."
            ));
            ObserveCore(status);
        }
    }

    public void Observe(MachineStatus status)
    {
        lock (_sync)
        {
            ObserveCore(status);
        }
    }

    private void ObserveCore(MachineStatus status)
    {
        var now = timeProvider.GetUtcNow();

        if (
            status.ActiveFault is not null
            && status.ActiveFault.Code != _activeFaultCode
        )
        {
            _activeFaultId = Guid.NewGuid();
            _activeFaultCode = status.ActiveFault.Code;
            Enqueue(new FaultRaisedWrite(
                _activeFaultId.Value,
                now,
                status.ActiveFault.Code,
                status.ActiveFault.Message,
                status.State,
                status.Cycle.State
            ));
            Enqueue(new MachineEventWrite(
                now,
                "FaultRaised",
                status.State,
                $"{status.ActiveFault.Code}: {status.ActiveFault.Message}"
            ));
        }
        else if (
            status.ActiveFault is null
            && _activeFaultId.HasValue
        )
        {
            Enqueue(new FaultClearedWrite(
                _activeFaultId.Value,
                now
            ));
            Enqueue(new MachineEventWrite(
                now,
                "FaultCleared",
                status.State,
                $"Fault {_activeFaultCode} cleared."
            ));
            _activeFaultId = null;
            _activeFaultCode = null;
        }

        if (!_activeCycleId.HasValue)
        {
            return;
        }

        var finalStatus = status.Cycle.State switch
        {
            "CycleComplete" => "Completed",
            "CycleFaulted" => "Faulted",
            "CycleAborted" when status.ActiveFault is not null => "Faulted",
            "CycleAborted" => "Aborted",
            _ => null
        };

        if (finalStatus is null)
        {
            return;
        }

        Enqueue(new CycleFinishedWrite(
            _activeCycleId.Value,
            now,
            finalStatus,
            finalStatus == "Faulted",
            status.ActiveFault?.Code,
            status.ActiveFault?.Message,
            finalStatus == "Completed" ? status.Inspection?.Accepted : null,
            status.Inspection?.Reason,
            status.Inspection?.SampleId,
            status.Inspection?.FeatureCoverage
        ));
        Enqueue(new MachineEventWrite(
            now,
            finalStatus == "Completed"
                ? "CycleCompleted"
                : $"Cycle{finalStatus}",
            status.State,
            $"Production cycle {finalStatus.ToLowerInvariant()}."
        ));
        _activeCycleId = null;
    }

    private void Enqueue(HistoryWrite write)
    {
        if (!queue.TryEnqueue(write))
        {
            logger.LogError(
                "Operational history queue is full; dropped {RecordType} without affecting controller operation.",
                write.GetType().Name
            );
        }
    }
}
