# Database setup

| Script | Purpose |
|--------|---------|
| `start-local-sqlserver.sh` | Starts SQL Server 2022 in Docker for local development. |
| `apply-migrations.sh` | Runs `dotnet ef database update` against `ConnectionStrings__Default`. |
| `generate-migration-script.sh` | Produces an idempotent SQL script for reviewed production deployments. |

Seed data (technologies, questions, challenges, flashcards, learning paths, companies, plans, roles, achievements)
is loaded by the API on startup from `scripts/SeedData/*.json`; re-running is safe (upserts by slug/external key).
The first Super Admin is created only when `Seed__AdminEmail` and `Seed__AdminPassword` are set.

Never commit connection strings with real passwords; pass them as environment variables or user-secrets.
