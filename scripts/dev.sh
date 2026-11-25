#!/usr/bin/env bash
# Local dev: SQL Server and MinIO in Docker, API (dotnet watch) and web (ng serve) on the host, with demo data.
# LOCAL DEVELOPMENT ONLY. Stop with Ctrl+C; scripts/dev-reset.sh wipes the data so the next run reseeds.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
api_port=5121

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

dotnet tool restore >/dev/null
dotnet ef database update -p src/Infrastructure -s src/Api

dotnet watch --project src/Api run --no-launch-profile --urls "http://localhost:$api_port" &
api_pid=$!
trap 'kill "$api_pid" 2>/dev/null || true' EXIT INT TERM

[[ -d web/node_modules ]] || npm --prefix web ci

cat <<EOF

  Web        http://localhost:4200
  API        http://localhost:$api_port/swagger
  MinIO      http://localhost:9001
  Admin      $Admin__Email / $Admin__Password
  Customer   $Seed__CustomerEmail / $Seed__CustomerPassword   (local-only demo credentials from .env)

EOF

npm --prefix web start
