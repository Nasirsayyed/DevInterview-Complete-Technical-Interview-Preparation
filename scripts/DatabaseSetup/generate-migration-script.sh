#!/usr/bin/env bash
# Generates an idempotent SQL script of all migrations for DBAs / pipelines that cannot run dotnet-ef against production.
# Output: scripts/DatabaseSetup/output/devinterview-migrations.sql (git-ignored).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
mkdir -p "$ROOT/scripts/DatabaseSetup/output"
cd "$ROOT/backend"
dotnet tool restore >/dev/null
dotnet ef migrations script --idempotent -p src/DevInterview.Infrastructure -s src/DevInterview.API \
  -o "$ROOT/scripts/DatabaseSetup/output/devinterview-migrations.sql"
echo "Wrote scripts/DatabaseSetup/output/devinterview-migrations.sql"
