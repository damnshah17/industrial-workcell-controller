# Local controller IPC

Phase 9 moves ASP.NET-to-controller traffic from stdin/stdout to a loopback TCP connection. ASP.NET still owns the child-process lifetime, and `IMachineTransport` remains the boundary used by machine and simulation services.

## Why loopback TCP

Loopback TCP provides the same implementation and test path on Windows and Linux. It separates protocol bytes from controller logs without the platform-specific C++ work required for Windows named pipes. The protocol remains local-only because the bridge binds explicitly to `127.0.0.1`.

## Framing and protocol

Each message is one UTF-8 JSON object followed by a newline. Messages are limited to 64 KiB.

Request example:

```json
{"requestId":"9be4...","command":"start-cycle","payload":{"sampleId":"good-part"}}
```

Response example:

```json
{"requestId":"9be4...","success":true,"status":{"state":"Running"},"error":null}
```

Dynamic operations use structured payloads:

- `start-cycle` with `sampleId`
- `configure-simulation-fault` with `fault` and `enabled`

Lifecycle commands retain their established names. Every response repeats the request ID. Failures use structured codes including `MALFORMED_REQUEST`, `MESSAGE_TOO_LARGE`, `UNKNOWN_COMMAND`, and `COMMAND_REJECTED`.

## Ownership and lifecycle

The socket reader does not access controller objects. It translates a request and submits it to the existing controller-owner queue, then waits for that owner thread to produce the status snapshot.

ASP.NET selects an unused loopback port, launches `machine_bridge --tcp-port <port>`, and connects within the configured startup timeout. Commands remain serialized at `IMachineTransport`. A timeout terminates the child because a late reply could otherwise invalidate correlation. Disposal sends `shutdown`, waits briefly, and kills the process tree only if graceful exit fails.

Configuration:

```json
"Controller": {
  "ExecutablePath": "../../controller/build/machine_bridge.exe",
  "StartupTimeoutMilliseconds": 5000,
  "CommandTimeoutMilliseconds": 3000
}
```

Automatic restart is intentionally deferred to Phase 10.
