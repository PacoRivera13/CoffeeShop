# Removes the local containers and their volumes, so the next scripts/dev.ps1 run starts and reseeds from scratch.
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
docker compose down -v
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
