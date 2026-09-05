# Fault and safety handling

## Detection and propagation

Simulation routes configure failure behavior in C++ simulated devices. They do not set `MachineState::Faulted`. During normal sequence work, device health, failed operations, safety state, or timeouts are detected by `SequenceController` and `MachineController` through the same path a real HAL adapter would use.

```text
REST simulation request
→ bridge configures simulated device behavior
→ normal controller operation detects failure
→ CycleFaulted / Machine Faulted
→ fault telemetry returned through IPC and REST
→ WPF displays the active fault
→ ASP.NET queues one fault-history record
```

Repeated status polling observes the same fault but does not create duplicate fault records. When recovery is observed, the existing record receives `cleared_at`.

## Operator recovery

The required order is:

```text
Clear physical/simulated condition → Reset controller → return to Idle
                                      → Start explicitly when safe
```

For example:

```http
POST /api/simulation/faults/gripper-close/clear
POST /api/machine/reset
POST /api/machine/start
```

Reset does not clear an active external condition and never resumes the interrupted cycle.

## Emergency Stop

E-stop is a higher-priority safety state, not an ordinary production fault. It interrupts an active sequence and stops active robot/conveyor behavior. The E-stop condition must first be cleared with `/api/machine/clear-estop`; the operator must then call `/api/machine/reset`. A restart or reconnect never automatically clears E-stop intent or resumes production.

## Supported simulated conditions

See [Advanced failure simulation](fault-simulation.md) for all endpoint names. These APIs exist only for the simulation environment and remain separate from normal `/api/machine` controls.
