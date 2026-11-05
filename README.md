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
| Commands that outlive a domain reload (compile, play, stop, test_run) | The agent keeps a job for each across the reload and answers once it is done; the CLI long-polls ucp-host until then |

---

## Architecture overview

```text
┌──────────────────────────────────────────────────────┐
│  ucp-cli (stateless, any shell / CI / AI agent)      │
│  ucp-cli run echo --session-type editor --args ...   │
└─────────────────────┬────────────────────────────────┘
                      │ HTTP 127.0.0.1:6610, X-UCP-Token
┌─────────────────────▼────────────────────────────────┐
│  ucp-host (background daemon, auto-started)          │
│  ├── SessionDirectory — sessions/*.json, targets.json│
│  ├── CommandRunner   — one lane per session,         │
│  │                     deadlines, long polling       │
│  ├── EditorFileQueueAdapter  (Editor)                │
│  └── RuntimeHttpAdapter      (Runtime)               │
└──────┬──────────────────────────┬────────────────────┘
       │ writes inbox JSON        │ POST /command
       ▼                          ▼
~/.ucp/queues/{id}/inbox/    http://127.0.0.1:{port}/
       │                          │
┌──────▼───────────────┐   ┌──────▼──────────────────┐
│ com.air.ucp-agent    │   │ RuntimeAgentHost        │
│ (EditorAgentBootstrap│   │ (MonoBehaviour, Play    │
│  checks every tick)  │   │  Mode only)             │
│ CommandExecutor runs │   │ CommandExecutor runs    │
│ the CliCommand       │   │ the CliCommand          │
│ writes outbox JSON   │   │ returns JSON response   │
└──────────────────────┘   └─────────────────────────┘
```

---

## Key features

- **Zero Unity HTTP in Edit Mode** — the Editor agent uses a file-based queue. No `HttpListener`, no URL ACL, works without admin rights.
- **Auto-discovered sessions** — the agent writes `~/.ucp/sessions/{projectId}.json` every 2 s; `ucp-host` promotes it to a ready session automatically.
- **Targets config** — `~/.ucp/targets.json` stores Editor and Runtime targets. Runtime targets are probed via `/health`; Editor targets fall back to session-file discovery.
- **Capability gating** — each session advertises which command types it supports; `ucp-host` rejects unsupported commands before dispatching.
- **Waits instead of failing** — a command sent while the Editor imports assets waits until it is ready; each command has its own default timeout (`build` 30 min, `test_run` 10 min, `compile` 180 s, most others 30 s).
- **Domain Reload Safe** — commands that cause a reload still return their result, once; see below.
- **Play Mode runtime channel** — `RuntimeAgentHost` starts automatically on `AfterSceneLoad`, binds `127.0.0.1:6620` (configurable), and shuts down cleanly when Play Mode exits.
- **Multi-project safe** — queue paths are keyed by `SHA-256(projectPath)[0:16]`; multiple Unity instances coexist without conflicts.
- **Structured JSON everywhere** — every response is `{ ok, data, message, error_code, hint, timing }`. `error_code` is a fixed code (`node_not_found`, `play_mode_required`, `compile_error`, ...) a script can branch on; `ucp-cli help <command>` lists them.

---

## Domain Reload Safety

Unity wipes all C# static state when scripts recompile or Play Mode starts (**domain reload**).
Commands that cause one (`compile`, `refresh compile=true`, `play`, `stop`, `test_run`,
`package_add`, `package_remove`) still return their result, exactly once.

### How

1. A command that cannot finish inside one call is **deferred**: before it runs, the agent creates
   a job for it with its completion kind and deadline. Every other command answers synchronously
   and has no job.
2. The job is kept in memory, in `SessionState` (`Air.UcpAgent.Jobs`, survives a reload) and in
   `~/.ucp/jobs/{projectId}/{cmdId}.json` (survives anything short of a lost disk).
3. After the reload, `EditorJobStateManager` loads the jobs back before the first inbox poll. Each
   editor tick, a **completion policy** checks whether its job is done:

| Completion kind | Done when | Commands |
|-----------------|-----------|----------|
| `compilation` | Unity has stopped compiling; fails as `compile_error` if scripts do not compile | `compile`, `refresh compile=true` |
| `enter_play` / `exit_play` | Play Mode is on / off | `play`, `stop` |
| `tests` | the Test Runner reported the run and the Editor is back in Edit Mode | `test_run` |
| `packages` | the package is (or is no longer) registered and the Editor has settled | `package_add`, `package_remove` |
| `ui_wait` | the node condition holds; fails just before the deadline | `ui_wait` |
| `started` | completed by the command's own callback; **orphaned** by a reload, which kills it | — |

4. A finished job's result goes to the outbox and the job is deleted. A job past its deadline is
   deleted without a result: ucp-host counts the same deadline and has already answered `timeout`.

The inbox file is claimed (renamed to `.claimed`) before a command runs, and a command whose job
exists is never started again, so a reload in the middle cannot run it twice.

---

## Commands

The catalog is generated from the agent itself, so there is no checked-in command table to drift:

```powershell
ucp-cli help                  # every command, its description and its parameters
ucp-cli help prefab_create    # one command: parameters, an example call, its error codes
ucp-cli help --md             # every card as markdown, e.g. for an AI agent's instructions
```

The Editor commands, grouped by what they address:

| Family | What it covers |
|--------|----------------|
| `ping` `echo` `state` `console` `help` | Health, agent/editor state, console log, command catalog |
| `compile` `play` `stop` `pause` `refresh` `reserialize` `menu` `screenshot` `profiler` `exec` | Editor control and diagnostics |
| `build` `test_run` | Player builds; EditMode / PlayMode tests with their failures |
| `package_*` | List, add and remove UPM packages |
| `prefab_*` | Prefab assets, variants, instances, overrides, nesting, Prefab Mode |
| `gameobject_*` | Create, delete, duplicate, rename, reparent, transform, activate, tag, layer, info, tree, find |
| `component_*` | Add, remove, list, and read/write serialized properties |
| `scene_*` | Open, new, save, save-as, close, list, info |
| `asset_*` | List, find, info, folders, delete, move, duplicate, dependencies, GUIDs |
| `selection_*` | Read and drive the Editor selection |
| `tag_*` `layer_*` `tool_set` | Project tag/layer tables and the active transform tool |
| `prefab_edit` `scene_edit` | Several node commands on one prefab or scene, kept only if all succeed |
| `ui_snapshot` `ui_click` `ui_input` `ui_wait` | Play Mode only: read the UI, click and type into it, wait for what follows |

`ucp-cli run-script flow.json` runs a list of commands in order and stops at the first failure:

```json
[
  { "command": "play" },
  { "command": "ui_click", "args": { "node": "Canvas/Login/Go" } },
  { "command": "ui_wait", "args": { "node": "Canvas/Home", "until": "active" }, "timeout": 10000 },
  { "command": "stop" }
]
```

### One command set, two scopes

Every `gameobject_*` and `component_*` command takes an optional `prefab` argument. With it, the
command edits that prefab asset in place — no scene open, no Prefab Mode. Without it, it edits the
open scene. Prefab and scene editing therefore cannot drift apart; it is one implementation.

```powershell
# edit the open scene
ucp-cli run component_add --args '{"node":"Player/Body","component":"Rigidbody"}'

# edit the asset on disk
ucp-cli run component_add --args '{"prefab":"Assets/P/Player.prefab","node":"Player/Body","component":"Rigidbody"}'
```

### Batches

`prefab_edit` and `scene_edit` run a list of the same node commands (`gameobject_*`,
`component_*`, prefab instance commands) against one target. The prefab is saved once; if any op
fails, nothing is kept: the prefab file is untouched and a scene is undone. Every op is checked
before the target is opened. A single command follows the same rule, as a batch of one.

```powershell
ucp-cli run prefab_edit --args '{"prefab":"Assets/UI/Page.prefab","ops":[
  {"command":"gameobject_create","args":{"parent":"Page","name":"Card"}},
  {"command":"component_add","args":{"node":"Page/Card","component":"Image"}}]}'
```

Ops never carry `prefab` / `scene`; the target belongs to the batch.

Object-reference properties accept an asset path, a GUID, a node path or an instance id, and
`component_get` returns values in exactly the shape `component_set` takes back.

---

## Quick start

Node 20 or later. In a clone of this repository:

```powershell
cd ucp-cli
.\setup.ps1          # npm install, build, npm link: puts ucp-cli and ucp-host on PATH

ucp-cli ping
ucp-cli run state
ucp-cli host status
```

Without linking: `node <repo>/ucp-cli/bin/ucp-cli.js ping`. If `ucp-cli` is not found after
`setup.ps1`, the npm global folder (`npm prefix -g`) is missing from `PATH`; open a new terminal
after adding it.

Unity `Packages/manifest.json`, either from a local clone or straight from git:

```json
"com.air.ucp-agent": "file:C:/path/to/unity-command-platform/com.air.ucp-agent"
"com.air.ucp-agent": "https://github.com/Airuxul/unity-command-platform.git?path=/com.air.ucp-agent"
```

Open the Unity project and wait for compile — the agent writes `~/.ucp/sessions/{projectId}.json`.

### Upgrading from 0.3

Update the CLI and the Unity package together. Several parameters were renamed and every failure
now carries a code; the list is in
[docs/CHANGELOG_AGENT.md](docs/CHANGELOG_AGENT.md#upgrading-from-03).

### Runtime (Play Mode)

```powershell
# optional: pre-configure a named runtime target
ucp-cli targets set my-runtime --type runtime --host 127.0.0.1 --port 6620 --project C:/Project/GameDemo

ucp-cli run play --session-type editor
ucp-cli run echo --session-type runtime --args '{"message":"hello from runtime"}'
ucp-cli run stop --session-type editor
```

### Runtime Cmd Panel (in-game UGUI)

Drop `Resources/UcpCmdPanel.prefab` under a Canvas in your scene (the `` ` `` key shows and hides
it). Layout and `_fontSize` are set on the prefab. The scene needs a Canvas and an EventSystem.

```text
com.air.ucp-agent/Resources/UcpCmdPanel.prefab
com.air.ucp-agent/Runtime/CmdPanel/UcpCmdPanel.cs
```

| Area | What it does |
|------|--------------|
| History | Two lines per run: `> command`, then its result |
| Hints | The commands and `--parameters` matching what you are typing |
| Input | Click to focus; `Tab` completes, `Enter` runs, `↑` / `↓` walk the history |

**Adding runtime commands:** put a `CliCommand` / `CliCommand<TParams>` with
`CommandHostScope.Runtime` anywhere in your project's scripts. After compiling, the panel and
`ucp-cli --session-type runtime` both find it.

```text
> ping
pong

> echo --message hello
echo ok {"channel":"runtime","message":"hello"}
```

---

## Data layout (`~/.ucp/`)

```text
~/.ucp/
├── host.json                        # pid, port and token of the running ucp-host
├── host.lock                        # spawn guard
├── targets.json                     # user-configured targets
├── sessions/{projectId}.json        # written by the Unity agent (Editor)
├── jobs/{projectId}/{cmdId}.json    # deferred commands in flight; empty when idle
└── queues/{projectId}/
    ├── inbox/{cmdId}.json           # ucp-host → Unity; .claimed while it runs
    └── outbox/{cmdId}.json          # Unity → ucp-host; removed once read
```

---

## Tests

```powershell
cd ucp-cli
npm run verify                       # build + lint + unit tests (+ integration, skipped without Unity)

# Against an open, compiled Editor
$env:UCP_EDITOR_INTEGRATION="1"
$env:UCP_TEST_PROJECT="C:/path/to/UnityProject"
npm run test:integration             # about 3 min; $env:UCP_EDITOR_SLOW="1" adds compile/play and a real build
npm run smoke                        # one Prefab round trip, a few seconds
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
