# 使用示例

给第一次用这个仓库的人。命令的完整名单不写在这里：装好之后用 `ucp-cli help` 看，它和当前 Editor 里实际能跑的命令是同一份。

相关文档：[README.zh-CN.md](../README.zh-CN.md) 是概览，[COMMAND-DESIGN.zh-CN.md](COMMAND-DESIGN.zh-CN.md) 是复杂命令怎么设计，[MAINTENANCE.md](MAINTENANCE.md) 是往包里加命令的检查清单，[AGENTS.md](AGENTS.md) 是给自动化调用方的约定。

下面的路径按 Windows、PowerShell 来写。JSON 参数用单引号包住，这样 PowerShell 不会吃掉里面的双引号。

## 1. 先分清三个程序

| 名字 | 是什么 | 你要不要自己启动 |
|------|--------|------------------|
| `ucp-cli` | 你敲的命令行 | 要。每条命令都从这里进 |
| `ucp-host` | 本机后台，默认 `127.0.0.1:6610` | 不用。第一条 `ucp-cli` 会自己拉起来 |
| `com.air.ucp-agent` | 装进 Unity 工程的包 | 打开 Unity 并等它编译完 |

Editor 里的命令不走网络：host 把 JSON 写到 `~/.ucp/queues/.../inbox`，Unity 里的 Agent 每个 tick 取走，做完再写回 outbox。Play Mode 里的运行时命令才走本机 HTTP。

一条命令要么发给 **Editor session**（改工程、编译、进 Play），要么发给 **runtime session**（游戏正在跑时的命令）。同一个工程可以同时有这两条，它们互不等待。

## 2. 装上，并确认 Editor 在听

需要 Node 20 或更高。

```powershell
cd ucp-cli
.\setup.ps1
```

这会安装依赖、构建，并把 `ucp-cli`、`ucp-host` 链到全局。如果新终端里仍然找不到 `ucp-cli`，把 `npm prefix -g` 的目录加进 `PATH`，再开一个终端。不想全局链接时，把下面的 `ucp-cli` 换成 `node .\bin\ucp-cli.js`（当前目录是 `ucp-cli`）。

在 Unity 工程的 `Packages/manifest.json` 里加上这个包，保存后等编译结束：

```json
"com.air.ucp-agent": "file:C:/path/to/unity-command-platform/com.air.ucp-agent"
```

然后：

```powershell
ucp-cli ping
ucp-cli host status
```

`ping` 的结果里 `ok` 为 `true` 就可以往下做。`host status` 能看到当前有哪些 session。只有一个 Unity 工程开着时，后面的命令不用写 `--project`。开了多个工程时，每次加上工程路径：

```powershell
ucp-cli ping --project C:/Project/GameDemo
```

`ping` 失败时先看 `error_code`，再看 `hint`：

| `error_code` | 通常是什么情况 |
|--------------|----------------|
| `no_ready_session` | Unity 没开，或 Agent 还没编译完。等 Console 不再编译，再 ping |
| `editor_not_running` | session 文件还在，但那个 Unity 进程已经退出 |
| `unauthorized` | 这台机器上有旧的 `ucp-cli`。CLI 和 Unity 包要一起升级 |

## 3. 怎么读一条结果

每条命令都打印一段 JSON，进程退出码只有在 `ok` 为 `true` 时才是 0。

```powershell
ucp-cli run echo --args '{"message":"hello"}'
```

成功时大致是：

```json
{
  "ok": true,
  "data": { "channel": "editor", "message": "hello" },
  "message": "echo ok",
  "timing": { "queued_ms": 1, "unity_ms": 20, "run_ms": 0 }
}
```

失败时看这三个字段，不要只看退出码：

- `error_code`：分支用的码，例如 `node_not_found`、`invalid_argument`
- `message`：发生了什么
- `hint`：下一步可以试什么

`ucp-cli help <命令>` 的卡片列出 Agent 自己的错误码。host 在命令还没进 Unity 时用的是另一组，名字带 `host_`，例如排队超时是 `host_timeout`，Unity 自己的等待超时仍是 `timeout`。

超时或 `host_lost` 之后，命令可能已经在 Unity 里执行过。再发 `gameobject_create`、`exec` 这种不能重复做的命令之前，先用一条读取命令确认现状。

## 4. 查有哪些命令

```powershell
ucp-cli help
ucp-cli help gameobject_create
ucp-cli help --md
```

不带参数是目录：名字、说明、参数、默认超时、前后要串的命令（`before` / `after`），以及 `completion`。`completion` 有值表示这条命令每次都要等一会儿才结束，值是收尾的 policy 类全名，例如 `compile` 是 `Air.UcpAgent.Completion.CompilationPolicy`。这套设计在 [COMMAND-DESIGN.zh-CN.md](COMMAND-DESIGN.zh-CN.md)。带上命令名是一张卡片，里面有调用示例。`--md` 把卡片打成 Markdown，适合塞进自动化说明。

默认超时写在卡片的 `default_timeout_ms` 上。那里是空的，就是 30 秒。要改某一条的等待时间：

```powershell
ucp-cli run compile --timeout 300000
```

## 5. 日常用法

### 看 Editor 现在什么样

```powershell
ucp-cli run state
ucp-cli run console --args '{"type":"error","limit":20}'
```

`state` 里看 `agent_state`、`play_mode`、`tests_running`。`ping`、`state`、`console`、`help`、`echo` 不用排在编译或测试后面；其它命令要等前一条做完。

### 改当前打开的场景

节点用路径，不用 Transform 的查找。同名兄弟用下标，例如 `Body[1]`。

```powershell
ucp-cli run gameobject_create --args '{"name":"Player"}'
ucp-cli run component_add --args '{"node":"Player","component":"Rigidbody"}'
ucp-cli run gameobject_info --args '{"node":"Player"}'
```

改的是内存里的场景，标成脏。写进磁盘是 `scene_save` 的事。找不到节点时 `error_code` 是 `node_not_found`，`hint` 里会给相近的名字。

### 改磁盘上的 Prefab，不用打开它

`prefab` 表示「这次编辑的目标」。Prefab 资产本身的路径，在 `prefab_create` 这类命令里叫 `path`。要实例化进场景的 Prefab 叫 `source`。

```powershell
ucp-cli run component_add --args '{"prefab":"Assets/P/Player.prefab","node":"Player","component":"Rigidbody"}'
```

### 一次改好几处，失败就全部撤回

```powershell
ucp-cli run group --args '{"steps":[{"command":"gameobject_create","args":{"prefab":"Assets/UI/Page.prefab","parent":"Page","name":"Card"}},{"command":"component_add","args":{"prefab":"Assets/UI/Page.prefab","node":"Page/Card","component":"Image"}}]}'
```

每一步在自己的 `args` 里写 `prefab` 或 `scene`。相邻几步改同一个目标，只保存一次。任何命令都能放进组里，例如 `asset_move`、`prefab_create`、`compile`。默认 `rollback=all`：一步失败，已经跑过的都撤回。`rollback=failed` 只撤失败的那一步，再用 `on_error=stop|continue` 决定后面还跑不跑。哪些命令能撤、怎么撤，见 [COMMAND-DESIGN.zh-CN.md](COMMAND-DESIGN.zh-CN.md) 第 11 节。

想先看结果、不保留：

```powershell
ucp-cli run group --args '{"dry_run":true,"steps":[{"command":"gameobject_rename","args":{"prefab":"Assets/UI/Page.prefab","node":"Page","name":"Page2"}}]}'
```

### 改完脚本再调用新类型

改了 C# 之后分两步。先把文件写到磁盘，再单独跑 `compile`。`exec`、`play`、`test_run`、`build`、`component_add` 都用已经加载的程序集，不会自己去编译。Unity 在后台也不会自己导入改过的脚本。没编译就调用新类型，`component_add` 得到 `type_not_found`。

如果某条命令总要先做另一条、或做完再做一条，作者把命令名写在描述符的 `Before` / `After` 上。host 会按顺序各跑一遍。调用方也可以用 `group` 或 `run-script` 自己排。见 [COMMAND-DESIGN.zh-CN.md](COMMAND-DESIGN.zh-CN.md)。

```powershell
ucp-cli run compile
```

编译失败时 `error_code` 是 `compile_error`，`data.errors` 里是编译器的错误。

### 进 Play Mode，再操作界面

Runtime 不会因为你点了 Play 就自动出现在 host 里。要先登记一台设备，并且游戏正在跑、`/health` 返回成功。端口默认从 6620 起。

```powershell
ucp-cli targets set my-runtime --type runtime --host 127.0.0.1 --port 6620 --project C:/Project/GameDemo

ucp-cli run play --session-type editor
ucp-cli run echo --session-type runtime --args '{"message":"hello from runtime"}'
ucp-cli run ui_snapshot --session-type editor
ucp-cli run ui_click --session-type editor --args '{"node":"Canvas/Login/Go"}'
ucp-cli run stop --session-type editor
```

`ui_snapshot`、`ui_click`、`ui_input`、`ui_wait` 走 Editor session，但要求已经在 Play Mode。节点路径用 `ui_snapshot` 打印出来的那套。没进 Play 时错误码是 `play_mode_required`。

`--session-type` 只能是 `editor` 或 `runtime`。只写了 `--project`、没写类型时，选中的是 Editor。

多步流程可以写成文件，第一条失败就停下：

```json
[
  { "command": "play" },
  { "command": "ui_click", "args": { "node": "Canvas/Login/Go" } },
  { "command": "ui_wait", "args": { "node": "Canvas/Home", "until": "active" }, "timeout": 10000 },
  { "command": "stop" }
]
```

```powershell
ucp-cli run-script flow.json --project C:/Project/GameDemo
```

某一拍要发给 runtime 时，给那一步加上 `"session_type": "runtime"`。

### Play Mode 里的命令面板

把 `com.air.ucp-agent/Resources/UcpCmdPanel.prefab` 拖到场景的 Canvas 下。反引号键显隐。场景里要有 Canvas 和 EventSystem。布局和字号写在预制体上。

输入框里 `Tab` 补全，`Enter` 执行，`↑` / `↓` 翻历史。工程里 scope 为 `CommandHostScope.Runtime` 的命令，编译后面板和 `ucp-cli --session-type runtime` 都能看见。

## 6. 复杂命令怎么设计

当场改一个节点，和要等编译、进 Play、整批撤回，是不同的写法。按「什么时候交出结果」来选的步骤在 [COMMAND-DESIGN.zh-CN.md](COMMAND-DESIGN.zh-CN.md)。第 5 节已经用过的 `compile`、`play`、`group`，按那里的例子敲即可。

## 7. 在自己的工程里加一条命令

不用改这个仓库，也不用登记名单。放一个带 `[UcpCommand]` 的静态类，编译之后 `ucp-cli help` 就能看见。写错了的类不会出现，原因在 `help` 结果的 `load_errors` 里。

运行时命令放在任意运行时脚本里，只在 Play Mode 里出现。Editor 的 `help` 里不会有它，直接对 Editor 调用会得到 `host_unknown_command`。

`Assets/Ucp/UcpProjectRuntimeCommand.cs`：

```csharp
using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;

[UcpCommand]
public static class UcpProjectRuntimeCommand
{
    public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
        "ucp_project_runtime",
        CommandHostScope.Runtime,
        "project runtime probe");

    public static object Execute(CommandContext ctx)
    {
        ctx.Message = "project runtime";
        return new Dictionary<string, object> { ["where"] = "runtime" };
    }
}
```

```powershell
ucp-cli run compile
ucp-cli run play --session-type editor
ucp-cli run ucp_project_runtime --session-type runtime
```

要带参数时把 `Execute` 写成 `Execute(TParams p, CommandContext ctx)`，在参数类的属性上加 `UcpParam`。属性名首字母小写就是 JSON 键；没声明的键会以 `invalid_argument` 失败，`hint` 里列出合法的键。

```csharp
using Air.UcpAgent.Params;

public sealed class AnnounceParams
{
    [UcpParam(Description = "text to show", Required = true)]
    public string Text { get; set; }
}
```

Editor 命令要放在 `Assets/Editor/`（或只在 Editor 平台编译的程序集）里，并把 `CommandHostScope` 写成 `Editor`。这样它不会打进 Player。程序集需要引用 `com.air.UcpAgent.Editor`；包的 asmdef 是自动引用的，普通 `Assets/Editor` 脚本不用再配。

只想跑一个静态方法时不必写命令：在方法上标 `[UcpFunction("名字")]`，用 `ucp-cli run call --args '{"name":"名字","args":{..}}'` 调用。

改场景或 Prefab、要撤回、要等编译或进 Play 才交结果、要插队，按 [COMMAND-DESIGN.zh-CN.md](COMMAND-DESIGN.zh-CN.md) 末尾的五个问题选。工程里要等一会儿才交结果的命令，和它的 `ICompletionPolicy`，都放在 `Assets/Editor/`。

加完之后：

```powershell
ucp-cli run compile
ucp-cli help ucp_project_runtime
```

## 8. 往这个仓库里加命令

和上一节是同一套规则，文件放在包里：

1. 名字写在描述符上，形如 `domain_operation`。只有别的 C# 要写到这个名字时，才在 `Runtime/Commands/CommandNames.cs` 加常量。
2. 有参数就在 `Editor/Params/` 加一个参数类。
3. 逻辑放在 `Editor/Services/`，用 `UcpException` 表示已知失败，用 `ArgumentException` 表示参数不合法。
4. 在 `Editor/Commands/` 加一个带 `[UcpCommand]` 的静态类。命名空间是 `Air.UcpAgent.Editor.Commands`，子目录不改变命名空间。Unity Undo 记不到的改动，再写一个 `Undo`。
5. 等 Unity 编译。`CommandRegistry` 会扫到这个类，没有手工注册表。

怎么选延迟完成、插队和编辑目标，见 [COMMAND-DESIGN.zh-CN.md](COMMAND-DESIGN.zh-CN.md)。这个仓库里的 policy 放在 `Editor/Completion/`。命令上的 `CompletionPolicy` 字段指向那个类。

完整约束、测试和快照要求见 [MAINTENANCE.md](MAINTENANCE.md)。不要在文档里再维护一份命令表。
