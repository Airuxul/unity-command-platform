# unity-connector

[简体中文](README.zh-CN.md)

Unity Editor / Player HTTP bridge for [unity-cmd](../unity-cmd/).

**Version:** 1.1.0  
**UPM name:** `com.air.unity-connector`  
**Dependencies:** `com.air.unity-game-core`, `com.unity.nuget.newtonsoft-json`

## Install (local path)

In your Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.air.unity-connector": "file:../CustomPackages/packages/unity-cli/com.air.unity-connector"
  }
}
```

Open the project in the Editor. Default URLs: Editor `http://127.0.0.1:6547/`, Editor Play `6794`, Dev player `6795`.

## Security

| Topic | Behavior |
|-------|----------|
| Bind address | Default loopback (`127.0.0.1`). Set `UNITY_CMD_BIND=lan` only on trusted networks. |
| Auth token | Optional shared secret: Unity `UNITY_CMD_TOKEN` / CLI `UNITY_CMD_TOKEN` env or profile field. Requests must send header `X-Unity-Cmd-Token` (or `Authorization: Bearer …` when enabled). |
| Single-flight | Concurrent `POST /command` on one host returns `503` + `error_code=SERVER_BUSY`. |
| Domain reload | Editor returns `503` + `error_code=DOMAIN_RELOADING`; CLI retries; job status survives via `EditorJobLedger`. |

## Built-in commands

| Command | Scope | Deferred | Aliases |
|---------|-------|----------|---------|
| `ping` | any | no | —|
| `state` | editor | no | —|
| `echo` | editor / runtime | no | —(same name; host picks handler) |
| `compile` | editor | yes | `recompile`, `reload` |
| `play` | editor | yes | —|
| `stop` | editor | yes | —|
| `refresh` | editor | yes if `compile=true` | —|
| `console` | editor | no | —|
| `menu` | editor | no | —|
| `screenshot` | editor | no | —|
| `exec` | editor | no | —|
| `profiler` | editor | no | —|
| `manage` | editor | no | —|
| `reserialize` | editor | no | —|

**Routing:** Editor-scoped commands (`compile`, `state`, `profiler`, `screenshot`, — use profile **`editor`** (:6547), including while Play Mode is active. Runtime `echo` uses **`editor-play`** (:6794) or **`package-play`** (:6795).

Run `unity-cmd --profile editor help` to see per-command parameter lines from the live catalog.

## Runtime / Play-mode

| HTTP `host` | Port | When |
|-------------|------|------|
| `editor` | 6547 | Editor project open |
| `editor_play` | 6794 | Editor Play Mode |
| `player` | 6795 | Development Build player |

**Implementation:** `PlayConnectorServer` + `ConnectorMainThreadScheduler` (`Runtime` assembly), `EditorConnectorServer` (Editor), `EditorPlayHttpBootstrap`, `DevPlayerBootstrap` (`DEVELOPMENT_BUILD`).

| Command scope | `editor` :6547 | `editor_play` :6794 | `player` :6795 |
|---------------|----------------|---------------------|----------------|
| `editor` | Yes | No | No |
| `runtime` | No | Yes | Yes |
| `any` | Yes | Yes | Yes (if shipped) |

```bash
unity-cmd --profile editor-play ping
unity-cmd --profile editor-play echo
unity-cmd --profile editor profiler --action status
unity-cmd --profile editor screenshot --view game --output_path Screenshots/play.png
```

**All instances:** [../README.md#commands-per-instance](../README.md#commands-per-instance).

Full stack: [docs/IMPLEMENTATION.md](docs/IMPLEMENTATION.md#runtime--play-mode-stack).

## Examples

```bash
unity-cmd --profile editor console --type error,warning --lines 20
unity-cmd --profile editor exec --code "return Application.productName;"
unity-cmd --profile editor profiler --action enable
unity-cmd --profile editor play
unity-cmd --profile editor screenshot --view game --output_path Screenshots/play.png
unity-cmd --profile editor stop
unity-cmd --profile editor state
unity-cmd --profile editor help
```

Profiler and screenshot use profile **`editor`**. In Play, use **`editor-play`** for `echo`.

## Development Build player

Enable **Development Build** in Build Settings. The `UnityCliConnector.Runtime` assembly includes `DEVELOPMENT_BUILD` constraint for player startup; Release players exclude runtime bootstrap code at compile time.

CLI: `unity-cmd --profile package-play ping` / `unity-cmd --profile package-play echo`.

## Extend a command

```csharp
using Air.UnityConnector.Cli;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Params;

public sealed class MyToolParams
{
    [CliParam(Description = "asset path", Required = true)]
    public string AssetPath { get; set; }
}

public sealed class MyToolCommand : CliCommand<MyToolParams>
{
    public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<MyToolParams>(
        "my.tool", CommandHostScope.Editor, "My example tool");

    public override void Run(MyToolParams p) =>
        CompleteSuccess(new { path = p.AssetPath });
}
```

For deferred commands, use `DeferredInvokeDescriptor` and call `MarkRunning()` then `CompleteSuccess` / `CompleteFail` later. See [docs/MAINTENANCE.md](../docs/MAINTENANCE.md).

Bump `ConnectorBuild.Id`, then `unity-cmd --profile editor compile` and `help`.

## EditMode tests (optional)

```bash
Unity -runTests -batchmode -projectPath <YourProject> -testPlatform editmode \
  -assemblyNames UnityCliConnector.Tests.Editor
```

## See also

- [docs/IMPLEMENTATION.md](docs/IMPLEMENTATION.md)
- [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)
- [../unity-cmd/README.md](../unity-cmd/README.md)
