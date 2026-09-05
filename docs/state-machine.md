# Machine and production state machines

## Machine state

| State | Meaning |
|---|---|
| `Offline` | Safe startup state; hardware is not initialized. |
| `Initializing` | Hardware initialization is in progress. |
| `Idle` | Initialized and stopped, ready to start. |
| `Running` | Production is enabled and cycles may execute. |
| `Paused` | Production progression is suspended without discarding the cycle. |
| `Stopping` | Active work is being aborted/stopped before returning to `Idle`. |
| `Faulted` | A device, inspection, or sequence fault prevents production. |
| `EmergencyStop` | Emergency stop is active; active motion and sequencing are interrupted. |

High-level transitions:

```text
Offline → Initializing → Idle → Running ⇄ Paused
                              │    │
                              └────┴→ Stopping → Idle

Initializing / Idle / Running / Paused / Stopping → Faulted → Idle (reset)
Any non-E-stop state → EmergencyStop → Idle (clear condition, then reset)
```

`start` is valid from `Idle`; `pause` from `Running`; `resume` from `Paused`; and `stop` from `Running` or `Paused`. Invalid lifecycle requests are rejected without changing state. Reset returns `Faulted` to `Idle` only when the fault condition permits recovery. E-stop must be cleared before reset.

## Production cycle state

```text
WaitingForPart
→ StoppingConveyor
→ MovingToPick
→ ClosingGripper
→ MovingToInspection
→ Inspecting
→ MovingToAcceptBin | MovingToRejectBin
→ ReleasingPart
→ ReturningHome
→ RestartingConveyor
→ CycleComplete
```

Only a `Running` machine starts a cycle. The controller-produced inspection result selects the accept or reject branch. A completed cycle can be followed by another cycle while the machine remains running.

`pause` freezes progression and timeout accounting; `resume` continues the same cycle. `stop` produces `CycleAborted`. A detected device, safety, timeout, or inspection error produces `CycleFaulted` and moves the machine to `Faulted`. E-stop aborts active sequencing immediately and moves the machine to `EmergencyStop`.

Recovery never resumes an interrupted cycle. After clearing the underlying condition and resetting, the machine returns to `Idle`; the operator must issue `start` and begin a new cycle.
