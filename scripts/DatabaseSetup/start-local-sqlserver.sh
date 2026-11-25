#!/usr/bin/env bash
# Starts a local SQL Server 2022 container for development.
# Usage: MSSQL_SA_PASSWORD='<strong password>' ./scripts/DatabaseSetup/start-local-sqlserver.sh
set -euo pipefail
: "${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD (8+ chars, upper/lower/digit/symbol)}"
NAME="${CONTAINER_NAME:-devinterview-sql}"

if docker ps -a --format '{{.Names}}' | grep -qx "$NAME"; then
  docker start "$NAME" >/dev/null
else
  docker run -d --name "$NAME" -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="$MSSQL_SA_PASSWORD" \
    -p 1433:1433 -v devinterview-sqldata:/var/opt/mssql mcr.microsoft.com/mssql/server:2022-latest >/dev/null
fi

echo -n "Waiting for SQL Server"
for _ in $(seq 1 60); do
  if docker exec "$NAME" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "SELECT 1" >/dev/null 2>&1; then
    echo " ready."
    echo "Connection string: Server=localhost,1433;Database=DevInterview;User Id=sa;Password=<your password>;TrustServerCertificate=True"
    exit 0
  fi
  echo -n "."; sleep 2
done
echo " timed out"; exit 1
