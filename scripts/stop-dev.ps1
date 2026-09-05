[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repositoryRoot

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Required command 'docker' was not found."
}

$environmentFile = Join-Path $repositoryRoot ".env"
if (Test-Path -LiteralPath $environmentFile) {
    foreach ($line in Get-Content -LiteralPath $environmentFile) {
        if ($line -match '^\s*#' -or $line -notmatch '=') { continue }
        $key, $value = $line -split '=', 2
        $key = $key.Trim()
        if (@("POSTGRES_DB", "POSTGRES_USER", "POSTGRES_PASSWORD", "WORKCELL_DB_PORT") -contains $key) {
            [Environment]::SetEnvironmentVariable($key, $value.Trim(), "Process")
        }
    }
}

docker compose stop postgres
Write-Host "PostgreSQL stopped. Close WPF and use Ctrl+C in the backend terminal separately."
