# UCP Naming & Code Conventions

Applies to `packages/unity-command-platform/` (this repo), `ucp-cli/` (Node), and `com.air.ucp-agent/` (Unity).

## Product & layers

| Layer | Name | Artifact |
|-------|------|----------|
| Product | **UCP** | Unity Command Platform |
| L1 CLI | **ucp-cli** | npm package + bin `ucp-cli` |
| L2 Host | **ucp-host** | same npm package + bin `ucp-host` |
| L3 Agent | **ucp-agent** | UPM `com.air.ucp-agent` |

Data root: **`~/.ucp/`** (`sessions/`, `queues/`, `host.json`, `host.lock`).

**Legacy (do not use):** `uctl`, `unity-cli`, `unity-cmd`, `unity-connector`, `connector_state`, `~/.unity-cmd/`, Editor HTTP `:6547`.

## Repository layout

```text
packages/unity-command-platform/
├── ucp-cli/               # npm ucp-cli
├── com.air.ucp-agent/     # Unity UPM
├── docs/
├── CONVENTIONS.md
└── README.md
```

## Node (`ucp-cli`)

| Path | Role |
|------|------|
| `src/cli/` | CLI (`main.ts`, `ucp-host-client.ts`) |
| `src/host/` | ucp-host (`createUcpHost`, `UcpHostServer`, services) |
| `src/shared/` | Protocol, paths, zod |

Host submodules (match [01_Architecture.md](../../01_Architecture.md)):

| Service | Path |
|---------|------|
| Session | `host/session/` |
| Queue (in-memory) | `host/queue/` |
| Scheduler | `host/scheduler/` |
| Retry | `host/retry/` |
| Discovery | `host/discovery/` |
| Capability | `host/capability/` |
| Transport router | `host/router/` |
| Editor adapter (FileQueue) | `host/adapter/editor-file-queue/` |
| Runtime adapter (HTTP skeleton) | `host/adapter/runtime-http/` |

**Do not confuse:** Host `QueueService` (memory state machine) vs disk **FileQueue** (`inbox`/`outbox`).

### npm exports

- `ucp-cli` — `createUcpHost()`
- `ucp-cli/shared` — protocol + paths
- `ucp-cli/host` — host app
- `ucp-cli/cli/client` — HTTP client to ucp-host

### Environment

| Variable | Purpose |
|----------|---------|
| `UCP_ROOT` | Override `~/.ucp` |
| `UCP_HOST_PORT` | Host HTTP port (default `6610`) |
| `UCP_EDITOR_INTEGRATION` | `1` = live Unity integration tests |
| `UCP_EDITOR_SLOW` | `1` = include compile/play/stop tests |
| `UCP_LOG_LEVEL` | Host log level |

## Unity (`com.air.ucp-agent`)

Single command track: **`CliCommand`** under `Editor/Commands/`.

| Path | Namespace |
|------|-----------|
| `Runtime/Commands/CommandNames.cs` | `Air.UcpAgent.Commands` |
| `Editor/Commands/*.cs` | `Air.UcpAgent.Editor.Commands` |
| `Editor/Bridge/` | `Air.UcpAgent.Editor.Bridge` |
| `Runtime/Protocol/` | `UcpCommand`, `UcpResult`, `UcpSession` |

Deferred commands (`compile`, `play`, `stop`) use `EditorJobStateManager`; bridge tracks `DeferredJobId`.

## CLI commands

| Command | Purpose |
|---------|---------|
| `ucp-cli ping` | Health check |
| `ucp-cli run <type> [--args JSON]` | Any agent command |
| `ucp-cli host status` | Host + sessions |
| `ucp-cli build` | Shortcut for `run build` |

Command `type` strings: lowercase, constants in `CommandNames.cs`.
