# REST API

The development base URL is `http://localhost:5295`. JSON enum values are strings. Machine responses are authoritative snapshots produced by the C++ controller.

## Machine

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/machine/status` | Current machine, cycle, device, fault, and inspection telemetry |
| POST | `/api/machine/initialize` | Initialize from `Offline` |
| POST | `/api/machine/start` | Enter `Running` from `Idle` |
| POST | `/api/machine/pause` | Pause a running machine |
| POST | `/api/machine/resume` | Resume from `Paused` |
| POST | `/api/machine/stop` | Stop/abort and return to `Idle` |
| POST | `/api/machine/reset` | Recover from a cleared fault or E-stop condition |
| POST | `/api/machine/estop` | Activate Emergency Stop |
| POST | `/api/machine/clear-estop` | Clear the simulated E-stop condition; reset is still required |
| POST | `/api/machine/cycle` | Start a production cycle with a known inspection sample |

```http
POST /api/machine/cycle
Content-Type: application/json

{ "sampleId": "good-part" }
```

Successful commands return `{ "success": true, "status": { ... } }`. A status snapshot includes `state`, `emergencyStopActive`, `activeFault`, `cycle`, `robot`, `conveyor`, `gripper`, `partSensor`, and `inspection`.

## Simulation faults

Enable or clear device behavior under `/api/simulation/faults`:

```http
POST /api/simulation/faults/robot-communication
POST /api/simulation/faults/robot-communication/clear
POST /api/simulation/faults/clear
```

Supported names are `robot-communication`, `motion-timeout`, `conveyor-start`, `conveyor-stop`, `gripper-open`, `gripper-close`, `sensor`, and `safety-door`. These routes configure simulated devices; they do not force controller state.

`POST /api/machine/fault/motion-timeout` remains as a compatibility-only direct injection route. New simulation demonstrations should use `/api/simulation/faults/motion-timeout`, which exercises the natural stall/timeout path.

## History and metrics

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/events?page=1&pageSize=50` | Machine lifecycle and cycle events |
| GET | `/api/cycles?page=1&pageSize=50` | Production-cycle history |
| GET | `/api/cycles/{id}` | One cycle or `404` |
| GET | `/api/faults?page=1&pageSize=50` | Raised/cleared fault history |
| GET | `/api/metrics` | Totals, accepted/rejected counts, acceptance rate, average duration, fault count |

Pages are newest-first; `page` is normalized to at least 1 and `pageSize` is clamped to 1–100.

## Health

`GET /health` returns overall and component status:

```json
{
  "status": "Degraded",
  "timestamp": "2026-09-05T12:00:00Z",
  "service": { "status": "Healthy", "message": "ASP.NET service is available." },
  "controller": { "status": "Healthy", "message": "Controller process and IPC connection are available." },
  "database": { "status": "Degraded", "message": "PostgreSQL is unavailable; machine control remains available." },
  "persistence": { "status": "Healthy", "message": "Persistence queue and writer are operational." }
}
```

## HTTP semantics

- `200` — successful query or accepted command
- `400` — malformed JSON, invalid model binding, or unknown simulation fault
- `404` — requested cycle does not exist
- `409` — command is invalid for the current controller state
- `503` — controller or database dependency required by that request is unavailable
- `500` — unexpected server failure

Error responses contain a trace ID but no stack trace or sensitive configuration.

## Terminal demo

```powershell
$base = "http://localhost:5295"
Invoke-RestMethod -Method Post "$base/api/machine/initialize"
Invoke-RestMethod -Method Post "$base/api/machine/start"
Invoke-RestMethod -Method Post -ContentType application/json -Body '{"sampleId":"good-part"}' "$base/api/machine/cycle"
Invoke-RestMethod "$base/api/machine/status"
Invoke-RestMethod "$base/api/cycles?pageSize=10"
Invoke-RestMethod "$base/api/metrics"
```
