# Industrial Robotic Workcell Controller

A local, full-stack simulation of an industrial pick/inspect/sort workcell. The project demonstrates deterministic real-time control in C++, a hardware abstraction layer, safety and fault handling, REST orchestration, operational history, and an operator-facing desktop client without making the UI or database a second source of machine truth.

The production sequence is:

```text
Detect part → Stop conveyor → Pick → Inspect → Route accepted/rejected
            → Release → Return home → Restart conveyor
```

## Key features

- C++20 machine and production-cycle state machines
- Hardware interfaces with deterministic simulated robot, conveyor, gripper, and sensor
- Emergency Stop plus naturally detected device and sequence faults
- Deterministic PGM machine-vision inspection and accept/reject routing
- ASP.NET Core 10 REST API and supervised C++ child process
- Structured newline-delimited JSON over loopback TCP
- PostgreSQL operational history and production metrics through an asynchronous bounded queue
- Windows WPF operator console that is strictly a REST client
- Structured health, degraded database operation, bounded IPC recovery, and safe HTTP errors
- C++, hosted HTTP, PostgreSQL, IPC, vision, fault, and WPF Core tests in GitHub Actions

## Architecture

```text
WPF Operator Console
        │ REST
        ▼
ASP.NET Core Machine Service ─────► PostgreSQL operational history
        │ supervised loopback TCP       (never controls the machine)
        ▼
C++ machine_bridge / MachineController
        │
        ├── SequenceController + inspection
        ├── SafetyController + FaultManager
        └── HAL interfaces → simulated hardware
```

The C++ controller owns live machine, safety, fault, and cycle state. ASP.NET is the only backend boundary and owns the bridge process. WPF never accesses the bridge or PostgreSQL directly. See [Architecture](docs/architecture.md).

## Technology stack

| Layer | Technology |
|---|---|
| Controller | C++20, CMake 3.20+, GoogleTest |
| IPC | Loopback TCP, UTF-8 NDJSON |
| Backend | ASP.NET Core / .NET 10, EF Core 10, Npgsql |
| Persistence | PostgreSQL 18 via Docker Compose |
| Operator UI | WPF on .NET 10 for Windows |
| Tests/CI | CTest, Python 3 bridge tests, xUnit, GitHub Actions |

## Repository structure

```text
controller/        C++ controller, HAL, simulation, bridge, samples, and tests
service/           ASP.NET API, persistence, migrations, and integration tests
operator-console/  WPF application and cross-platform Core tests
docs/              Focused architecture and subsystem documentation
scripts/           Small PowerShell development helpers
compose.yml        Local PostgreSQL
```

## Prerequisites

- Git
- .NET 10 SDK
- CMake 3.20+ and a C++20 compiler
- Ninja (used by the documented cross-platform build commands)
- Python 3 for CTest bridge integration tests
- Docker Desktop with Docker Compose
- Windows 11 to run WPF (controller and backend also build on Linux)

## Quick start

Copy `.env.example` to the ignored `.env`, choose a local password, then run from PowerShell at the repository root:

```powershell
Copy-Item .env.example .env
./scripts/run-dev.ps1
```

The helper validates tools, starts PostgreSQL, builds `machine_bridge`, applies migrations, and runs ASP.NET at `http://localhost:5295`. In a second Windows terminal:

```powershell
dotnet run --project operator-console/WorkcellOperatorConsole/WorkcellOperatorConsole.csproj
```

Stop ASP.NET with Ctrl+C, close WPF, and stop PostgreSQL with:

```powershell
./scripts/stop-dev.ps1
```

Manual setup commands and configuration are documented in [Persistence](docs/persistence.md) and [Operator console](docs/operator-console.md).

## Quick demo

1. Start the backend and WPF console using Quick Start.
2. Initialize, then start the machine.
3. Run `good-part`; show PASS, accept routing, and the accepted history row.
4. Run `missing-hole`; show FAIL, reject routing, and the rejected history row.
5. Enable `robot-communication`, start a cycle, and show the controller fault and history.
6. Clear the simulated condition, reset, and start again.
7. Trigger E-stop during a cycle; clear the E-stop condition and reset.
8. Show cycle totals, acceptance rate, fault count, and `/health`.

See [API](docs/api.md) for a terminal-driven version.

## API overview

- Machine control and telemetry: `/api/machine/*`
- Simulation-only faults: `/api/simulation/faults/*`
- History and metrics: `/api/events`, `/api/cycles`, `/api/faults`, `/api/metrics`
- Component health: `/health`

Command rejection is `409`, malformed input is `400`, unavailable dependencies are `503`, and unexpected failures are `500`. Full requests and response shapes are in [API](docs/api.md).

## Testing and CI

```powershell
cmake -S controller -B controller/build -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build controller/build
ctest --test-dir controller/build --output-on-failure

dotnet test service/MachineService.Tests/MachineService.Tests.csproj
dotnet test operator-console/WorkcellOperatorConsole.Tests/WorkcellOperatorConsole.Tests.csproj
```

PostgreSQL-backed service tests use `WORKCELL_TEST_CONNECTION_STRING`; CI supplies it and builds the bridge first. GitHub Actions has separate C++ controller, ASP.NET/PostgreSQL, and Windows WPF jobs.

## Design decisions

- Safety-critical live state stays in one deterministic C++ owner.
- PostgreSQL stores history only; its loss degrades analytics, not control.
- WPF is presentation and operator interaction only.
- Loopback TCP gives one testable Windows/Linux protocol without exposing the controller remotely.
- Classical deterministic inspection is transparent and reproducible for this simulation.

## Known limitations and future extensions

This is simulation software, not safety-certified industrial control software. It has no physical-device adapters, authentication, distributed deployment, durable persistence retry, or full WPF GUI automation. Natural extensions include real HAL/camera adapters, authenticated deployment boundaries, durable history buffering, and hardware-in-the-loop testing.

## Documentation

[Architecture](docs/architecture.md) · [State machines](docs/state-machine.md) · [API](docs/api.md) · [Fault handling](docs/fault-handling.md) · [Machine vision](docs/machine-vision.md) · [IPC](docs/ipc.md) · [Persistence](docs/persistence.md) · [Reliability](docs/reliability.md) · [Operator console](docs/operator-console.md)
