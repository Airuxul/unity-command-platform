# UCP (Unity Command Platform)

[简体中文](README.zh-CN.md)

**Unity Command Platform** — automate Unity Editor and Runtime from the command line, CI scripts, or AI agents without any Unity-side HTTP server.

| Package | Role |
|---------|------|
| [ucp-cli](ucp-cli/) | Node — `ucp-cli` front-end + `ucp-host` daemon (single npm package) |
| [com.air.ucp-agent](com.air.ucp-agent/) | Unity UPM — Editor FileQueue agent + commands |

Naming: [CONVENTIONS.md](CONVENTIONS.md) · Docs: [docs/](docs/)

---

## Why UCP?

| Problem | UCP solution |
|---------|-------------|
| Unity Editor has no scriptable CLI | `com.air.ucp-agent` installs a lightweight poll-based inbox agent — no extra process, no port to open |
| CI/CD needs to drive Unity headlessly | `ucp-cli` is a standard npm binary; works inside any shell, pipeline, or agent loop |
| HTTP listeners break in firewalled or containerized environments | Editor transport uses local files (`~/.ucp/queues/`) — zero network required |
| Play Mode runtime needs a separate channel | Runtime `GET /health` + `POST /command` HTTP on loopback; discovered automatically, no profile setup |
| Multiple Unity instances on one machine | Each project gets its own queue directory keyed by project path hash — no port collisions |
| Commands that outlive a single HTTP request (compile, play, stop) | Host keeps an in-memory `QueueService`; deferred commands are driven by `EditorJobStateManager` in `Update()` |

---

## Architecture overview

```text
┌──────────────────────────────────────────────────────┐
│  ucp-cli (stateless, any shell / CI / AI agent)      │
│  ucp-cli run echo --session-type editor --args ...   │
└─────────────────────┬────────────────────────────────┘
                      │ HTTP POST /commands  127.0.0.1:6610
┌─────────────────────▼────────────────────────────────┐
│  ucp-host (background daemon, auto-started)          │
│  ├── SessionService  — reads ~/.ucp/sessions/*.json  │
│  ├── DiscoveryService — watches targets.json + files │
│  ├── QueueService   — in-memory command lifecycle    │
│  ├── SchedulerService — dispatch loop                │
│  ├── CapabilityRegistry — validates command type     │
│  └── TransportRouter                                 │
│        ├── EditorFileQueueAdapter  (Editor)          │
│        └── RuntimeHttpAdapter      (Runtime)         │
└──────┬──────────────────────────┬────────────────────┘
       │ writes inbox JSON        │ POST /command
       ▼                          ▼
~/.ucp/queues/{id}/inbox/    http://127.0.0.1:{port}/
       │                          │
┌──────▼──────────────┐    ┌──────▼──────────────────┐
│ com.air.ucp-agent   │    │ RuntimeAgentHost        │
│ (EditorAgentBootstrap│   │ (MonoBehaviour, Play Mode│
│  polls every 250ms) │    │  only)                  │
│ executes CliCommand  │    │ executes RuntimeCommand │
│ writes outbox JSON  │    │ returns JSON response   │
└─────────────────────┘    └─────────────────────────┘
```

---

## Key features

- **Zero Unity HTTP in Edit Mode** — the Editor agent uses a file-based queue. No `HttpListener`, no URL ACL, works without admin rights.
- **Auto-discovered sessions** — the agent writes `~/.ucp/sessions/{projectId}.json` every 2 s; `ucp-host` promotes it to a ready session automatically.
- **Targets config** — `~/.ucp/targets.json` stores Editor and Runtime targets. Runtime targets are probed via `/health`; Editor targets fall back to session-file discovery.
- **Capability gating** — each session advertises which command types it supports; `ucp-host` rejects unsupported commands before dispatching.
- **Deferred long-running commands** — compile, play, stop, and exec block inside `EditorJobStateManager` without timing out the HTTP caller.
- **Domain Reload Safe** — in-flight commands survive Unity's script compilation cycle without losing state; see below.
- **Play Mode runtime channel** — `RuntimeAgentHost` starts automatically on `AfterSceneLoad`, binds `127.0.0.1:6620` (configurable), and shuts down cleanly when Play Mode exits.
- **Multi-project safe** — queue paths are keyed by `SHA-256(projectPath)[0:16]`; multiple Unity instances coexist without conflicts.
- **Structured JSON everywhere** — every command response is `{ ok, data, message, error_code }` — easy to parse in scripts or AI tool calls.

---

## Domain Reload Safety

Unity wipes all C# static state when script compilation finishes (**Domain Reload**). UCP uses a two-layer persistence strategy so in-flight commands are never lost or stuck.

### Persistence layers

| Layer | Mechanism | Notes |
|-------|-----------|-------|
| In-memory | `EditorJobStateManager._commands` | Primary dictionary; rebuilt immediately from the layers below after each reload |
| Lightweight | `SessionState` (`Air.UcpAgent.Jobs`) | Unity's built-in Editor key-value store; survives Domain Reload automatically |
| Disk | `~/.ucp/jobs/{projectId}/{cmdId}.json` | Written by `EditorJobLedger`; recovers from any crash or hot reload |

### Recovery sequence after reload

```text
Domain Reload fires
    ↓
EditorJobStateManager  [InitializeOnLoad static ctor]
    1. PurgeCorruptFiles()        remove bad disk job files
    2. LoadFromSession()          restore dict from SessionState
    3. MergePendingInto()         fill gaps from disk ledger
    4. OrphanJobsAfterDomainReload()  mark non-resumable jobs as Orphaned
    5. Save()                     write merged state back to SessionState + disk
    ↓
EditorAgentBootstrap  [InitializeOnLoad static ctor]
    re-registers EditorApplication.update
    rewrites session file → ucp-host sees the agent as ready again
```

### Which commands survive a Domain Reload?

`OrphanJobsAfterDomainReload` decides based on the job's `CompletionKind` field:

| CompletionKind | After reload | Typical commands |
|----------------|-------------|-----------------|
| `compilation` | Resumes — `CompilationPolicy` checks `IsCompiling` flag | `compile`, `refresh` |
| `enter_play` | Resumes — `EnterPlayModePolicy` checks Play Mode state | `play` |
| `exit_play` | Resumes — `ExitPlayModePolicy` checks Play Mode state | `stop` |
| `deferred` | Resumes — caller drives progress in `Update()` | `exec` |
| other / empty | **Orphaned** — failure written to outbox immediately | commands without a registered policy |

### How does the CLI receive results after a reload?

`EditorAgentBootstrap.PollInbox()` resumes in the next `EditorApplication.update` tick (~250 ms). `ucp-host` picks up the outbox result before the command timeout and forwards it to `ucp-cli`. Orphaned commands also write an explicit error to the outbox, so the CLI never hangs indefinitely.

---

## Built-in commands

| Command | Transport | Description |
|---------|-----------|-------------|
| `ping` | Editor / Runtime | Round-trip health check |
| `echo` | Editor / Runtime | Return a message with channel annotation |
| `state` | Editor | Agent + Editor state snapshot (play mode, compiling, …) |
| `console` | Editor | Read Unity Console entries with type/count filters |
| `screenshot` | Editor | Capture Game or Scene view to a file |
| `profiler` | Editor | Enable / disable Profiler, read frame stats |
| `refresh` | Editor | `AssetDatabase.Refresh`, optionally trigger compile |
| `compile` | Editor | Request script compilation and wait for result |
| `play` / `stop` | Editor | Enter / exit Play Mode |
| `manage` | Editor | Tags, layers, active tool, misc editor actions |
| `menu` | Editor | Execute any `MenuItem` by path |
| `build` | Editor | Trigger a Player build (skeleton — pipeline TBD) |
| `reserialize` | Editor | Force-reserialize assets |
| `exec` | Editor | Execute a C# snippet inside the Editor |

---

## Quick start

```powershell
cd ucp-cli
.\setup.ps1          # npm install + npm link (one-time)

ucp-cli ping
ucp-cli run state
ucp-cli host status
```

Unity `Packages/manifest.json`:

```json
"com.air.ucp-agent": "file:../CustomPackages/packages/unity-command-platform/com.air.ucp-agent"
```

Open the Unity project and wait for compile — the agent writes `~/.ucp/sessions/{projectId}.json`.

### Runtime (Play Mode)

```powershell
# optional: pre-configure a named runtime target
ucp-cli targets set my-runtime --type runtime --host 127.0.0.1 --port 6620 --project C:/Project/GameDemo

ucp-cli run play --session-type editor
ucp-cli run echo --session-type runtime --args '{"message":"hello from runtime"}'
ucp-cli run stop --session-type editor
```

---

## Data layout (`~/.ucp/`)

```text
~/.ucp/
├── host.json                        # pid + port of running ucp-host
├── host.lock                        # spawn guard
├── targets.json                     # user-configured targets
├── sessions/{projectId}.json        # written by Unity agent (Editor)
└── queues/{projectId}/
    ├── inbox/{cmdId}.json           # ucp-host → Unity
    └── outbox/{cmdId}.json          # Unity → ucp-host
```

---

## Tests

```powershell
cd ucp-cli
npm run verify                       # build + lint + unit tests

# Integration tests (Unity Editor must be open and compiled)
$env:UCP_EDITOR_INTEGRATION="1"
npm run test:integration
```

---

## Request flow summary

```text
ucp-cli → ucp-host (:6610) → ~/.ucp/queues/.../inbox    (Editor)
                            → POST http://…/command       (Runtime)
         → com.air.ucp-agent poll / RuntimeAgentHost
         → outbox / HTTP response
         → ucp-host → ucp-cli prints JSON
```

No Unity-side HTTP in Edit Mode. No `~/.unity-cmd/` profiles.
