# WPF operator console

The operator console is a presentation-only REST client. It does not start the C++ bridge, connect to PostgreSQL, or maintain an independent machine state.

## Features

- Live machine, cycle, robot, conveyor, gripper, sensor, safety, and fault telemetry
- Initialize, start, pause, resume, stop, reset, Emergency Stop, and clear-E-stop controls
- Accepted-part and rejected-part production commands
- Production totals and persisted metrics
- Production-cycle, fault, and machine-event history
- Connection and rejected-command feedback

Live telemetry is refreshed every 500 ms. Persisted history and metrics are refreshed every five seconds and immediately after operator commands.

## Run

Start the C++ bridge, PostgreSQL, and ASP.NET service using the existing backend instructions. Then, on Windows:

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
