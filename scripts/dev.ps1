# Local dev: SQL Server and MinIO in Docker, API (dotnet watch) and web (ng serve) on the host, with demo data.
# LOCAL DEVELOPMENT ONLY. Stop with Ctrl+C; scripts/dev-reset.ps1 wipes the data so the next run reseeds.
$ErrorActionPreference = 'Stop'
$apiPort = 5121

. (Join-Path $PSScriptRoot 'dev-infra.ps1')

$api = Start-Process dotnet -NoNewWindow -PassThru -ArgumentList @(
    'watch', '--project', 'src/Api', 'run', '--no-launch-profile', '--urls', "http://localhost:$apiPort")
try {
    if (-not (Test-Path web/node_modules)) { Invoke-Native npm @('--prefix', 'web', 'ci') }

    Write-Host ""
    Write-Host "  Web        http://localhost:4200"
    Write-Host "  API        http://localhost:$apiPort/swagger"
    Write-Host "  MinIO      http://localhost:9001"
    Write-Host "  Admin      $env:Admin__Email / $env:Admin__Password"
    Write-Host "  Customer   $env:Seed__CustomerEmail / $env:Seed__CustomerPassword   (local-only demo credentials from .env)"
    Write-Host ""

    $ErrorActionPreference = 'Continue'  # npm writes warnings to stderr
    & npm --prefix web start
}
finally {
    # dotnet watch runs the API as a child process; stop the whole tree.
    if ($IsWindows -or $env:OS -eq 'Windows_NT') { & taskkill /PID $api.Id /T /F *> $null }
    else { Stop-Process -Id $api.Id -ErrorAction SilentlyContinue }
}
