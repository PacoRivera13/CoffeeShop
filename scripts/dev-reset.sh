#!/usr/bin/env bash
# Removes the local containers and their volumes, so the next scripts/dev.sh run starts and reseeds from scratch.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
docker compose down -v
