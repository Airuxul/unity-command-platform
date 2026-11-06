# com.air.ucp-agent

Unity agent for the Unity Command Platform. Edit Mode polls a file queue and does not listen on HTTP. Play Mode starts a loopback HTTP agent.

## Install

In `Packages/manifest.json`, from a local clone or from git:

```json
"com.air.ucp-agent": "file:C:/path/to/unity-command-platform/com.air.ucp-agent"
"com.air.ucp-agent": "https://github.com/Airuxul/unity-command-platform.git?path=/com.air.ucp-agent"
```

Open the project and wait for compile. The agent writes `~/.ucp/sessions/{projectId}.json`. `test_run` exists only when `com.unity.test-framework` is installed.

How to call it: [../docs/GUIDE.zh-CN.md](../docs/GUIDE.zh-CN.md). Add a command: [../docs/MAINTENANCE.md](../docs/MAINTENANCE.md#add-an-editor-command). Naming: [../CONVENTIONS.md](../CONVENTIONS.md).

A new command is a static class marked `[UcpCommand]`. `CommandRegistry` finds it on the next compile and reports a malformed one under `load_errors` in `help`. There is no hand-written registration list. A static method marked `[UcpFunction("name")]` runs through `call`.
