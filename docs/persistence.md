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

## History API

- `GET /api/events?page=1&pageSize=50`
- `GET /api/cycles?page=1&pageSize=50`
- `GET /api/cycles/{id}`
- `GET /api/faults?page=1&pageSize=50`
- `GET /api/metrics`

Page size is constrained to 1–100 records. Results are returned newest first.
