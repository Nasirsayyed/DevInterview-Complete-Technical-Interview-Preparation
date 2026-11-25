# syntax=docker/dockerfile:1
# DevInterview API. Build from the repository root:  docker build -f backend/Dockerfile -t devinterview/api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/DevInterview.sln backend/
COPY backend/src/DevInterview.Domain/DevInterview.Domain.csproj backend/src/DevInterview.Domain/
COPY backend/src/DevInterview.Contracts/DevInterview.Contracts.csproj backend/src/DevInterview.Contracts/
COPY backend/src/DevInterview.Application/DevInterview.Application.csproj backend/src/DevInterview.Application/
COPY backend/src/DevInterview.Infrastructure/DevInterview.Infrastructure.csproj backend/src/DevInterview.Infrastructure/
COPY backend/src/DevInterview.API/DevInterview.API.csproj backend/src/DevInterview.API/
RUN dotnet restore backend/src/DevInterview.API/DevInterview.API.csproj
COPY backend/src backend/src
# Seed content packs are linked into the API output from scripts/SeedData.
COPY scripts/SeedData scripts/SeedData
RUN dotnet publish backend/src/DevInterview.API/DevInterview.API.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# The Docker code-execution provider shells out to the docker CLI (daemon access is granted at run time).
RUN apt-get update && apt-get install -y --no-install-recommends docker.io curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "DevInterview.API.dll"]
