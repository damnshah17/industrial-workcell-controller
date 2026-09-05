# WPF operator console

The operator console is a presentation-only REST client. It does not start the C++ bridge, connect to PostgreSQL, or maintain an independent machine state.

## Features

- Live machine, cycle, robot, conveyor, gripper, sensor, safety, and fault telemetry
- Initialize, start, pause, resume, stop, reset, Emergency Stop, and clear-E-stop controls
- Selection of deterministic inspection samples and controller-produced accept/reject results
- Production totals and persisted metrics
- Production-cycle, fault, and machine-event history
- Distinct backend-disconnected, controller-unavailable, history-degraded, and healthy states
- Rejected-command feedback without local lifecycle logic

Live telemetry is refreshed every 500 ms. Persisted history and metrics are refreshed every five seconds and immediately after operator commands.

## Run

ASP.NET starts and supervises the C++ bridge; do not launch it separately. Start PostgreSQL and ASP.NET using the root README, then on Windows run:

```powershell
dotnet run --project operator-console/WorkcellOperatorConsole/WorkcellOperatorConsole.csproj
```

The default API URL is `http://localhost:5295/`. Override it without changing source code:

```powershell
$env:WORKCELL_API_URL = "http://localhost:5295/"
dotnet run --project operator-console/WorkcellOperatorConsole/WorkcellOperatorConsole.csproj
```

## Test

```powershell
dotnet test operator-console/WorkcellOperatorConsole.Tests/WorkcellOperatorConsole.Tests.csproj
```

The cross-platform Core test project covers REST serialization, commands, health, history, fault display, and view-model behavior. It does not automate native desktop rendering.
