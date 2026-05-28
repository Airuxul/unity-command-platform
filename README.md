# unity-cli

[简体中文](README.zh-CN.md)

Command-line control of Unity Editor over loopback HTTP — for scripts, CI, and agents.

| Package | Role |
|---------|------|
| [unity-cmd](unity-cmd/) | Node.js CLI |
| [unity-connector](unity-connector/) | Unity UPM bridge (HTTP + commands) |

## How it works

```text
unity-cmd  →  GET /health, POST /list, POST /command, GET /commands/{id}
              unity-connector (Editor :6547, Editor Play :6794, Player :6795)
```

- Unity publishes the command catalog (`POST /list`); the CLI resolves aliases and caches it per endpoint (`~/.unity-cmd/cache/catalog-<host>:<port>.json`).
- Long work (`compile`, `play`, …) returns **HTTP 202**; the CLI polls `GET /commands/{id}`.
- Failures are JSON: `ok`, `error_code`, optional `hint`.

Built-ins include play/stop, console, exec, profiler, screenshot, menu, reserialize. Extend commands in the connector — see [unity-connector/README.md](unity-connector/README.md).

## Quick start

```bash
cd unity-cmd && npm install && npm link   # optional

unity-cmd profile create editor --host 127.0.0.1 --port 6547 --host-kind editor
unity-cmd --profile editor ping
unity-cmd --profile editor list
unity-cmd --profile editor play
unity-cmd --profile editor screenshot --view game --output_path Screenshots/game.png
unity-cmd --profile editor stop
```

Install the connector and open your Unity project: [unity-connector/README.md](unity-connector/README.md).  
CLI flags and npm scripts: [unity-cmd/README.md](unity-cmd/README.md).

After editing connector C#: `unity-cmd --profile editor compile` (alias `recompile`) or `refresh --compile true --timeout 30000`.

Fixed ports: **Editor `6547`**, **Editor Play `6794`**, **Player `6795`** — all three can run together on one machine.

## Commands per instance

Use **`--profile <name>`** (or `UNITY_CMD_PROFILE`). Create profiles once:

```bat
unity-cmd profile create editor --host 127.0.0.1 --port 6547 --host-kind editor
unity-cmd profile create editor-play --port 6794 --host-kind editor_play
unity-cmd profile create package-play --port 6795 --host-kind player
unity-cmd profile list
```

### 1. Editor — Edit Mode (profile `editor`, port **6547**)

**When:** Unity Editor is open.  
**Prerequisite:** Project has `unity-connector` installed; Console shows `http://127.0.0.1:6547/`.

```bat
unity-cmd --profile editor ping
unity-cmd --profile editor list
unity-cmd --profile editor state
unity-cmd --profile editor compile
unity-cmd --profile editor console --type error,warning --lines 20
unity-cmd --profile editor profiler --action enable
unity-cmd --profile editor play
unity-cmd --profile editor screenshot --view scene --output_path Screenshots/edit.png
```

### 2. Editor Play (profile `editor-play`, port **6794**)

**When:** Editor is in **Play Mode** (port listens only while playing).  
Enter Play with `--profile editor` first.

```bat
unity-cmd --profile editor-play ping
unity-cmd --profile editor-play echo
unity-cmd --profile editor screenshot --view game --output_path Screenshots/play.png
unity-cmd --profile editor-play profiler --action status
```

```bat
unity-cmd --profile editor play
REM ... editor-play commands ...
unity-cmd --profile editor stop
```

### 3. Development Build player (profile `package-play`, port **6795**)

**When:** A **Development Build** is running (not Release).

```bat
unity-cmd --profile package-play ping
unity-cmd --profile package-play list
unity-cmd --profile package-play echo
```

### Integration tests (from `unity-cmd/`)

```bat
set UNITY_CMD_PROFILE=editor
set UNITY_CMD_WORKSPACE=C:\Project\GameDemo
npm run test:integration

set UNITY_CMD_PROFILE=package-play
set UNITY_CMD_SCENARIO=player-runtime
npm run test:integration
```

| Profile (example) | `connector_host` | Port | Use |
|-------------------|------------------|------|-----|
| `editor` | `editor` | 6547 | Edit Mode, deferred commands, enter/exit Play |
| `editor-play` | `editor_play` | 6794 | During Editor Play |
| `package-play` | `player` | 6795 | Development Build |

Details: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#runtime--play-mode-http), [unity-cmd/README.md](unity-cmd/README.md#commands-per-instance), [unity-connector/docs/IMPLEMENTATION.md](unity-connector/docs/IMPLEMENTATION.md#runtime--play-mode-stack).

## vs [youngwoocho02/unity-cli](https://github.com/youngwoocho02/unity-cli)

Same idea (local HTTP, no MCP). **Not a fork** — different CLI, protocol, and extension model.

| | [youngwoocho02/unity-cli](https://github.com/youngwoocho02/unity-cli) | This repo |
|---|------------------------------------------------------------------------|-----------|
| CLI | Go binary, `install.sh` | Node `unity-cmd`, `npm install` |
| Discovery | `~/.unity-cli/instances/` heartbeat | profiles + `/health` |
| Commands | `unity-cli editor play` | `unity-cmd play` |
| Discovery | Per-request reflection; `list` + param schemas | `POST /list` catalog + CLI cache |
| Long tasks | Sync HTTP + `--wait` | **202 deferred command status** + poll |
| Runtime / Play-mode | **Editor Edit Mode only** — no HTTP while playing, no Dev player endpoint | **`editor_play` :6794** + **`player` :6795**; runtime via play/player profiles; editor tools stay on **`editor` :6547** |
| Status | `unity-cli status` | `ping`, `state` |
| Custom tools | `[UnityCliTool]` + `HandleCommand(JObject)` | `Descriptor` + single `Run(...)` command class + `[CliParam]` |
| Output | `success` / `message` | `ok`, `data`, `error_code`, `hint` |
| Extras | `test`, `update` | — (use Unity/CI for tests) |
| Compile deferred timeout | 120s | 30s (`compile` default) |

| Upstream | Here |
|----------|------|
| `unity-cli status` | `unity-cmd ping` |
| `unity-cli editor play` | `unity-cmd play` |
| `unity-cli exec "code"` | `unity-cmd exec --code "code"` |
| `unity-cli profiler hierarchy` | `unity-cmd profiler --action hierarchy` |
| `unity-cli editor refresh --compile` | `unity-cmd refresh --compile true` |

## Environment

| Variable | Purpose |
|----------|---------|
| `UNITY_CMD_PROFILE` | Default profile name (`--profile` overrides) |
| `UNITY_CMD_WORKSPACE` | Integration tests only: Unity project root for `assertFile` |
| `UNITY_CMD_TIMEOUT_MS` | Timeout (default `20000`) |
| `UNITY_CMD_TOKEN` | Optional auth token (must match Unity `UNITY_CMD_TOKEN`) |

Unity-side port overrides (when creating profiles): `UNITY_CMD_PORT`, `UNITY_CMD_EDITOR_PLAY_PORT`, `UNITY_CMD_PLAYER_PORT`.

## Tests

```bash
cd unity-cmd
npm run verify              # unit tests, no Unity
npm run test:integration    # needs Editor open; skips if no instance
```

## Documentation

| Doc | |
|-----|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Design and request flow |
| [docs/AGENTS.md](docs/AGENTS.md) | Automation notes |
| [unity-cmd/docs/IMPLEMENTATION.md](unity-cmd/docs/IMPLEMENTATION.md) | CLI internals |
| [unity-connector/docs/IMPLEMENTATION.md](unity-connector/docs/IMPLEMENTATION.md) | HTTP API and parameters |
