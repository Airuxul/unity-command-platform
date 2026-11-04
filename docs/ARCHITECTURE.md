# UCP Architecture (implemented)

Authoritative design: [01_Architecture.md](../../../01_Architecture.md) (GameDemo root).  
Naming: [../CONVENTIONS.md](../CONVENTIONS.md).

## Packages

| Package | Role |
|---------|------|
| `ucp-cli` | npm — `ucp-cli` bin + `ucp-host` bin + shared protocol |
| `com.air.ucp-agent` | Unity UPM — Editor agent (FileQueue), no HTTP |

## Directory layout

```text
packages/ucp/
├── ucp-cli/
│   ├── bin/ucp-cli.js, ucp-host.js
│   └── src/
│       ├── shared/     # UcpCommand, ~/.ucp paths, zod
│       ├── cli/        # ucp-cli (commander)
│       └── host/       # ucp-host services + adapters
└── com.air.ucp-agent/
    ├── Editor/         # Bootstrap, Commands, Bridge
    └── Runtime/        # Protocol, Dispatch, Cli
```

## End-to-end flow (Editor)

```text
ucp-cli ping
    ↓
ucp-cli (CLI layer, stateless)
    ↓ HTTP POST /commands  (127.0.0.1:6610)
ucp-host
    ├── SessionService     reads ~/.ucp/sessions/*.json
    ├── QueueService       in-memory command lifecycle
    ├── SchedulerService   dispatch loop
    ├── TransportRouter    picks adapter by session type
    └── EditorFileQueueAdapter
            writes ~/.ucp/queues/{projectId}/inbox/{cmdId}.json
    ↓
com.air.ucp-agent (EditorAgentBootstrap poll)
    ├── UcpCliCommandHandler
    ├── CliCommandDiscovery → *Command
    └── writes outbox/{cmdId}.json
    ↓
ucp-host reads result → ucp-cli prints JSON
```

## Disk layout (`~/.ucp/`)

| Path | Writer | Purpose |
|------|--------|---------|
| `host.json` | ucp-host | pid, port |
| `host.lock` | ucp-cli | spawn guard |
| `sessions/{projectId}.json` | Unity agent | readiness, capabilities |
| `queues/{projectId}/inbox/` | ucp-host | commands to Unity |
| `queues/{projectId}/outbox/` | Unity agent | results |

## Host services (in-process)

| Service | Responsibility |
|---------|----------------|
| SessionService | Resolve project → session |
| QueueService | Created → Queued → Running → Completed / Failed |
| SchedulerService | Pick queued work, invoke router |
| DiscoveryService | Watch session files |
| CapabilityRegistry | Match command type to session |
| TransportRouter | Editor vs Runtime adapter |
| RetryPolicy | Retry transient failures |

## Unity agent

| Component | Responsibility |
|-----------|----------------|
| `EditorAgentBootstrap` | Poll inbox, write outbox |
| `UcpCliCommandHandler` | FileQueue ↔ `CliCommand` |
| `CliCommandDiscovery` | Register all `Editor/Commands/**` |
| `EditorJobStateManager` | Deferred compile/play/stop |
| `EditorActionCommand<T>` | Command shell: service payload → result, exception → error |
| `EditTargetCommand<T>` + `EditBatch` | Node commands: `Apply(scope, p)`; the batch opens the target, commits or reverts |

### Content command layer (`Editor/Services/`)

| Component | Responsibility |
|-----------|----------------|
| `Objects/EditTargetScope` | Resolves "what am I editing" — a loaded scene, a prefab asset (`LoadPrefabContents` → `SaveAsPrefabAsset`), or the open Prefab Mode stage. Persists only if `MarkDirty()` was called. |
| `Objects/GameObjectPath` | Node addressing: `Root/Body/Arm`, `Body[1]` for same-named siblings |
| `Objects/GameObjectService` · `ComponentService` | The single implementation behind `gameobject_*` / `component_*`, for both scopes |
| `Objects/SerializedValueReader` · `SerializedValueWriter` | Serialized-property read/write; symmetric shapes so reads feed back into writes |
| `Objects/JsonValues` · `ComponentTypeResolver` | Value coercion and type-name resolution, shared by params and the property writer |
| `Prefab/PrefabAssetService` · `PrefabInstanceService` · `PrefabStageService` | Prefab assets, instances/overrides, Prefab Mode |
| `Scenes/SceneService` · `Assets/AssetService` · `SelectionService` · `EditorSettingsService` | Scenes, AssetDatabase, Editor selection, tag/layer/tool tables |

## Command protocol

```json
{ "id": "cmd-…", "type": "ping", "timeout": 30000, "args": {} }
```

```json
{ "id": "cmd-…", "success": true, "duration": 12, "message": "pong", "data": {} }
```

`state` command returns `agent_state` (not legacy `connector_state`).

Declared `aliases` are registered as extra dispatcher keys and advertised as capabilities, so they
dispatch like real names.

## Not implemented yet

- Runtime Player HTTP adapter (skeleton only in `runtime-http/`)
- Real `build` pipeline (skeleton command)
- Cloud / SSH / ADB adapters (architecture §13)
- Test Runner and Package Manager commands — both are async and need the deferred-job pipeline
  (`TODO.zh-CN.md` AGENT-05 / AGENT-06)
