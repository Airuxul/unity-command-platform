# unity-connector

[English](README.md)

Unity UPM 包（`unity-connector`）：本机 HTTP 暴露命令目录，供 [unity-cmd](../unity-cmd/) 调用。

## 安装

1. 将本仓库加入 Unity 工程皨`Packages/manifest.json`（本地路径或 Git URL）。
2. 打开工程；Editor 在**6547** 启动 HTTP（可用`UNITY_CMD_PORT` 覆盖）。

## 端点

| `GET /health` →`host` | 端口 | 说明 |
|------------------------|------|------|
| `editor` | 6547 | Editor（编辑与 Play 时均可用亀Editor 域命令） |
| `editor_play` | 6794 | Editor 进入 Play 吨|
| `player` | 6795 | Development Build 玩家 |

同一台机器上三端可同时在线。CLI 用**profile** 区分，见 [../README.zh-CN.md](../README.zh-CN.md)。

## 内置命令（摘要）

| 命令 | 说明 |
|------|------|
| `ping` | 健康检柧|
| `compile` | 异步延迟完成命令 |
| `play` / `stop` | 进出场延迟完成命代|
| `refresh` | 刷新资源；`compile=true` 时走编译延迟完成 |
| `console` | 日志 |
| `exec` | 执行 C# 片段 |
| `profiler` | Profiler 层级筨|
| `screenshot` | 截图 |
| `menu` | 菜单額|
| `manage` | 编辑器控刨|
| `reserialize` | 重序列化资源 |
| `echo` | 运行时回显（Play / player：|

各实例完整列表：[../README.zh-CN.md#各实例命令](../README.zh-CN.md#各实例命代。

## 示例

```bash
unity-cmd --profile editor console --type error,warning --lines 20
unity-cmd --profile editor exec --code "return Application.productName;"
unity-cmd --profile editor play
unity-cmd --profile editor screenshot --view game --output_path Screenshots/play.png
unity-cmd --profile editor stop
unity-cmd --profile editor state
unity-cmd --profile editor help
```

Profiler、截图用 **`editor`** profile；Play 一`echo` 用**`editor-play`**。

## Development Build 包体

构建设置勾逍**Development Build** 后才会编兀`UnityCliConnector.Runtime` 皨player 启动逻辑（player HTTP、`/list`）；**Release 在编译期剔除该启动逻辑**。

CLI：`unity-cmd --profile package-play ping`、`unity-cmd --profile package-play echo`。

**局域网：* Unity 侀`UNITY_CMD_BIND=lan`（或 `UNITY_CMD_LAN=1`）；另一台机器：`unity-cmd profile create <name> --host <局域网IP> --port <端口> --host-kind editor_play`。

## 扩展命令

```csharp
using UnityCliConnector;
using UnityCliConnector.Commands;

public sealed class MyToolParams
{
    [CliParam(Description = "资源路径", Required = true)]
    public string AssetPath { get; set; }
}

public class MyToolCommand : CommandBase, ICommand<MyToolParams>
{
    public CommandDescriptor Descriptor { get; } = new CommandDescriptor<MyToolParams>(
        "my.tool",
        CommandScope.Editor,
        "示例");

    public void Run(MyToolParams p)
    {
        CompleteSuccess(new { path = p.AssetPath });
    }
}
```

延迟完成命令：使用`DeferredCommandDescriptor`，在 `Run(...)` 中先 `MarkRunning()`，后续异步阶段调用`CompleteSuccess/CompleteFail`。同步命令直接在 `Run(...)` 内完成。详解[../docs/MAINTENANCE.md](../docs/MAINTENANCE.md)。

修改 C# 后递增 `ConnectorBuild.Id`，再执行 `unity-cmd --profile editor compile` 一`help`。

无参数命令：`ICommand` + `Run()`。`[CliParam]` 不写 key 时，CLI 参数名为属性camelCase（`ToolName` →`toolName`）。

## EditMode 测试（可选）

```bash
Unity -runTests -batchmode -projectPath <YourProject> -testPlatform editmode \
  -assemblyNames UnityCliConnector.Tests.Editor
```

## 参见

- [docs/IMPLEMENTATION.md](docs/IMPLEMENTATION.md)
- [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)
- [../docs/MAINTENANCE.md](../docs/MAINTENANCE.md)
