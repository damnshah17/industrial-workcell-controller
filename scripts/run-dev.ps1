[CmdletBinding()]
param(
    [switch]$SkipControllerBuild,
    [switch]$SkipDatabaseStart
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repositoryRoot

foreach ($command in @("dotnet", "cmake", "ninja", "docker")) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Required command '$command' was not found. See README.md prerequisites."
    }
}

$environmentFile = Join-Path $repositoryRoot ".env"
if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw "Create .env from .env.example and choose a local POSTGRES_PASSWORD first."
}

$allowedKeys = @("POSTGRES_DB", "POSTGRES_USER", "POSTGRES_PASSWORD", "WORKCELL_DB_PORT")
foreach ($line in Get-Content -LiteralPath $environmentFile) {
    if ($line -match '^\s*#' -or $line -notmatch '=') { continue }
    $key, $value = $line -split '=', 2
    $key = $key.Trim()
    if ($allowedKeys -contains $key) {
        [Environment]::SetEnvironmentVariable($key, $value.Trim(), "Process")
    }
}

$database = if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { "workcell" }
$user = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { "workcell" }
$port = if ($env:WORKCELL_DB_PORT) { $env:WORKCELL_DB_PORT } else { "5432" }
if (-not $env:POSTGRES_PASSWORD -or $env:POSTGRES_PASSWORD -eq "replace-with-a-local-password") {
    throw "Set a non-example POSTGRES_PASSWORD in .env."
}

if (-not $SkipDatabaseStart) {
    docker compose up -d --wait postgres
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL failed to start." }
}

if (-not $SkipControllerBuild) {
    cmake -S controller -B controller/build -G Ninja -DCMAKE_BUILD_TYPE=Debug
    if ($LASTEXITCODE -ne 0) { throw "CMake configuration failed." }
    cmake --build controller/build --target machine_bridge
    if ($LASTEXITCODE -ne 0) { throw "Controller bridge build failed." }
}

$bridgeName = if ($env:OS -eq "Windows_NT") { "machine_bridge.exe" } else { "machine_bridge" }
$bridgePath = Join-Path $repositoryRoot "controller/build/$bridgeName"
if (-not (Test-Path -LiteralPath $bridgePath)) {
    throw "Controller bridge was not found at '$bridgePath'. Build it before starting the service."
}

$env:Controller__ExecutablePath = $bridgePath
$env:ConnectionStrings__WorkcellDatabase = "Host=localhost;Port=$port;Database=$database;Username=$user;Password=$($env:POSTGRES_PASSWORD)"

dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "Local .NET tools could not be restored." }
dotnet tool run dotnet-ef database update --project service/MachineService/MachineService.csproj
if ($LASTEXITCODE -ne 0) { throw "Database migrations failed." }

Write-Host "Backend: http://localhost:5295"
Write-Host "Health:  http://localhost:5295/health"
Write-Host "WPF (second terminal): dotnet run --project operator-console/WorkcellOperatorConsole/WorkcellOperatorConsole.csproj"
Write-Host "Press Ctrl+C to stop ASP.NET and its supervised controller bridge."

dotnet run --project service/MachineService/MachineService.csproj --launch-profile http
