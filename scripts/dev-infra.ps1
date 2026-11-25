# Local dev infrastructure: loads .env, ensures a local Jwt:Key in user-secrets, starts SQL Server and MinIO in Docker,
# waits for them, applies migrations, and sets the API environment in this process. It also writes that environment to
# .vscode/api.env (git-ignored) for the VS Code "API" launch configuration.
# LOCAL DEVELOPMENT ONLY. Dot-sourced by scripts/dev.ps1; run on its own as the VS Code "infra" task.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

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

$apiEnv = [ordered]@{
    ASPNETCORE_ENVIRONMENT         = 'Development'
    ConnectionStrings__Default     = "Server=127.0.0.1,1433;Database=CoffeeShop;User Id=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=True"
    Jwt__Issuer                    = $(if ($env:Jwt__Issuer) { $env:Jwt__Issuer } else { 'coffeeshop' })
    Jwt__Audience                  = $(if ($env:Jwt__Audience) { $env:Jwt__Audience } else { 'coffeeshop' })
    Admin__Email                   = $env:Admin__Email
    Admin__Password                = $env:Admin__Password
    Seed__Demo                     = 'true'
    Seed__CustomerEmail            = $env:Seed__CustomerEmail
    Seed__CustomerPassword         = $env:Seed__CustomerPassword
    Outbox__Enabled                = 'true'
    Storage__Endpoint              = 'http://localhost:9000'
    Storage__PublicEndpoint        = 'http://localhost:9000'
    Storage__CreateBucketIfMissing = 'true'
    Storage__AccessKey             = $env:MINIO_USER
    Storage__SecretKey             = $env:MINIO_PASSWORD
}
foreach ($entry in $apiEnv.GetEnumerator()) { Set-Item "env:$($entry.Key)" $entry.Value }

New-Item -ItemType Directory -Force .vscode | Out-Null
# LF endings and no BOM, so env-file parsers never see a trailing CR or a BOM in the first key.
[IO.File]::WriteAllText((Join-Path $root '.vscode/api.env'),
    (($apiEnv.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "`n") + "`n",
    (New-Object Text.UTF8Encoding $false))

Invoke-Native dotnet @('tool', 'restore') | Out-Null
Invoke-Native dotnet @('ef', 'database', 'update', '-p', 'src/Infrastructure', '-s', 'src/Api')
Write-Host 'Infrastructure is ready; API environment written to .vscode/api.env.'
