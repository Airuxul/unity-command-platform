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
