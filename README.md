# unity-cli

[简体中文](README.zh-CN.md)

Command-line control of Unity Editor over loopback HTTP — for scripts, CI, and agents.

| Package | Role |
|---------|------|
| [unity-cmd](unity-cmd/) | Node.js CLI |
| [com.air.unity-connector](com.air.unity-connector/) | Unity UPM bridge (HTTP + commands) |

## How it works

```text
unity-cmd  →  GET /health, POST /list, POST /command, GET /commands/{id}
              com.air.unity-connector (Editor :6547, Editor Play :6794, Player :6795)
```

- Unity publishes the command catalog (`POST /list`); the CLI resolves aliases and caches it per endpoint (`~/.unity-cmd/cache/catalog-<host>:<port>.json`).
- Long work (`compile`, `play`, …) returns **HTTP 202**; the CLI polls `GET /commands/{id}`.
- Failures are JSON: `ok`, `error_code`, optional `hint`.

Built-ins include play/stop, console, exec, profiler, screenshot, menu, reserialize. Extend commands in the connector — see [com.air.unity-connector/README.md](com.air.unity-connector/README.md).

Editor commands wait on `~/.unity-cmd/instances/*.json` heartbeat + `editor-http.json` + `/health` (`session_id` / `generation`) so domain reloads do not hit a stale listener.

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

Install the connector and open your Unity project: [com.air.unity-connector/README.md](com.air.unity-connector/README.md).  
CLI flags and npm scripts: [unity-cmd/README.md](unity-cmd/README.md).

After editing connector C#: `unity-cmd --profile editor compile` (alias `recompile`). Default timeout is **20s**; do not raise unless needed.

Fixed ports: **Editor `6547`**, **Editor Play `6794`**, **Player `6795`** — all three can run together on one machine.

## AI / Agent usage

The [Cursor Agent Skill](https://cursor.com/docs/skills) source lives in **`docs/unity-cmd-skill/`** (committed to Git). `.cursor/` is usually not committed — **copy the folder into your local skills directory once**.

### Install the skill (required)

Copy **`docs/unity-cmd-skill`** to:

```text
<repo-root>/.cursor/skills/unity-cmd/
```

The folder name **must be `unity-cmd`** (matches `name: unity-cmd` in `SKILL.md`).

```powershell
# Windows PowerShell (repo root)
New-Item -ItemType Directory -Force -Path .cursor\skills | Out-Null
Copy-Item -Recurse -Force docs\unity-cmd-skill .cursor\skills\unity-cmd
```

```bash
# macOS / Linux
mkdir -p .cursor/skills
cp -R docs/unity-cmd-skill .cursor/skills/unity-cmd
```

See [docs/unity-cmd-skill/README.md](docs/unity-cmd-skill/README.md).

### Layout in the repo

```text
docs/unity-cmd-skill/           # source in Git → copy to .cursor/skills/unity-cmd/
  SKILL.md
  references/guide.zh-CN.md
```

### How to enable

| Method | Description |
|--------|-------------|
| Auto | After install, Cursor loads `.cursor/skills/unity-cmd/` for Unity tasks |
| `/unity-cmd` | Slash command in chat |
| `@` mention | `@.cursor/skills/unity-cmd/SKILL.md` (after install), or `@docs/unity-cmd-skill/SKILL.md` to read the spec |
| Full guide | [docs/unity-cmd-skill/references/guide.zh-CN.md](docs/unity-cmd-skill/references/guide.zh-CN.md) |
| Quick ref | [docs/AGENTS.md](docs/AGENTS.md) |

### Default logic (user asks to do X in Editor or runtime)

```text
1. Pick domain → profile (editor / editor-play / package-play)
2. unity-cmd --profile <name> list → match user intent on commands[]
3. Catalog present but no match → list --refresh-catalog → match again
4. Still no match → explain, suggest 2–5 command names → abort; do not invent commands
5. ping OK → run matched command
```

Catalog lives in `~/.unity-cmd/cache/catalog-<host>_<port>.json` (CLI-managed). **Do not** copy it into skill markdown. Refresh: `list --refresh-catalog`.

### Example 1: check compile after script changes

**User:** *"I changed C# — check Unity compile errors."*

**Agent:**

1. `unity-cmd --profile editor list`; confirm `compile` and `console` in `commands`.
2. No match → abort per skill; do not invent commands.
3. Run:

```bash
unity-cmd --profile editor ping
unity-cmd --profile editor compile
unity-cmd --profile editor console --type error,warning --lines 30
```

4. Report from JSON `ok`, `error_code`, `hint`; do not enter Play if `compile` failed.

### Example 2: Play Mode screenshot

**User:** *"Enter Play and capture the Game view."*

**Agent:** **editor** domain (`play` / `screenshot` / `stop` use profile `editor`):

```bash
unity-cmd --profile editor ping
unity-cmd --profile editor play
unity-cmd --profile editor screenshot --view game --output_path Screenshots/agent-play.png
unity-cmd --profile editor stop
```

For runtime `echo` while playing: `unity-cmd --profile editor-play list`, then use profile `editor-play`.

### Example 3: refresh command catalog

**User:** *"Refresh the Unity Editor command cache."*

```bash
unity-cmd --profile editor list --refresh-catalog
```

Updates `~/.unity-cmd/cache/catalog-*.json` via CLI; do **not** write skill markdown copies.

### Example 4: one-shot Cursor prompt

```text
Follow @.cursor/skills/unity-cmd/SKILL.md (install `docs/unity-cmd-skill` first if needed): run unity-cmd --profile editor list to confirm compile/console exist,
then compile in the Unity Editor and show the last 20 error/warning console lines.
```

### Rules for agents

- Flags come from `list` / catalog JSON — never invent CLI flags; do not maintain skill markdown command caches.
- `play` / `stop` → profile **`editor`** only; runtime `echo` while playing → **`editor-play`**.

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
**Prerequisite:** Project has `com.air.unity-connector` installed; Console shows `http://127.0.0.1:6547/`.

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

Details: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#runtime--play-mode-http), [unity-cmd/README.md](unity-cmd/README.md#commands-per-instance), [com.air.unity-connector/docs/IMPLEMENTATION.md](com.air.unity-connector/docs/IMPLEMENTATION.md#runtime--play-mode-stack).

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
| [docs/unity-cmd-skill/](docs/unity-cmd-skill/SKILL.md) | **Cursor Skill source** (copy to `.cursor/skills/unity-cmd/`) |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Design and request flow |
| [docs/AGENTS.md](docs/AGENTS.md) | Automation quick reference |
| [unity-cmd/docs/IMPLEMENTATION.md](unity-cmd/docs/IMPLEMENTATION.md) | CLI internals |
| [com.air.unity-connector/docs/IMPLEMENTATION.md](com.air.unity-connector/docs/IMPLEMENTATION.md) | HTTP API and parameters |
