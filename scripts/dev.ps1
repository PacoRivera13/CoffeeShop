# Local dev: SQL Server and MinIO in Docker, API (dotnet watch) and web (ng serve) on the host, with demo data.
# LOCAL DEVELOPMENT ONLY. Stop with Ctrl+C; scripts/dev-reset.ps1 wipes the data so the next run reseeds.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$apiPort = 5121

function Invoke-Native([string] $File, [string[]] $Arguments) {
    # Windows PowerShell 5.1 turns native stderr (docker progress, warnings) into errors; rely on the exit code instead.
    $ErrorActionPreference = 'Continue'
    & $File @Arguments 2>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) { throw "'$File $($Arguments[0..1] -join ' ')' failed with exit code $LASTEXITCODE." }
}

if (-not (Test-Path .env)) { throw "Missing .env: run 'Copy-Item .env.example .env' first." }

# Load .env; Jwt__Key is skipped (and empty values too) so the user-secrets key below is the one the API uses.
foreach ($line in Get-Content .env) {
    if ($line -match '^\s*#' -or $line -notmatch '^([^=]+)=(.*)$') { continue }
    $key = $Matches[1].Trim(); $value = $Matches[2]
    if ($value -eq '' -or $key -eq 'Jwt__Key') { continue }
    Set-Item "env:$key" $value
}
Remove-Item env:Jwt__Key -ErrorAction SilentlyContinue

foreach ($var in 'MSSQL_SA_PASSWORD', 'MINIO_USER', 'MINIO_PASSWORD', 'Admin__Email', 'Admin__Password',
                 'Seed__CustomerEmail', 'Seed__CustomerPassword') {
    if (-not [Environment]::GetEnvironmentVariable($var)) { throw "Set $var in .env (see .env.example)." }
}

# One-time setup: a random local JWT signing key in user-secrets, never in git.
$ErrorActionPreference = 'Continue'
$secrets = & dotnet user-secrets list --project src/Api 2>$null
$ErrorActionPreference = 'Stop'
if (-not ($secrets -match '^Jwt:Key = ')) {
    Write-Host 'Generating a local Jwt:Key in dotnet user-secrets...'
    $bytes = New-Object byte[] 48
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    Invoke-Native dotnet @('user-secrets', 'set', 'Jwt:Key', [Convert]::ToBase64String($bytes), '--project', 'src/Api') | Out-Null
}

Invoke-Native docker @('compose', 'up', '-d', 'sqlserver', 'minio')

function Wait-Ready([string] $Name, [scriptblock] $Probe) {
    for ($i = 0; $i -lt 60; $i++) {
        try { if (& $Probe) { Write-Host "$Name is ready."; return } } catch { }
        Start-Sleep -Seconds 2
    }
    throw "$Name did not become ready within 120 s."
}
Wait-Ready 'SQL Server' {
    & docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P $env:MSSQL_SA_PASSWORD -Q 'SELECT 1' *> $null
    $LASTEXITCODE -eq 0
}
Wait-Ready 'MinIO' {
    (Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 http://127.0.0.1:9000/minio/health/ready).StatusCode -eq 200
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__Default = "Server=127.0.0.1,1433;Database=CoffeeShop;User Id=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=True"
if (-not $env:Jwt__Issuer) { $env:Jwt__Issuer = 'coffeeshop' }
if (-not $env:Jwt__Audience) { $env:Jwt__Audience = 'coffeeshop' }
$env:Seed__Demo = 'true'
$env:Outbox__Enabled = 'true'
$env:Storage__Endpoint = 'http://localhost:9000'
$env:Storage__PublicEndpoint = 'http://localhost:9000'
$env:Storage__CreateBucketIfMissing = 'true'
$env:Storage__AccessKey = $env:MINIO_USER
$env:Storage__SecretKey = $env:MINIO_PASSWORD

Invoke-Native dotnet @('tool', 'restore') | Out-Null
Invoke-Native dotnet @('ef', 'database', 'update', '-p', 'src/Infrastructure', '-s', 'src/Api')

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
