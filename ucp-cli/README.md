# ucp-cli

npm package for **ucp-cli** (CLI) and **ucp-host** (daemon).

## Setup

```powershell
cd CustomPackages/packages/unity-command-platform/ucp-cli
.\setup.ps1
```

## Usage (Unity Editor open)

```powershell
ucp-cli ping
ucp-cli build
ucp-cli host status
ucp-cli run echo --args '{"message":"hello"}'
```

Local without global link: `node .\ucp-cli ping` or `npm run ucp-cli -- ping`

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
| Smoke | `npm run smoke -- --project <path>` | One Prefab round trip, 18 assertions | ~3-4 min |
| Full | `npm run test:integration` (needs `UCP_EDITOR_INTEGRATION=1`) | Every editor command | ~16 min |

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
npm run test:integration:editor
```

## Layout

```text
ucp-cli/
├── ucp-cli, bin/ucp-cli.js, bin/ucp-host.js
├── src/shared/    protocol + ~/.ucp
├── src/cli/       ucp-cli (commander)
└── src/host/      ucp-host daemon
```

See [../CONVENTIONS.md](../CONVENTIONS.md).
