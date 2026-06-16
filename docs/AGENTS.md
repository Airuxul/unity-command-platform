# AGENTS — `packages/ucp`

**Last Updated:** 2026-06-16 · **Scope:** agent entry for this Git submodule (English)

## Canonical rule

This file is the agent entrypoint for **this** repository (`ucp-cli` + `com.air.ucp-agent`). On conflict, this file wins over other docs in this repo.

## Package scope

| Property | Value |
|----------|--------|
| **Repo path** | `CustomPackages/packages/ucp` |
| **Node** | `ucp-cli/` — bins `ucp-cli`, `ucp-host` |
| **Unity** | `com.air.ucp-agent/` — FileQueue agent, **no HTTP** |
| **Data** | `~/.ucp/` |

## User documentation

| File | Language |
|------|----------|
| [README.md](../README.md) | English |
| [README.zh-CN.md](../README.zh-CN.md) | Chinese |
| [TODO.zh-CN.md](../TODO.zh-CN.md) | Chinese backlog |
| [docs/TODO.md](TODO.md) | English backlog |

## Agent documentation

| File | Purpose |
|------|---------|
| [AGENTS.md](AGENTS.md) | This file |
| [DOC_GOVERNANCE.md](DOC_GOVERNANCE.md) | Doc workflow |
| [CHANGELOG_AGENT.md](CHANGELOG_AGENT.md) | Agent changelog |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Stack and flows |
| [MAINTENANCE.md](MAINTENANCE.md) | Extend commands, tests |
| [RELIABILITY.zh-CN.md](RELIABILITY.zh-CN.md) | FileQueue reliability |
| [../CONVENTIONS.md](../CONVENTIONS.md) | Naming |

## Running commands

```powershell
cd ucp-cli
.\ucp-cli.cmd ping
.\ucp-cli.cmd run <type> [--args '{"key":"value"}']
.\ucp-cli.cmd host status
```

- **Project selection:** `--project <path>` or open Unity with agent installed.
- **Host port:** `UCP_HOST_PORT` (default `6610`).
- **Integration tests:** `UCP_EDITOR_INTEGRATION=1` + Unity Editor open.

## Quick facts

- **No profiles**, no `POST /list`, no `~/.unity-cmd/`.
- **Command names:** lowercase `type` field; constants in `CommandNames.cs`.
- **Add command:** one `CliCommand` class under `Editor/Commands/` + constant in `CommandNames.cs`.
- **After C# changes:** recompile in Unity before live CLI tests.
- **State field:** `agent_state` in `run state` response.

## Do not

- Reintroduce `com.air.unity-connector` or Editor HTTP `:6547`.
- Add manual handler registration (use `CliCommandDiscovery` only).
- Maintain a markdown command catalog in this repo — use `CommandNames.cs` + discovery.
