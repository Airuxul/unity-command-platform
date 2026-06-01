# unity-connector

[简体中文](README.zh-CN.md)

Unity Editor / Player HTTP bridge for [unity-cmd](../unity-cmd/).

**Version:** 0.1.7  
**UPM name:** `unity-connector`  
**Dependency:** `com.unity.nuget.newtonsoft-json` (parameter binding)

## Install (local path)

In your Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "unity-connector": "file:../CustomPackages/packages/unity-cli/unity-connector"
  }
}
```

Open the project in the Editor. Default URLs: Editor `http://127.0.0.1:6547/`, Editor Play `6794`, Dev player `6795`.

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

**Implementation:** `PlayModeHttpEndpoint` + `PlayModeCommandBridge` (`Runtime` assembly), `EditorPlayHttpBootstrap` (Editor), `DevPlayerBootstrap` (`Runtime` assembly with `DEVELOPMENT_BUILD` constraint).

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
using UnityCliConnector;
using UnityCliConnector.Commands;

public sealed class MyToolParams
{
    [CliParam(Description = "asset path", Required = true)]
    public string AssetPath { get; set; }
}

public class MyToolCommand : CommandBase, ICommand<MyToolParams>
{
    public CommandDescriptor Descriptor { get; } = new CommandDescriptor<MyToolParams>(
        "my.tool",
        CommandScope.Editor,
        "My example tool");

    public void Run(MyToolParams p)
    {
        CompleteSuccess(new { path = p.AssetPath });
    }
}
```

For deferred commands, use `DeferredCommandDescriptor` and call `MarkRunning()` then `CompleteSuccess/CompleteFail` later. For immediate commands, call `CompleteSuccess/CompleteFail` inside `Run(...)`. See [docs/MAINTENANCE.md](../docs/MAINTENANCE.md).

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
