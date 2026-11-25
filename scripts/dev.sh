#!/usr/bin/env bash
# Local dev: SQL Server and MinIO in Docker, API (dotnet watch) and web (ng serve) on the host, with demo data.
# LOCAL DEVELOPMENT ONLY. Stop with Ctrl+C; scripts/dev-reset.sh wipes the data so the next run reseeds.
set -euo pipefail

api_port=5121
# shellcheck source=dev-infra.sh
source "$(dirname "${BASH_SOURCE[0]}")/dev-infra.sh"

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
