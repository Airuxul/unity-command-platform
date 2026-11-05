# com.air.ucp-agent

Unity Command Platform agent — Editor FileQueue polling and native command dispatch (**no HTTP** in
Edit Mode), plus a loopback HTTP agent while in Play Mode.

## Install

`Packages/manifest.json`, from a local clone or from git:

```json
"com.air.ucp-agent": "file:C:/path/to/unity-command-platform/com.air.ucp-agent"
"com.air.ucp-agent": "https://github.com/Airuxul/unity-command-platform.git?path=/com.air.ucp-agent"
```

Open the Unity project — the agent registers at `~/.ucp/sessions/{projectId}.json`. `test_run`
appears only when `com.unity.test-framework` is installed.

## Architecture

```text
ucp-cli → ucp-host → FileQueue → EditorAgentBootstrap
                                 → CommandExecutor → CommandRegistry
                                 → CliCommand.Run (deferred: job + completion policy)
```

## CLI

```bash
ucp-cli ping
ucp-cli help
ucp-cli run echo --args '{"message":"hello"}'
ucp-cli run state --project C:/Project/GameDemo
```

Node package: [../ucp-cli](../ucp-cli/) · [CONVENTIONS.md](../CONVENTIONS.md)

## Extending

Add a `CliCommand` under `Editor/Commands/` — `CommandRegistry` finds it on the next compile. See
[../docs/MAINTENANCE.md](../docs/MAINTENANCE.md#add-an-editor-command).
