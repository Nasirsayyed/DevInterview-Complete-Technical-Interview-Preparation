#!/usr/bin/env bash
# Applies EF Core migrations to the database in ConnectionStrings__Default (or appsettings.Development.json).
# The API also migrates automatically on startup when Database:Provider=SqlServer; use this for controlled deployments.
# Usage: ConnectionStrings__Default='Server=...;Database=DevInterview;...' ./scripts/DatabaseSetup/apply-migrations.sh
set -euo pipefail
cd "$(dirname "$0")/../../backend"
dotnet tool restore >/dev/null 2>&1 || true
dotnet ef database update -p src/DevInterview.Infrastructure -s src/DevInterview.API
