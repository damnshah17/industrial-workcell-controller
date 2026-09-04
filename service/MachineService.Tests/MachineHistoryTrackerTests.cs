using MachineService.Models;
using MachineService.Persistence;
using MachineService.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace MachineService.Tests;

public sealed class MachineHistoryTrackerTests
{
    [Fact]
    public void RepeatedStatusPollingDoesNotDuplicateFaultEvents()
    {
        var queue = new CollectingHistoryWriteQueue();
        var tracker = CreateTracker(queue);
        var status = CreateStatus(
            MachineState.Faulted,
            "CycleFaulted",
            new FaultInfo("MOTION_TIMEOUT", "Timed out")
        );

        tracker.Observe(status);
        tracker.Observe(status);
        tracker.Observe(status);

        Assert.Single(queue.Writes.OfType<FaultRaisedWrite>());
        Assert.Single(
            queue.Writes.OfType<MachineEventWrite>(),
            x => x.EventType == "FaultRaised"
        );
    }

    [Fact]
    public void CompletedCycleIsWrittenOnceWithRequestedResult()
    {
        var queue = new CollectingHistoryWriteQueue();
        var tracker = CreateTracker(queue);

        tracker.RecordCycleStarted(
            accepted: false,
            CreateStatus(MachineState.Running, "StoppingConveyor")
        );
        var completed = CreateStatus(
            MachineState.Running,
            "CycleComplete"
        );
        tracker.Observe(completed);
        tracker.Observe(completed);

        var started = Assert.Single(queue.Writes.OfType<CycleStartedWrite>());
        Assert.False(started.Accepted);
        var finished = Assert.Single(queue.Writes.OfType<CycleFinishedWrite>());
        Assert.Equal(started.Id, finished.Id);
        Assert.Equal("Completed", finished.FinalStatus);
    }

    [Fact]
    public void FaultClearUpdatesTheOriginalFault()
    {
        var queue = new CollectingHistoryWriteQueue();
        var tracker = CreateTracker(queue);

        tracker.Observe(CreateStatus(
            MachineState.Faulted,
            "CycleFaulted",
            new FaultInfo("GRIPPER_FAILURE", "Close failed")
        ));
        tracker.Observe(CreateStatus(MachineState.Idle, "WaitingForPart"));

        var raised = Assert.Single(queue.Writes.OfType<FaultRaisedWrite>());
        var cleared = Assert.Single(queue.Writes.OfType<FaultClearedWrite>());
        Assert.Equal(raised.Id, cleared.Id);
    }

    [Fact]
    public void StopFinalizesAnActiveCycleAsAborted()
    {
        var queue = new CollectingHistoryWriteQueue();
        var tracker = CreateTracker(queue);
        tracker.RecordCycleStarted(
            true,
            CreateStatus(MachineState.Running, "MovingToPick")
        );

        tracker.RecordCommand(
            "Stopped",
            "Machine stopped.",
            CreateStatus(MachineState.Idle, "WaitingForPart")
        );

        var finished = Assert.Single(
            queue.Writes.OfType<CycleFinishedWrite>()
        );
        Assert.Equal("Aborted", finished.FinalStatus);
    }

    private static MachineHistoryTracker CreateTracker(
        IHistoryWriteQueue queue
    ) => new(
        queue,
        TimeProvider.System,
        NullLogger<MachineHistoryTracker>.Instance
    );

    private static MachineStatus CreateStatus(
        MachineState state,
        string cycleState,
        FaultInfo? fault = null
    ) => new(
        state,
        state == MachineState.EmergencyStop,
        fault,
        new CycleStatus(cycleState, 0, 0, 0),
        new RobotStatus("Home", false, true),
        new ConveyorStatus(false),
        new GripperStatus(true),
        new PartSensorStatus(false)
    );
}
