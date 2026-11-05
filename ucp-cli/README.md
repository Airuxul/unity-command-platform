# ucp-cli

npm package for **ucp-cli** (CLI) and **ucp-host** (daemon).

## Setup

Node 20 or later.

```powershell
cd unity-command-platform/ucp-cli
.\setup.ps1          # npm install, build, npm link
```

If `ucp-cli` is not found afterwards, add the npm global folder (`npm prefix -g`) to `PATH` and
open a new terminal. Local without global link: `node .\ucp-cli ping` or `npm run ucp-cli -- ping`.

## Usage (Unity Editor open)

```powershell
ucp-cli ping
ucp-cli help                                  # command catalog; ucp-cli help <type> for one card
ucp-cli run echo --args '{"message":"hello"}'
ucp-cli run-script flow.json                  # [{"command": ..., "args": ..., "timeout": ...}, ...]
ucp-cli build Builds/Win/Game.exe --platform StandaloneWindows64
ucp-cli host status
```

Every result is JSON: `{ ok, data, message, error_code, hint, timing }`; the exit code is 0 only
when `ok` is true.

## Targets configuration

Targets live in `~/.ucp/targets.json`. Runtime targets use host/port; Editor targets can be auto-discovered from Unity session files or set explicitly.

```powershell
ucp-cli targets set device-runtime --type runtime --host 192.168.0.10 --port 6620 --project C:/Project/GameDemo
ucp-cli targets set gamedemo-editor --type editor --project C:/Project/GameDemo
ucp-cli targets list
ucp-cli host status
```

Runtime health is probed via `GET http://{host}:{port}/health` (no heartbeat file).

## Play + Runtime integration

Requires Unity Editor open and compiled:

```powershell
$env:UCP_EDITOR_INTEGRATION="1"
npm run test:integration
```

The `editor-runtime-play` suite enters Play Mode, runs `echo` on editor + runtime sessions, stops Play, then asserts runtime commands fail.

## Testing against a live Editor

Two tiers, same transport:

| | Command | Covers | Time |
|-|---------|--------|------|
| Smoke | `npm run smoke -- --project <path>` | One Prefab round trip, 18 assertions | a few seconds |
| Full | `npm run test:integration` (needs `UCP_EDITOR_INTEGRATION=1`) | Every editor command | ~6 min |

```powershell
npm run smoke -- --project C:/Path/To/UnityProject
```

The smoke test creates a prefab with no scene open, edits its contents, reads the values back off
disk, instantiates it into an additive scratch scene, overrides the instance and applies those
overrides to the asset — then deletes everything it made. It never touches the scene you have open.
Exit code is 0 only if all 18 pass, so it works as a CI gate.

`--project` defaults to `$UCP_TEST_PROJECT`, and with neither set the host picks the only ready
editor session. `--keep` leaves `Assets/UcpSmoke` behind for inspection after a failure.

## Development

```powershell
npm run verify
$env:UCP_EDITOR_INTEGRATION="1"
npm run test:integration
```

`UCP_EDITOR_SLOW=1` adds the compile / play cases and a real player build. See
[../docs/MAINTENANCE.md](../docs/MAINTENANCE.md#node-development) for the full gate.

## Layout

```text
ucp-cli/
├── ucp-cli, bin/ucp-cli.js, bin/ucp-host.js
├── scripts/smoke.js
├── src/shared/    protocol + ~/.ucp
├── src/cli/       ucp-cli (commander), host client, run-script
└── src/host/      ucp-host daemon
```

See [../CONVENTIONS.md](../CONVENTIONS.md).
