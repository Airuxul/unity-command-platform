# unity-connector

[English](README.md)

Unity UPM 包（`unity-connector`）：本机 HTTP 暴露命令目录，供 [unity-cmd](../unity-cmd/) 调用。

## 安装

1. 将本仓库加入 Unity 工程的 `Packages/manifest.json`（本地路径或 Git URL）。
2. 打开工程；Editor 在 **6547** 启动 HTTP（可用 `UNITY_CMD_PORT` 覆盖）。

## 安全

- 默认仅监听本机；`UNITY_CMD_BIND=lan` 仅在可信网络使用。
- 可选令牌：`UNITY_CMD_TOKEN`，请求头 `X-Unity-Cmd-Token`（或 Bearer）。
- 同一 host 并发 `POST /command` 返回 `503`（`SERVER_BUSY`）；域重载期间为 `DOMAIN_RELOADING`。

## 端点

| `GET /health` →`host` | 端口 | 说明 |
|------------------------|------|------|
| `editor` | 6547 | Editor（编辑与 Play 时可用 Editor 域命令） |
| `editor_play` | 6794 | Editor 进入 Play 后的 runtime host |
| `player` | 6795 | Development Build 玩家 |

同一台机器上三端可同时在线。CLI 用**profile** 区分，见 [../README.zh-CN.md](../README.zh-CN.md)。

## 内置命令（摘要）

| 命令 | 说明 |
|------|------|
| `ping` | 健康检查 |
| `compile` | 异步延迟完成命令 |
| `play` / `stop` | 进出 Play 的延迟完成命令 |
| `refresh` | 刷新资源；`compile=true` 时走编译延迟完成 |
| `console` | 日志 |
| `exec` | 执行 C# 片段 |
| `profiler` | Profiler 层级树 |
| `screenshot` | 截图 |
| `menu` | 菜单调用 |
| `manage` | 编辑器控制 |
| `reserialize` | 重序列化资源 |
| `echo` | 运行时回显（Play / player） |

各实例完整列表：[../README.zh-CN.md#各实例命令](../README.zh-CN.md#各实例命令)。

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

Profiler、截图用 **`editor`** profile；Play 期间 `echo` 用 **`editor-play`**。

## Development Build 包体

构建设置勾选 **Development Build** 后才会编译 `UnityCliConnector.Runtime` 的 player 启动逻辑（player HTTP、`/list`）；**Release 在编译期剔除该启动逻辑**。

CLI：`unity-cmd --profile package-play ping`、`unity-cmd --profile package-play echo`。

**局域网：** Unity 侧 `UNITY_CMD_BIND=lan`（或 `UNITY_CMD_LAN=1`）；另一台机器：`unity-cmd profile create <name> --host <局域网IP> --port <端口> --host-kind editor_play`。

## 扩展命令

```csharp
using Air.UnityConnector.Cli;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Params;

public sealed class MyToolParams
{
    [CliParam(Description = "资源路径", Required = true)]
    public string AssetPath { get; set; }
}

public sealed class MyToolCommand : CliCommand<MyToolParams>
{
    public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<MyToolParams>(
        "my.tool", CommandHostScope.Editor, "示例");

    public override void Run(MyToolParams p) =>
        CompleteSuccess(new { path = p.AssetPath });
}
```

延迟完成命令：使用 `DeferredInvokeDescriptor`，在 `Run(...)` 中先 `MarkRunning()`，再 `CompleteSuccess` / `CompleteFail`。详见 [../docs/MAINTENANCE.md](../docs/MAINTENANCE.md)。

修改 C# 后递增 `ConnectorBuild.Id`，再执行 `unity-cmd --profile editor compile` 与 `help`。

无参数命令：继承 `CliCommand` 并实现 `Run()`。`[CliParam]` 不写 key 时，CLI 参数名为属性 camelCase（`ToolName` → `toolName`）。

## EditMode 测试（可选）

```bash
Unity -runTests -batchmode -projectPath <YourProject> -testPlatform editmode \
  -assemblyNames UnityCliConnector.Tests.Editor
```

## 参见

- [docs/IMPLEMENTATION.md](docs/IMPLEMENTATION.md)
- [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)
- [../docs/MAINTENANCE.md](../docs/MAINTENANCE.md)
