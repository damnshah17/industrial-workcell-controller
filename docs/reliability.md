# Reliability and observability

The machine service exposes `GET /health` with separate `service`, `controller`,
`database`, and `persistence` components. Each component reports `Healthy`,
`Degraded`, or `Unhealthy`. PostgreSQL or history-writer failures degrade the
service but do not make controller commands unavailable.

The controller transport serializes commands and logs the command, IPC request
identifier, controller process identifier, duration, and outcome. A timeout,
malformed or mismatched response, closed socket, or unexpected bridge exit makes
the current connection unusable. The in-flight command is never replayed. On the
next command, the transport makes at most the configured number of restart
attempts with a bounded backoff. A restarted controller remains in its safe
`Offline` state and requires normal operator lifecycle commands.

Configuration is under `Controller`:

- `CommandTimeoutMilliseconds` bounds each command, including recovery.
- `StartupTimeoutMilliseconds` bounds a bridge connection attempt.
- `MaxRestartAttempts` is clamped from 1 through 10.
- `RestartBackoffMilliseconds` controls delay between attempts.
- `RecoveryCooldownMilliseconds` prevents repeated callers from creating an
  aggressive restart loop after a complete failed recovery batch.

The persistence queue is bounded at 1,000 records. Health details expose its
current depth, failed and dropped write counts, last successful write, and last
error. These counters are process-local diagnostics; failed or dropped records
are not replayed after a service restart.

Expected HTTP behavior is `409` for controller command rejection, `503` for an
unavailable controller or database dependency, and `500` for an unexpected
service error. Error bodies contain a trace identifier but no exception details.
The WPF console consumes `/health` over REST and distinguishes a backend outage,
a controller outage, and degraded history. It does not restart components.

The overall service is `Unhealthy` when controller transport is unavailable and
`Degraded` when database or persistence history is impaired while control remains
available. Health is passive for controller state; it does not issue lifecycle
commands. WPF displays `DISCONNECTED`, `BACKEND OK • CONTROLLER UNAVAILABLE`,
`CONTROLLER OK • HISTORY DEGRADED`, or `CONNECTED` as appropriate.
