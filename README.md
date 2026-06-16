# UCP (Unity Command Platform)

[简体中文](README.zh-CN.md)

**Unity Command Platform** — decoupled CLI, Command Host, and Unity Agent.

| Package | Role |
|---------|------|
| [ucp-cli](ucp-cli/) | Node — `ucp-cli` front-end + `ucp-host` daemon (single npm package) |
| [com.air.ucp-agent](com.air.ucp-agent/) | Unity UPM — Editor FileQueue agent + commands |

Design: [../../01_Architecture.md](../../01_Architecture.md) · Naming: [CONVENTIONS.md](CONVENTIONS.md) · Docs: [docs/](docs/)

## Quick start

```powershell
cd ucp-cli
.\setup.ps1

ucp-cli ping
ucp-cli run state
ucp-cli host status
```

Unity `Packages/manifest.json`:

```json
"com.air.ucp-agent": "file:../CustomPackages/packages/unity-command-platform/com.air.ucp-agent"
```

Open the Unity project and wait for compile — the agent writes `~/.ucp/sessions/{projectId}.json`.

## Tests

```powershell
cd ucp-cli
npm run verify

# Live Editor (Unity open, agent compiled)
$env:UCP_EDITOR_INTEGRATION="1"
npm run test:integration:editor
```

## Request flow

```text
ucp-cli → ucp-host (:6610) → ~/.ucp/queues/.../inbox
         → com.air.ucp-agent poll → CliCommand → outbox → ucp-host → ucp-cli
```

No Unity HTTP listener. No `~/.unity-cmd/` profiles.
