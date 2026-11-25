# Code execution sandbox

User code **never runs inside the API process**. The API's `ICodeExecutionProvider` picks one of:

| Provider | Config (`CodeExecution:Provider`) | Notes |
|----------|-----------------------------------|-------|
| Docker sandbox | `Docker` | Default for self-hosting. One throw-away container per run. |
| Judge0 | `Judge0` | Use a managed/self-hosted Judge0 CE; set `CodeExecution:Judge0:BaseUrl` and `LanguageIds` (+ `AuthToken` via an env var). |
| Disabled | `Disabled` | Run/Submit return `execution_unavailable`; the rest of the app works. |

## Docker sandbox isolation

Every run starts a new container with:

- `--network none` (no network), `--read-only` root filesystem, `tmpfs` for `/work` and `/tmp`
- `--memory` = `--memory-swap` = the challenge memory limit, `--cpus 1`, `--pids-limit 64`
- `--cap-drop ALL`, `--security-opt no-new-privileges`, `--user 65534:65534` (nobody)
- per-test-case `timeout -s KILL` and a hard wall-clock deadline enforced by the API (container is force-removed)
- output is truncated to `CodeExecution:OutputLimitBytes`

Source and inputs are written to a fresh directory under `CodeExecution:WorkRoot` and mounted **read-only**.
Compilation happens once, then all test cases run in the same container.

## Images

`build-images.sh` builds `devinterview/sandbox-kotlin:2.0` (Kotlin has no official compiler image) and pulls the
official images for Python, Node.js, GCC, Temurin and the .NET SDK. Pin digests in production by replacing the
`Image` values in `appsettings.Production.json` with `image@sha256:…`.

## Running the API in a container

The provider shells out to `docker`, so the API container needs access to a Docker daemon **and** the work
directory must have the same path inside the container and on the host (it is bind-mounted into sandboxes).
`docker-compose.yml` does this with `/var/lib/devinterview-sandbox`. Access to the Docker socket is equivalent to
root on that host: in production run the API (or a dedicated execution worker) on a host used only for this, or
use Judge0.
