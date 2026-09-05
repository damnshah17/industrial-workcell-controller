# Technical summary

## Problem

This project simulates an automated robotic workcell that repeatedly performs the following sequence:

```text
Detect part → Stop conveyor → Pick → Inspect → Route accepted/rejected
            → Release → Return home → Continue
```

The implementation demonstrates how deterministic machine control, safety, device failures, operator interaction, telemetry, and historical records can be separated without creating competing sources of truth. It is engineering simulation software, not safety-certified industrial equipment.

## System architecture

```text
WPF Operator Console
        │ REST
        ▼
ASP.NET Core Machine Service ─────► PostgreSQL history
        │ supervised loopback TCP
        ▼
C++ machine_bridge / MachineController
        │
        ├── Sequence / Safety / Fault management
        └── HAL → simulated devices + machine vision
```

ASP.NET starts and supervises one C++ bridge process. Commands and authoritative snapshots travel as correlated newline-delimited JSON messages over loopback TCP. The backend exposes those capabilities through REST, observes meaningful controller transitions, and queues historical writes. WPF communicates only with ASP.NET.

## Component responsibilities

- `MachineController` owns live lifecycle state and coordinates commands on one controller thread.
- `SequenceController` owns production-cycle progression and accepted/rejected routing.
- `SafetyController` owns safety-condition response, including Emergency Stop behavior.
- `FaultManager` owns the controller's active fault record.
- HAL interfaces isolate controller logic from robot, conveyor, gripper, and sensor implementations.
- Simulation implementations provide deterministic device behavior and configurable failure conditions.
- `IInspectionSystem` keeps inspection behind a controller-side abstraction; the current implementation evaluates deterministic PGM samples.
- ASP.NET owns REST mapping, bridge process supervision, transition observation, health aggregation, and persistence orchestration.
- PostgreSQL stores machine events, production cycles, fault events, and inspection outcomes for history and metrics. It never controls the machine.
- WPF displays REST telemetry and sends operator intent. It does not reproduce controller state or contact the bridge/database directly.

## Important engineering decisions

- An explicit machine state machine makes legal lifecycle and recovery paths observable and testable.
- Dependency-injected HAL interfaces let production logic exercise realistic success and failure behavior without vendor hardware.
- A single controller-owner thread prevents concurrent mutation of live control state.
- Inspection decisions and routing remain in C++, avoiding a second production-decision owner in the UI or service.
- Structured TCP IPC provides request correlation, framing, timeouts, and portable Windows/Linux tests.
- A bounded asynchronous persistence queue prevents database latency or outages from blocking controller commands.
- Database degradation is surfaced through health while machine control remains available.
- Controller recovery is bounded, does not replay uncertain commands, and starts a replacement controller in `Offline`.
- Classical deterministic vision keeps the simulation transparent and reproducible without an ML runtime or GPU.
- The REST-only WPF boundary keeps presentation replaceable and prevents direct machine/database coupling.

## Project snapshot

- Eight lifecycle states: `Offline`, `Initializing`, `Idle`, `Running`, `Paused`, `Faulted`, `EmergencyStop`, and `Stopping`.
- Eight configurable simulation conditions: robot communication, motion timeout, conveyor start/stop, gripper open/close, sensor, and safety door.
- Deterministic `good-part`, `missing-hole`, and malformed-part inspection samples.
- Four public API groups: machine control/telemetry, simulation faults, history/metrics, and health.
- Three persisted record types: machine events, production cycles, and fault events.
- Controller restart uses bounded retries and never restores or resumes volatile production state.

## Testing

The test strategy follows the component boundaries:

- C++ unit tests cover lifecycle, sequence, safety, fault, simulated hardware, commands, and inspection.
- Python-driven bridge tests cover normal control, faults, vision, framing, request correlation, and IPC recovery.
- ASP.NET xUnit tests cover REST mapping, service orchestration, persistence tracking, health, transport recovery, and hosted HTTP workflows.
- PostgreSQL integration tests apply the real EF Core model and validate historical records.
- Cross-platform WPF Core tests cover API-client behavior and view-model presentation logic.
- GitHub Actions builds and tests C++ and ASP.NET on Linux and WPF on Windows.

## Current limitations

- All devices and images are simulated; there are no vendor SDK, PLC, fieldbus, or camera adapters.
- Timing models are deterministic demonstrations rather than calibrated physical motion profiles.
- The software is not safety-certified and must not be used as an industrial safety system.
- IPC is local, single-service infrastructure with no remote authentication or encryption boundary.
- History buffering is bounded and in-memory, so writes can be lost during an extended outage or abrupt process termination.
- Controller restart intentionally loses volatile machine/cycle state and requires explicit reinitialization.
- WPF uses polling rather than server-pushed telemetry and has no full GUI automation suite.
- PostgreSQL and the development environment are local; deployment, authorization, and production observability are outside the current scope.

