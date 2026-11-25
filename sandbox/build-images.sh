#!/usr/bin/env bash
# Builds/pulls every image referenced by CodeExecution:Languages in appsettings.json.
# Run on the host that executes code (the API host when CodeExecution:Provider=Docker).
set -euo pipefail
cd "$(dirname "$0")"

docker build -t devinterview/sandbox-kotlin:2.0 images/kotlin

for image in python:3.12-slim node:22-slim gcc:14 eclipse-temurin:21-jdk mcr.microsoft.com/dotnet/sdk:8.0; do
  docker pull "$image"
done
echo "Sandbox images ready."
