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

Host modules:

| Module | Path |
|--------|------|
| HTTP server | `host/server/` |
| Sessions, discovery, runtime probes | `host/sessions/` (`SessionDirectory`) |
| Lanes, deadlines, long polling | `host/commands/` (`CommandRunner`) |
| Editor adapter (FileQueue) | `host/adapter/editor-file-queue/` |
| Runtime adapter (HTTP) | `host/adapter/runtime-http/` |

**Do not confuse:** the host's in-memory command lanes vs the disk **FileQueue** (`inbox`/`outbox`).

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
| `UCP_EDITOR_SLOW` | `1` = include compile/play/stop tests and the real build |
| `UCP_TEST_PROJECT` | Unity project the integration tests and smoke run against |
| `UCP_LOG_LEVEL` | Host log level |

## Unity (`com.air.ucp-agent`)

Single command track: **`CliCommand`** under `Editor/Commands/`. Editor commands normally inherit
`EditorActionCommand<TParams>` (thin shell) and delegate to a service. Commands that work inside a
scene or prefab inherit `EditTargetCommand<TParams>` instead and only implement
`Apply(scope, p)`; `EditBatch` opens the target, commits on success and reverts on failure.

| Path | Namespace |
|------|-----------|
| `Runtime/Commands/CommandNames.cs` | `Air.UcpAgent.Commands` |
| `Editor/Commands/**/*.cs` | `Air.UcpAgent.Editor.Commands` (flat — subfolders group, they do not nest the namespace) |
| `Editor/Params/*.cs` | `Air.UcpAgent.Params` |
| `Editor/Services/Objects/` | `Air.UcpAgent.Editor.Services.Objects` |
| `Editor/Services/Prefab/` | `Air.UcpAgent.Editor.Services.Prefab` |
| `Editor/Services/Scenes/` · `Editor/Services/Assets/` | `…Services.Scenes` · `…Services.Assets` |
| `Editor/IO/AssetPaths.cs` | `Air.UcpAgent.Editor.IO` |
| `Editor/TestRunner/` | `Air.UcpAgent.Editor.TestRunner` (own assembly, needs the test framework) |
| `Runtime/Cli/` | `Air.UcpAgent.Cli` (`CommandRegistry`, `CommandExecutor`, `UcpException`) |
| `Runtime/Protocol/` | `UcpCommand`, `UcpResult`, `UcpSession` |

Deferred commands (`compile`, `play`, `stop`, `test_run`, `package_add` / `package_remove`,
`ui_wait`) keep a job in `EditorJobStateManager` that a completion policy in `Editor/Completion/`
finishes; see [docs/MAINTENANCE.md](docs/MAINTENANCE.md#deferred-commands).

Error codes are lowercase snake case (`node_not_found`), declared once in `UcpErrorCodes`.
Parameter names used by several commands are constants in `ParamKeys`; `prefab` always means the
prefab asset a content command edits, `path` the asset a command operates on, `source` a prefab
to copy from.

### Command naming

`{domain}_{operation}`, lowercase, one operation per command — `prefab_create`, `component_set`,
`gameobject_reparent`. No multi-action `action` dispatchers. Use `aliases` for a synonym
(`prefab_move` → `prefab_rename`) rather than a second class; aliases are dispatched and advertised
as capabilities.

Batching reuses command names: `prefab_edit` / `scene_edit` take
`ops: [{command, args}]` where `command` is any batchable `EditTargetCommand`. Do not add a second
op vocabulary. A command that also writes outside its target (it could not be taken back if a
later op failed) overrides `Batchable => false`.

**Legacy (removed in 0.4.0):** `manage` (its actions became `pause`, `tag_add`, `tag_remove`,
`tag_list`, `layer_add`, `layer_remove`, `layer_list`, `tool_set`).

## CLI commands

| Command | Purpose |
|---------|---------|
| `ucp-cli ping` | Health check |
| `ucp-cli run <type> [--args JSON]` | Any agent command |
| `ucp-cli help [<type>] [--md]` | Command catalog, or one command's card |
| `ucp-cli run-script <file>` | Several commands in order, stops at the first failure |
| `ucp-cli build <output_path> [--platform] [--scene ...] [--development]` | Shortcut for `run build` |
| `ucp-cli targets list\|set\|remove\|show` | `~/.ucp/targets.json` |
| `ucp-cli host status` | Host + sessions |

Command `type` strings: lowercase, constants in `CommandNames.cs`.
