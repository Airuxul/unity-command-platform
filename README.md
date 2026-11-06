# UCP (Unity Command Platform)

[简体中文](README.zh-CN.md)

Automate the Unity Editor and Play Mode from a shell, CI, or an agent. Edit Mode does not open an HTTP port.

| Package | Role |
|---------|------|
| [ucp-cli](ucp-cli/) | Node — `ucp-cli` and `ucp-host` |
| [com.air.ucp-agent](com.air.ucp-agent/) | Unity UPM — FileQueue agent and the commands |

Walkthrough: [docs/GUIDE.zh-CN.md](docs/GUIDE.zh-CN.md) (Chinese). Command list: `ucp-cli help`. Stack: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Reload and delivery: [docs/RELIABILITY.zh-CN.md](docs/RELIABILITY.zh-CN.md).

## What it does

`ucp-cli` talks to `ucp-host` on `127.0.0.1:6610`. The host starts itself. An Editor session is the Unity agent writing `~/.ucp/sessions/{projectId}.json`; commands for it go through files under `~/.ucp/queues/`. A runtime session is a Play Mode listener. It appears only after `ucp-cli targets set --type runtime` and a successful `/health` probe. Entering Play Mode does not add that session by itself.

One open Editor is selected automatically. Pass `--project` when several are open. `--session-type` is `editor` or `runtime`; omitted, the Editor wins.

Every result is `{ ok, data, message, error_code, hint, timing }`. The exit code is 0 only when `ok` is true. Host failures that never reached Unity use a `host_` code (`host_timeout`, `host_unknown_command`, ...). Unity's own wait is still `timeout`.

`gameobject_*` and `component_*` are one implementation. `prefab` edits that prefab asset; without it they edit the open scene. `group` runs several commands as one and takes them back together (`rollback=all`) or only the failed one (`rollback=failed`), across compile and Play Mode too.

Commands that reload the domain (`compile`, `play`, `stop`, `test_run`, `package_add`, `package_remove`, and `ui_wait`) still return once. Details are in the reliability doc.

## Install

Node 20 or later.

```powershell
cd ucp-cli
.\setup.ps1
ucp-cli ping
```

Without a global link: `node ucp-cli/bin/ucp-cli.js ping`. If the shell cannot find `ucp-cli` after `setup.ps1`, add `npm prefix -g` to `PATH` and open a new terminal.

Unity `Packages/manifest.json`:

```json
"com.air.ucp-agent": "file:C:/path/to/unity-command-platform/com.air.ucp-agent"
```

Open the project and wait until it compiles. Then `ucp-cli ping` should succeed.

Upgrade the CLI and the Unity package together, and every `ucp-cli` on the machine. A 0.2 CLI gets `unauthorized` from a newer host. Parameter renames are in [docs/CHANGELOG_AGENT.md](docs/CHANGELOG_AGENT.md#upgrading-from-03).

## Tests

```powershell
cd ucp-cli
npm run verify
```

Live Editor suites need `UCP_EDITOR_INTEGRATION=1`. See [ucp-cli/README.md](ucp-cli/README.md) and [docs/MAINTENANCE.md](docs/MAINTENANCE.md#node-development).
