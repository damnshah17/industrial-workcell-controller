# Final demonstration guide

This 5–10 minute walkthrough demonstrates the complete workcell through its supported operator and service boundaries. Use the WPF console for operator actions and keep a terminal available for API and history checks.

## Preparation

From the repository root, create the local environment file once:

```powershell
Copy-Item .env.example .env
# Replace the example POSTGRES_PASSWORD value in .env.
```

Start PostgreSQL, build the bridge, apply migrations, and run ASP.NET:

```powershell
./scripts/run-dev.ps1
```

In a second Windows terminal, launch the operator console:

```powershell
dotnet run --project operator-console/WorkcellOperatorConsole/WorkcellOperatorConsole.csproj
```

The default backend URL is `http://localhost:5295`. Set `WORKCELL_API_URL` before launching WPF only when using a different URL.

## Walkthrough

| Step | Operator action | Expected state or visible result | Engineering concept |
|---|---|---|---|
| 1 | Open the console and inspect health. | Service, controller, database, and persistence are healthy. The machine is `Offline`. | Supervised process startup and component-level health. |
| 2 | Select **Initialize**. | The machine transitions through `Initializing` to `Idle`; simulated devices are initialized. | Explicit lifecycle state machine and HAL initialization. |
| 3 | Select **Start**. | The machine enters `Running` and is ready for a production cycle. | Operator intent is sent through REST and correlated TCP IPC. |
| 4 | Select `good-part`, then start a cycle. | Inspection reports PASS with reason `PASS`; cycle telemetry passes through `MovingToAcceptBin`, the accepted count increments, and the cycle completes. | Controller-owned deterministic inspection and accepted routing. |
| 5 | Open cycle history and metrics. | An accepted cycle appears and totals/acceptance rate update after the asynchronous history write completes. | PostgreSQL operational history without database ownership of control state. |
| 6 | Select `missing-hole`, then start another cycle. | Inspection reports FAIL with `MISSING_FEATURE`; cycle telemetry passes through `MovingToRejectBin`, the rejected count increments, and a rejected cycle is persisted. | Reproducible reject classification and controller-owned routing. |
| 7 | In the terminal, call `POST /api/simulation/faults/robot-communication`, then start a cycle in WPF. | Normal controller work detects the unhealthy robot, the cycle faults, the machine becomes `Faulted`, and diagnostics appear in WPF and fault history. | Failure injection changes device behavior; it does not bypass controller fault handling. |
| 8 | Call `POST /api/simulation/faults/robot-communication/clear`, select **Reset**, then **Start**. | The physical/simulated condition clears first; reset returns the controller to `Idle`; start returns it to `Running`. The interrupted cycle is not resumed. | Explicit safe recovery and fault-history closure. |
| 9 | Start a cycle and select **E-stop** while it is active. | The machine enters `EmergencyStop`; active robot/conveyor work is stopped. | Higher-priority safety interruption. |
| 10 | Select **Clear E-stop**, then **Reset**. | Clearing removes the condition but does not restart work. Reset returns to `Idle`; production requires another explicit start. | E-stop is distinct from ordinary faults and cannot auto-resume. |
| 11 | Review health, cycle history, fault history, and metrics. | Health is restored; accepted/rejected cycles and the fault record remain available. | End-to-end telemetry, persistence, recovery, and auditability. |

Allow a brief moment before refreshing history because persistence is deliberately asynchronous. If a control button is disabled, confirm the current machine state and complete the required recovery transition rather than bypassing it.

## Optional terminal evidence

These read-only calls are useful while presenting the system:

```powershell
$base = "http://localhost:5295"
Invoke-RestMethod "$base/health" | ConvertTo-Json -Depth 5
Invoke-RestMethod "$base/api/machine/status" | ConvertTo-Json -Depth 6
Invoke-RestMethod "$base/api/cycles?page=1&pageSize=10" | ConvertTo-Json -Depth 5
Invoke-RestMethod "$base/api/faults?page=1&pageSize=10" | ConvertTo-Json -Depth 5
Invoke-RestMethod "$base/api/metrics" | ConvertTo-Json
```

The full route reference, including simulation endpoints, is in [REST API](api.md).

## Shutdown and repeatability

1. Close the WPF window normally.
2. Press Ctrl+C in the ASP.NET terminal. ASP.NET disposes its supervised `machine_bridge` child.
3. Stop PostgreSQL:

   ```powershell
   ./scripts/stop-dev.ps1
   ```

4. Confirm no unexpected bridge remains if startup was interrupted:

   ```powershell
   Get-Process machine_bridge -ErrorAction SilentlyContinue
   ```

`stop-dev.ps1` stops the PostgreSQL container but preserves the named volume and demo history. Use Docker Compose volume removal only when a deliberately empty database is required.

## Real screenshot checklist

No generated or mock screenshots are included. Useful screenshots to capture from a real run are:

- WPF dashboard with a healthy controller in `Running`.
- `good-part` PASS result and accepted routing.
- `missing-hole` failure with `MISSING_FEATURE` and rejected routing.
- Controller `Faulted` state with device diagnostics.
- Active `EmergencyStop` state.
- Production history and metrics after both accepted and rejected cycles.
- A degraded health view with PostgreSQL unavailable, if that behavior is being demonstrated.
