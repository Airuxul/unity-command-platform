# com.air.ucp-agent

Unity Command Platform agent — Editor FileQueue polling and native command dispatch (**no HTTP**).

## Install

```json
"com.air.ucp-agent": "file:../CustomPackages/packages/unity-command-platform/com.air.ucp-agent"
```

Open the Unity project — the agent registers at `~/.ucp/sessions/{projectId}.json`.

## Architecture

```text
ucp-cli → ucp-host → FileQueue → EditorAgentBootstrap
                                 → UcpCliCommandHandler
                                 → CliCommand handlers
```

## CLI

```bash
ucp-cli ping
ucp-cli run echo --args '{"message":"hello"}'
ucp-cli run state --project C:/Project/GameDemo
```

Node package: [../ucp-cli](../ucp-cli/) · [CONVENTIONS.md](../CONVENTIONS.md)

## Extending

Add `CliCommand` under `Editor/Commands/` — auto-discovered via `CliCommandDiscovery`.
