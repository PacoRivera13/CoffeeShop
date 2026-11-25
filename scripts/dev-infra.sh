#!/usr/bin/env bash
# Local dev infrastructure: loads .env, ensures a local Jwt:Key in user-secrets, starts SQL Server and MinIO in Docker,
# waits for them, applies migrations, and exports the API environment. It also writes that environment to
# .vscode/api.env (git-ignored) for the VS Code "API" launch configuration.
# LOCAL DEVELOPMENT ONLY. Sourced by scripts/dev.sh; run on its own as the VS Code "infra" task.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

[[ -f .env ]] || { echo "Missing .env: run 'cp .env.example .env' first." >&2; exit 1; }

# Load .env; Jwt__Key is skipped (and empty values too) so the user-secrets key below is the one the API uses.
while IFS= read -r line || [[ -n "$line" ]]; do
  line="${line%$'\r'}"
  [[ -z "$line" || "$line" == \#* || "$line" != *=* ]] && continue
  key="${line%%=*}"; value="${line#*=}"
  [[ -z "$value" || "$key" == "Jwt__Key" ]] && continue
  export "$key=$value"
done < .env
unset Jwt__Key

for var in MSSQL_SA_PASSWORD MINIO_USER MINIO_PASSWORD Admin__Email Admin__Password Seed__CustomerEmail Seed__CustomerPassword; do
  [[ -n "${!var:-}" ]] || { echo "Set $var in .env (see .env.example)." >&2; exit 1; }
done

# One-time setup: a random local JWT signing key in user-secrets, never in git.
if ! dotnet user-secrets list --project src/Api 2>/dev/null | grep -q '^Jwt:Key ='; then
  echo "Generating a local Jwt:Key in dotnet user-secrets..."
  dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)" --project src/Api >/dev/null
fi

docker compose up -d sqlserver minio

wait_for() {
  local name="$1"; shift
  for _ in $(seq 1 60); do
    if "$@" >/dev/null 2>&1; then echo "$name is ready."; return 0; fi
    sleep 2
  done
  echo "$name did not become ready within 120 s." >&2; exit 1
}
wait_for "SQL Server" docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "SELECT 1"
wait_for "MinIO" curl -fsS http://127.0.0.1:9000/minio/health/ready

export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__Default="Server=127.0.0.1,1433;Database=CoffeeShop;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
export Jwt__Issuer="${Jwt__Issuer:-coffeeshop}" Jwt__Audience="${Jwt__Audience:-coffeeshop}"
export Seed__Demo=true Outbox__Enabled=true
export Storage__Endpoint=http://localhost:9000 Storage__PublicEndpoint=http://localhost:9000 Storage__CreateBucketIfMissing=true
export Storage__AccessKey="$MINIO_USER" Storage__SecretKey="$MINIO_PASSWORD"

mkdir -p .vscode
( umask 077
  for var in ASPNETCORE_ENVIRONMENT ConnectionStrings__Default Jwt__Issuer Jwt__Audience Admin__Email Admin__Password \
             Seed__Demo Seed__CustomerEmail Seed__CustomerPassword Outbox__Enabled Storage__Endpoint Storage__PublicEndpoint \
             Storage__CreateBucketIfMissing Storage__AccessKey Storage__SecretKey; do
    printf '%s=%s\n' "$var" "${!var}"
  done > .vscode/api.env )

dotnet tool restore >/dev/null
dotnet ef database update -p src/Infrastructure -s src/Api
echo "Infrastructure is ready; API environment written to .vscode/api.env."
