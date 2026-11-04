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
- **Command names:** lowercase `{domain}_{operation}`; constants in `CommandNames.cs`.
- **Command catalog:** `ucp-cli run help` (Editor scope) — 82 commands, generated from discovery.
- **Add command:** constant in `CommandNames.cs` + params in `Editor/Params/` + logic in
  `Editor/Services/` + an `EditorActionCommand<T>` shell in `Editor/Commands/`. See
  [MAINTENANCE.md](MAINTENANCE.md).
- **Editing project content:** `gameobject_*` / `component_*` take an optional `prefab` argument —
  with it they edit that prefab asset in place, without it the open scene. Same commands, one
  implementation (`Editor/Services/Objects/EditTargetScope.cs`). For several edits, use
  `prefab_edit` / `scene_edit` with `ops: [{command, args}]`: one save, nothing kept on failure.
- **After C# changes:** recompile in Unity before live CLI tests.
- **State field:** `agent_state` in `run state` response.

## Do not

- Reintroduce `com.air.unity-connector` or Editor HTTP `:6547`.
- Add manual handler registration (use `CliCommandDiscovery` only).
- Maintain a markdown command catalog in this repo — use `CommandNames.cs` + discovery.
- Add multi-action commands that dispatch on an `action` parameter (that is what `manage` was, and
  it was removed in 0.4.0). One operation, one command; use `aliases` for synonyms.
- Duplicate a content operation into a separate `prefab_*` version — extend
  `GameObjectService` / `ComponentService` so both scopes get it.
- Call anything that can raise a modal dialog (unsaved-changes prompts, `EditorUtility.DisplayDialog`)
  — a modal blocks the Editor main thread, which is also the agent's poll loop. Refuse and ask the
  caller for `force` instead, as `SceneService` does.
