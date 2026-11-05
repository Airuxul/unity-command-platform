# UCP Architecture

Naming: [../CONVENTIONS.md](../CONVENTIONS.md). Reload and delivery guarantees:
[RELIABILITY.zh-CN.md](RELIABILITY.zh-CN.md).

## Packages

| Package | Role |
|---------|------|
| `ucp-cli` | npm — `ucp-cli` bin + `ucp-host` bin + shared protocol |
| `com.air.ucp-agent` | Unity UPM — Editor agent (FileQueue, no HTTP) and the Play Mode runtime agent (loopback HTTP) |

## Directory layout

```text
unity-command-platform/
├── ucp-cli/
│   ├── bin/ucp-cli.js, ucp-host.js
│   ├── scripts/smoke.js
│   └── src/
│       ├── shared/     # protocol types, zod schemas, ~/.ucp paths, target store
│       ├── cli/        # ucp-cli (commander), host client, run-script, targets
│       └── host/       # server, sessions/, commands/, adapter/
└── com.air.ucp-agent/
    ├── Runtime/        # Cli/ (registry, executor, results), Invoke/, Job/, Params/, Protocol/,
    │                   # Agent/ (runtime HTTP agent), CmdPanel/, Commands/ (names, runtime commands)
    └── Editor/         # EditorAgentBootstrap, Dispatch/ (jobs), Completion/ (policies),
                        # Commands/, Params/, Services/, TestRunner/ (own assembly)
```

## End-to-end flow (Editor)

```text
ucp-cli run gameobject_create --args '{...}'
    ↓ POST /commands, then GET /commands/:id?wait=25000 until done   (127.0.0.1:6610, X-UCP-Token)
ucp-host
    ├── SessionDirectory   picks the session (sessions/*.json, targets.json, runtime /health probes)
    ├── CommandRunner      one lane per session; deadline from submission; waits for a busy Editor
    └── EditorFileQueueAdapter
            writes ~/.ucp/queues/{projectId}/inbox/{cmdId}.json, watches outbox/
    ↓
com.air.ucp-agent  (EditorAgentBootstrap, every editor tick)
    ├── claims the file: inbox/{cmdId}.json → {cmdId}.claimed
    ├── CommandExecutor: CommandRegistry lookup → CliParamBinder → CliCommand.Run
    │       synchronous: result now
    │       deferred:    job in EditorJobStateManager; a completion policy finishes it later
    └── writes outbox/{cmdId}.json
    ↓
ucp-host reads the result, deletes both queue files → ucp-cli prints JSON
```

The runtime path is the same up to the adapter: `RuntimeHttpAdapter` POSTs to the Play Mode
agent's `/command`, and `RuntimeAgentHost` runs it through the same `CommandExecutor`. Runtime
commands cannot defer.

## Disk layout (`~/.ucp/`)

| Path | Writer | Purpose |
|------|--------|---------|
| `host.json` | ucp-host | pid, port, token |
| `host.lock` | ucp-cli | spawn guard |
| `targets.json` | ucp-cli `targets` | configured Editor / runtime targets |
| `sessions/{projectId}.json` | Unity agent | status, capabilities, pid, focused, default timeouts; every 2 s |
| `queues/{projectId}/inbox/` | ucp-host | commands to Unity |
| `queues/{projectId}/outbox/` | Unity agent | results |
| `jobs/{projectId}/` | Unity agent | deferred commands in flight, for recovery after a reload |

## ucp-host

| Module | Responsibility |
|--------|----------------|
| `server/ucp-host-server.ts` | HTTP: `/health` (open), `/sessions`, `/targets`, `/commands` (token); refuses `Origin` and foreign `Host` |
| `sessions/session-directory.ts` | Watches `sessions/`, probes runtime targets, decides ready / busy / gone |
| `commands/command-runner.ts` | Lanes, deadlines, long polling, results kept 10 min, timeout hints |
| `adapter/editor-file-queue/` | Inbox / outbox exchange, sweep of files older than an hour |
| `adapter/runtime-http/` | POST to the runtime agent |

## Unity agent

| Component | Responsibility |
|-----------|----------------|
| `Runtime/Cli/CommandRegistry` | Finds every `CliCommand` subclass once per domain; lookup per host (editor / runtime) |
| `Runtime/Cli/CommandExecutor` | Bind, run, and map the outcome to a `UcpResult`; creates the job of a deferred command |
| `Runtime/Cli/UcpException` · `UcpErrorCodes` | The error codes, with hints |
| `Editor/EditorAgentBootstrap` | Session file, inbox claim, outbox writes, refusing commands while tests run |
| `Editor/Dispatch/EditorJobStateManager` · `EditorJobLedger` | Jobs in memory, `SessionState` and `jobs/`; reload recovery; deadlines |
| `Editor/Completion/*Policy` | When a job is done: compilation, enter / exit play, tests, packages, ui_wait |
| `Editor/Commands/EditorActionCommand<T>` | Command shell: service payload → result, exception → coded failure |
| `Editor/Commands/EditTargetCommand<T>` + `Editor/Services/Objects/EditBatch` | Node commands: `Apply(scope, p)`; the batch opens the target, commits or reverts |

### Content command layer (`Editor/Services/`)

| Component | Responsibility |
|-----------|----------------|
| `Objects/EditTargetScope` | What a command edits: a loaded scene, a prefab asset (`LoadPrefabContents` → `SaveAsPrefabAsset`), or the open Prefab Mode stage |
| `Objects/GameObjectPath` | Node addressing: `Root/Body/Arm`, `Body[1]` for same-named siblings; near-miss hints |
| `Objects/GameObjectService` · `ComponentService` | The single implementation behind `gameobject_*` / `component_*`, for both scopes |
| `Objects/SerializedValueReader` · `SerializedValueWriter` | Serialized-property read/write; symmetric shapes so reads feed back into writes |
| `Prefab/*` · `Scenes/*` · `Assets/*` | Prefab assets, instances, Prefab Mode; scenes (`SceneLookup`); AssetDatabase (`AssetQuery`) |
| `PlayModeNodes` · `UiSnapshotService` · `UiInteractionService` | Play Mode UI: find, read, click, type, wait |
| `BuildService` · `PackageService` · `TestRunState` | Player builds, UPM packages, test-run state shared with `Editor/TestRunner` |

## Command protocol

```json
{ "id": "cmd-…", "type": "ping", "timeout": 30000, "args": {} }
```

```json
{ "id": "cmd-…", "success": false, "duration": 3, "error": "node_not_found",
  "message": "Node 'Canvas/Gp' not found. …", "hint": "Did you mean 'Canvas/Go'?", "data": null }
```

ucp-cli turns this into `{ ok, data, message, error_code, hint, timing }`.

Declared `aliases` are lookup keys and are advertised as capabilities, so they dispatch like real
names.

## Not implemented

- Cloud / SSH / ADB adapters.
- Prefab backups and `ucp-cli restore` (`TODO.zh-CN.md` AGENT-19 / CLI-07).
