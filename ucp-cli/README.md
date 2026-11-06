# ucp-cli

npm package for **ucp-cli** (CLI) and **ucp-host** (daemon).

## Setup

Node 20 or later.

```powershell
cd unity-command-platform/ucp-cli
.\setup.ps1          # npm install, build, npm link
```

If `ucp-cli` is not found afterwards, add the npm global folder (`npm prefix -g`) to `PATH` and
open a new terminal. Without a global link: `node .\bin\ucp-cli.js ping` or `npm run ucp-cli -- ping`.

Everyday commands, sessions, and adding a command: [../docs/GUIDE.zh-CN.md](../docs/GUIDE.zh-CN.md).
The command list is `ucp-cli help`. A result is `{ ok, data, message, error_code, hint, timing }`;
the exit code is 0 only when `ok` is true.

Editor sessions are discovered from `~/.ucp/sessions/`. A runtime session exists only when
`~/.ucp/targets.json` has a runtime target and its `/health` probe succeeds.

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
