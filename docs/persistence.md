# PostgreSQL operational history

PostgreSQL stores machine events, completed production-cycle records, and fault history. The C++ controller remains the source of live machine state. Writes are queued and performed by an ASP.NET background service so database failures do not change controller command results.

## Local setup

1. Copy `.env.example` to `.env` and replace the example password.
2. Start PostgreSQL:

   ```powershell
   docker compose up -d postgres
   ```

3. Configure the service process and apply migrations:

   ```powershell
   $env:ConnectionStrings__WorkcellDatabase = "Host=localhost;Port=5432;Database=workcell;Username=workcell;Password=<your-password>"
   dotnet tool restore
   dotnet tool run dotnet-ef database update --project service/MachineService/MachineService.csproj
   ```

The `.env` file is ignored by Git. Do not commit real credentials.

## Schema and write path

- `machine_events`: timestamped lifecycle/cycle event type, machine state, and message.
- `production_cycles`: start/completion, result, duration, final/fault status, and inspection fields.
- `fault_events`: fault code/message, machine/cycle context, raised timestamp, and optional cleared timestamp.

EF Core migrations live under `service/MachineService/Persistence/Migrations`. Do not replace migrations with `EnsureCreated` outside tests.

ASP.NET observes authoritative controller transitions and submits records to a bounded 1,000-item in-memory queue. A background writer performs EF Core operations. Status polling is not itself persisted, so repeated GET requests do not duplicate lifecycle or fault records.

## History API

- `GET /api/events?page=1&pageSize=50`
- `GET /api/cycles?page=1&pageSize=50`
- `GET /api/cycles/{id}`
- `GET /api/faults?page=1&pageSize=50`
- `GET /api/metrics`

Page size is constrained to 1–100 records. Results are returned newest first.

## Degraded mode

A database or writer failure is logged and exposed by `/health` with queue depth, failed/dropped counts, and the last error. Controller commands remain independent and continue to use C++ state. History and metrics requests return `503` when PostgreSQL is unavailable. Failed or dropped records are not durably replayed, and process-local counters reset with ASP.NET.
