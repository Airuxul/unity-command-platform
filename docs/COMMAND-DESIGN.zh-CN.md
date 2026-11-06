# 复杂命令怎么设计

给要写命令、但被 job、policy、域重载、命令组撤回、`RunsWhileBusy` 绕住的人。

日常怎么敲 CLI，看 [GUIDE.zh-CN.md](GUIDE.zh-CN.md)。文件怎么投递、超时后留不留结果，看 [RELIABILITY.zh-CN.md](RELIABILITY.zh-CN.md)。往这个仓库里加文件、跑测试，看 [MAINTENANCE.md](MAINTENANCE.md)。这里只讲写一条命令时要做的选择。

命令名单以 `ucp-cli help` 为准，本文不列参数。

## 1. 调用方只等一条结果

敲一条命令，就等一条 JSON。没有第二条命令用来查进度。`run-script` 也会等这一步结束，再跑下一步。

后面的机制都是为了让这一条结果在对的时间交出去。

## 2. 结果有两种交法

先只回答一个问题：**`Execute` 返回的那一刻，调用方要的答案是不是已经有了？**

有了，就当场交。还没有，就先开工，过一会儿再交。

### 当场交

`gameobject_create` 在 `Execute` 里把节点建好，返回值就是结果。CLI 马上拿到 JSON。

```text
CLI 提交
  → Execute 做完
  → 这条 JSON 回到 CLI
```

### 过一会儿再交

`compile` 在 `Execute` 里只是请求编译。编译结束、域重载完成之后，成功或失败才知道。CLI 会一直等到那时候。

```text
CLI 提交
  → Execute 只开工，先不回结果
  → （Unity 可能重载域，内存被清空）
  → 之后每个 Editor tick 看一眼：好了没有
  → 好了，这条 JSON 才回到 CLI
```

要等的事情包括：编译结束、进入或退出 Play、测试跑完、包解析完、界面上的条件成立。`Execute` 返回时这些都还没发生。

## 3. 用 `compile` 把词对上

下面四个词都来自这一条链路。先看步骤，再看名字。

1. CLI 提交 `compile`。
2. Unity 收到后，先把一张「还没做完的单」写下来。单上记着两件事：等的是哪个 policy 类，以及最晚交卷时间。
3. `Execute` 请求编译，然后返回。这时调用方还没有结果。
4. Unity 可能重载域，静态字段被清空。单还在，所以重载之后知道 `compile` 还没交卷，**不会把 `Execute` 再跑一遍**。
5. 之后每个 Editor tick，`CompilationPolicy` 看一眼。还在编译就继续等。编译结束就写出成功，或 `compile_error`。
6. 这条 JSON 回到 CLI。

| 词 | 在第几步 | 是什么 |
|----|----------|--------|
| job | 2 | 那张单。一份在 `SessionState`，一份在 `~/.ucp/jobs/{projectId}/{命令 id}.json`，用来活过域重载 |
| `CompletionPolicy` | 2 | 命令上的字段，值是收尾的那个类，例如 `typeof(CompilationPolicy)`。job 文件存的是这个类的全名 |
| `Execute` | 3 | 只负责开工 |
| `ICompletionPolicy` | 5 | 字段指向的类。每个 tick 用 job 上的类名找到它 |

`ucp-cli help compile` 里的 `completion` 是 policy 类的全名，例如 `Air.UcpAgent.Completion.CompilationPolicy`。有值，表示这条命令**每次**都走「过一会儿再交」。`--md` 里写成 `deferred until` 加上这个类名。

类对象本身写不进 job 文件。域重载会清掉内存，重新加载后再按这个全名找到同一个类。

现有命令里，每次都等的是这些：

| 命令 | `completion` | 在等什么 |
|------|----------------|----------|
| `compile` | `CompilationPolicy` | 编译结束 |
| `play` | `EnterPlayModePolicy` | 已经进入 Play |
| `stop` | `ExitPlayModePolicy` | 已经退出 Play |
| `test_run` | `TestRunPolicy` | 测试跑完 |
| `package_add`、`package_remove` | `PackagePolicy` | 包解析完 |

## 4. 两种写法

### 每次都要等

命令上有一个字段，指向 policy 类。描述符用 `DeferredInvokeDescriptor`，第 4 个参数就是这个字段。job 在 `Execute` **之前**就建好，上面写的是这个类的全名。所以 `Execute` 刚返回就发生域重载，也不会把命令再跑一遍。`Execute` 返回 null。

`play` 的字段是 `typeof(EnterPlayModePolicy)`。`Execute` 里调用 `EnterPlaymode()`，然后返回。真正「已经在 Play 里」由这个类在之后的 tick 里确认。

### 有时 `Execute` 里已经有答案

描述符仍用普通的 `InvokeDescriptor`。答案已经有了就直接返回。还没有，再调用 `ctx.Defer(typeof(那个 Policy), json)`，把还要等的条件放进 job。

- `refresh`：参数 `compile` 为 false 时当场交；为 true 时 `Defer(typeof(CompilationPolicy))`，后面和 `compile` 一样等。
- `ui_wait`：节点已经是要等的样子，当场交。否则 `Defer(typeof(UiWaitPolicy), ...)`，这个类每个 tick 再看，条件成立就交卷。

这两种的 `help` 卡片上 `completion` 是空的，因为不是每次都等。

描述符上已经有 policy 类时，`Defer` 要传同一个类。`package_add` 用它自己的 `CompletionPolicy` 字段把「加哪个包」写进 job。传了另一个类，这条命令失败。

## 5. policy 看什么、回什么

一个具体类，实现 `ICompletionPolicy`，有无参构造。Editor 启动时把所有已加载程序集里的这种类扫出来。

每个 Editor tick 调一次 `TryComplete`，域重载之后也调。

- 还没好：返回 `false`。CLI 继续等。
- 好了：返回 `true`，载荷放进 `result`。
- 失败了：返回 `true`，原因放进 `failure`（里面是 `error_code`、`message`、`hint`）。

调用方看到的 `message` 默认是 `ok`。要换成别的句子，`result` 用 `PolicyOutcome`：`Message` 是那句话，`Data` 才是 JSON 里的 `data`。

还要等的条件放在 `ctx.Defer` 的第二个参数，是一段 JSON。policy 从 job 的 `CompletionArgs` 读。域重载会清掉静态字段，进度写在静态字段上会丢。

同一个 policy 类可以被多条命令共用，例如 `package_add` 和 `package_remove` 都指向 `PackagePolicy`。一条「每次都等」的命令指向的类在 Editor 启动时还不存在，启动直接抛异常。

域重载之后，job 上的类名对不上任何已加载的 policy，这张单立即失败，`error_code` 是 `orphaned`。回调要在重载前把结果交给 `EditorJobStateManager.Succeed` / `Fail`，或者把还要看的状态写进 job，由 policy 读。policy 自己从 job 恢复，不靠还活着的那次域。

## 6. 等到什么时候算超时

时间从 CLI **提交**那一刻开始算，排队的时间也算。描述符上的 `default_timeout_ms` 是默认上限，`ucp-cli help` 里能看到；空着就是 30 秒。调用方用 `--timeout` 覆盖。

到点之后看是谁先说话：

- 到了 deadline，Unity 把 job 删掉，不再写结果。CLI 收到的是 `host_timeout`。host 按同一个 deadline 计时，这时它已经回复过了。
- `ui_wait` 会在 deadline 之前自己停。提前量是 1.5 秒和本次超时的一半里较短的那个。这时 CLI 收到的是 `timeout`，`message` 里有节点当时的样子。

## 7. runtime 里写不了「过一会儿再交」

Play Mode 的 runtime 没有 job。描述符用了 `DeferredInvokeDescriptor` 时，`Execute` 之前就失败，`error_code` 是 `command_failed`。运行时命令在 `Execute` 里把结果返回。

工程里的例子放在 `Assets/Editor/`。

## 8. 一份对得上的例子

下面这条每次都等。`CompletionPolicy` 就是 `EditorIdlePolicy` 这个类。描述符把这个字段交给 `DeferredInvokeDescriptor`，job 上写的是 `EditorIdlePolicy` 的全名。每个 tick 用这个名字找到该类。`ReadyForTools` 还是 false（正在编译或刷新资源）时，`TryComplete` 返回 false，CLI 继续等。变成 true 才交卷，`message` 是 `editor idle`。

```csharp
using System;
using System.Collections.Generic;
using Air.UcpAgent;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;

[UcpCommand]
public static class WaitUntilReadyCommand
{
    public static readonly Type CompletionPolicy = typeof(EditorIdlePolicy);

    public static readonly InvokeDescriptor Descriptor = new DeferredInvokeDescriptor(
        "wait_until_ready",
        CommandHostScope.Editor,
        "wait until the editor is idle",
        CompletionPolicy,
        defaultTimeoutMs: 60_000);

    public static object Execute(CommandContext ctx) => null;
}

public sealed class EditorIdlePolicy : ICompletionPolicy
{
    public bool TryComplete(
        InvokeJobRecord command,
        EditorStateSnapshot state,
        out object result,
        out CommandFailure failure)
    {
        result = null;
        failure = null;
        if (!state.ReadyForTools)
            return false;

        result = new PolicyOutcome
        {
            Message = "editor idle",
            Data = new Dictionary<string, object> { ["ready"] = true },
        };
        return true;
    }
}
```

编译之后 `ucp-cli help wait_until_ready` 的 `completion` 是 `EditorIdlePolicy`。这个类没有命名空间，全名就是类名。

## 9. 排队，以及改场景

交结果的时间由上面决定。下面两件事跟交卷无关：Editor 忙的时候还能不能回答，以及改的是不是场景或 Prefab。

### 忙的时候仍可回答：`RunsWhileBusy`

同一条 Editor 上，命令默认排成一队。前面的编译或测试还在跑时，后面的等着。

`RunsWhileBusy = true` 的命令不进这队，测试进行时也能跑。现在是 `ping`、`state`、`console`、`help`、`echo`。session 文件里的名单叫 `runsWhileBusy`。

这种命令在 `Execute` 里就把结果交出来。延迟完成的命令留在队列里，等到 policy 交卷。

描述符上的 `ReadsTargetOnly = true` 只表示打开目标来读、不记撤销。它不改变排队。

在编译、测试、进出 Play 的半截里跑它，也不会改坏工程或正在跑的游戏时，才设 `RunsWhileBusy`。默认不设。

域重载的那几秒里 Unity 不 tick，插队的命令也要等重载结束。

### 改场景或 Prefab：编辑目标

`gameobject_*` 和 `component_*` 是同一套实现。

| 参数 | 打开的目标 |
|------|------------|
| 带 `prefab` | 那个 Prefab 资产 |
| 带 `scene` | 那个已加载的场景 |
| 两个都不带 | 正开着 Prefab Mode 时是那个 stage，否则是当前场景 |

参数类继承 `EditTargetParams`，`Execute` 的第二个参数是 `EditTargetContext` 或它的子类。`EditorExecutionHost` 先打开目标，放进 `ctx.Scope`，命令在里面改。命令自己不加载 Prefab，也不自己保存。

`Execute` 正常返回：提交。场景标脏，Prefab 写回资产。`Execute` 抛异常，或调用了 `ctx.Fail`：这一条撤回。

`ReadsTargetOnly = true`：打开来读，不开撤销组，也不保存。

带 `prefab`、多条命令和 `dry_run` 的敲法在 [GUIDE.zh-CN.md](GUIDE.zh-CN.md) 第 5 节。

## 10. 命令怎么写：静态类、参数、Context

命令是带 `[UcpCommand]` 的静态类。`CommandRegistry` 扫描已加载的程序集，按名字读三个成员：

| 成员 | 写法 |
|------|------|
| 描述符 | `public static readonly InvokeDescriptor Descriptor` |
| 执行 | `public static object Execute(TParams p, TContext ctx)`；没有参数的命令写 `Execute(TContext ctx)` |
| 撤回（可选） | `public static void Undo(TParams p, TContext ctx)`，参数和 `Execute` 一样 |

- **命令不存状态。** 静态字段不是 readonly、静态属性带 setter，都会被拒绝。
- **Params 是这一次的输入**，由 `UcpParamBinder` 按 `[UcpParam]` 绑定。
- **Context 是这一次的记录。** id、`Message`、结果、失败、延迟都在 `CommandContext` 上。`Undo` 要用、参数里又没有的东西（新建了哪些文件夹、备份放在哪），命令就声明一个 `CommandContext` 的子类，用有类型的字段记下来。
- **Context 子类的字段必须能写成 JSON。** 命令组会把它写进磁盘，重载之后照着它撤回。活对象（例如 `EditTargetContext.Scope`）标 `[JsonIgnore]`，`Undo` 里不能依赖它。

签名写错时编译器不报错。`CommandRegistry` 加载时检查，有问题的类会被跳过，原因记进 `help` 返回的 `load_errors`，同时在 Console 打一条 Error。集成测试要求 `load_errors` 为空。加载时能查出的问题：

- 不是静态类；
- 缺 `Descriptor`；
- `Execute` 的签名或返回值不对；
- `Undo` 的参数和 `Execute` 对不上；
- 编辑目标命令没有用 `EditTargetContext`；
- 编辑目标命令声明成延迟完成；
- Context 的字段不能序列化。

### 静态函数：`call`

只想让调用方跑一个静态方法时，不必写命令。在方法上标 `[UcpFunction("名字")]`，`call` 就能按名字调到它：

```csharp
public static class SaveTools
{
    [UcpFunction("reset_save", Description = "Delete the local save", Scope = CommandHostScope.Editor)]
    public static object ResetSave(string slot, bool keepSettings = false) { ... }
}
```

```bash
ucp-cli run call --args '{"name":"reset_save","args":{"slot":"a"}}'
ucp-cli run call    # 不带 name：列出这个 host 上的函数和参数
```

- 参数按形参名从 `args` 绑定，不区分大小写。有默认值的形参可以不传。
- 类型是 `CommandContext` 的形参会收到这一次的 Context。
- 返回值就是结果，void 返回 `ok`。
- 参数错误和 `ArgumentException` 返回 `invalid_argument`，其他异常返回 `command_failed`，名字找不到返回 `unknown_command`。
- `Scope` 默认 `Any`。Play Mode 里 runtime agent 跑在 Editor 进程里，也看得到 Editor 程序集，所以只在 Editor 里用的函数要写 `Scope = CommandHostScope.Editor`。
- 同名的两个函数都不会列出，原因进 `load_errors`。
- 函数目前没有 Undo，也不能延迟完成。放进命令组时回退跳过它。

## 11. 撤回，以及命令组

### 一条命令怎么撤回

撤回一步时依次做两件事：

1. 这一步的 Unity 撤销组用 `RevertAllDownToGroup` 倒回。编辑目标命令的改动都经 `EditTargetScope` 登记到 Unity Undo；`PrefabUtility` 传 `UserAction` 时，Unity 也把自己的记录写进同一组。
2. 命令写了 `Undo` 的，再调用它，撤 Unity Undo 记不到的部分。

能用 Unity Undo 撤回的命令就不写 `Undo`。逐条评估的结果：

| 归类 | 命令 | 怎么撤 |
|------|------|--------|
| Unity Undo 覆盖 | 场景和 stage 里的 `gameobject_*`、`component_*`、`prefab_instantiate` / `apply` / `revert` / `unpack` / `replace` | 不写 `Undo` |
| 编辑目标里 Unity Undo 覆盖不到的部分 | 同上，但目标是离屏 Prefab 资产，或 stage 已经保存到文件 | 执行器统一处理：命令组第一次打开这个 Prefab 时备份文件，撤回时写回再 `ImportAsset` |
| 写了 `Undo` | `prefab_create` | 删掉新建的 `.prefab`；场景里的连接由 Unity Undo 撤 |
| 写了 `Undo` | `asset_create_folder` | 从深到浅删掉这次新建的文件夹 |
| 写了 `Undo` | `asset_move`、`prefab_move` | `MoveAsset` 移回，GUID 不变；再删掉为目标新建的文件夹 |
| 写了 `Undo` | `asset_duplicate`、`prefab_duplicate`、`prefab_create_empty`、`prefab_create_variant` | 删掉新文件和新建的文件夹 |
| 写了 `Undo` | `asset_delete`、`prefab_delete` | 在命令组里，删之前把文件和 `.meta`（文件夹整个）拷到组的目录；撤回时拷回再导入，GUID 不变 |
| 暂不写 | `tag/layer_add/remove`、`scene_new/open/close/save/save_as`、`selection_set`、`tool_set`、`pause` | 改的是工程设置或编辑器状态，组合编辑里很少需要撤 |
| 撤不了 | `build`、`exec`、`menu`、`package_*`、`compile`、`refresh`、`reserialize`、`test_run`、`play`/`stop`、`screenshot`、`profiler`、`ui_*`、`call` | 回退时跳过，结果里 `undone` 为 false |

`Undo` 只照 Context 里记下的内容撤：中途失败的命令只撤它做完的部分，没做的跳过。

单条命令失败时，执行器立即撤回这一条，所以失败的命令不留半截改动。成功之后不再撤回。

Play Mode 不记撤销。Play Mode 里改的场景撤不回，撤回时报 `edit_mode_required`。

### 命令组 `group`

```bash
ucp-cli run group --args '{"steps":[{"command":"asset_create_folder","args":{"path":"Assets/UI/New"}},{"command":"prefab_create_empty","args":{"path":"Assets/UI/New/Card.prefab"}}],"rollback":"all"}'
```

| 参数 | 含义 |
|------|------|
| `steps` | 按顺序跑的命令，每条 `{command, args}`；编辑目标的步骤可以加 `expect` |
| `rollback` | `all`（默认）：任何一步失败，撤回所有跑过的步骤。`failed`：只撤失败的那一步，之前成功的保留 |
| `on_error` | 只配 `rollback=failed`：`stop`（默认）后面不再跑；`continue` 接着跑。`rollback=all` 配 `continue` 会被拒绝 |
| `dry_run` | 每一步都真跑、结果照常返回，最后全部撤回 |

- **先查后跑。** 所有步骤先查表、绑参数、解析 `expect`。有未知命令、参数错误、嵌套 `group`，或超过 100 步时，一步都不跑，直接返回 `invalid_argument`。
- **每一步都走执行器。** 和单独发这条命令一样，`ctx.Fail`、`ctx.Message`、延迟完成都照常工作。
- **同一个目标只保存一次。** `rollback=all` 时，相邻几步改同一个 `prefab` / `scene`，共用一次打开和一次保存。每一步仍有自己的撤销组，可以逐条撤。`rollback=failed` 时每一步各自保存。
- **成功之后**，各步的撤销组合并成一个。之后在 Editor 里按一次 Ctrl+Z，撤掉的是整个组里 Unity 记录到的部分。
- **结果。** `data.steps` 里每一步一条 `{index, command, ok, message, error_code, data, undone}`。`data.rollback` 是 `{mode, ran, ok, errors}`，另外还有 `failed_at` 和 `not_run`。
  - 只要有一步失败，整个组就失败，错误码取第一个失败步骤的。
  - 撤回本身也出错时，错误码是 `rollback_failed`。`rollback.errors` 列出哪几步没撤回，其余步骤照常撤完。

### 跨域重载

步骤里可以有 `compile`、`play`、`test_run` 这类延迟完成的命令。

- 组的进度和回退记录写在 `Library/ucp/undo/<组 id>/`：
  - `progress.json`：步骤、走到第几步、每一步的结果；
  - `journal.json`：每个要撤的步骤的命令名、参数和 Context 的 JSON；
  - Prefab 和被删资产的备份。
- 写盘顺序固定：先跑完一步，再写 journal，再推进 progress。域重载只发生在两帧之间，打断不了这三步。
- 某一步延迟完成时，组自己也转成延迟命令，交给 `GroupPolicy`。这一步的 job 记着组的 id（`ParentId`），结果不进 outbox，由组读取。`GroupPolicy` 每帧检查这个 job，完成后记下结果，接着跑后面的步骤，直到下一个延迟步骤或全部结束。
- 重载之后，按命令名从 Registry 找回命令，用存下的参数重新绑定，Context 用无参构造新建再填回 JSON，然后照常撤回。
- deadline 覆盖整个组。到点时，同步跑着的组在下一步之前停下，按 `rollback` 撤回；等着延迟步骤的组由 `GroupPolicy.Expired` 撤回。
- 组结束（成功、失败或撤回完）后删掉它的目录。Editor 启动时如果发现某个组目录没有对应的 job，说明 Unity 在组跑到一半时退出了。这时打一条 Warning，列出目录，不自动撤回，交给人处理。
- 实测（Unity 2022.3.56，`group-persistence.test.ts`）：Unity 的撤销组编号跨过 `compile` 的域重载，也跨过进出 Play Mode，仍然有效。重载或 Play 之前建的节点，之后能撤掉；整个组成功后按一次 Ctrl+Z 也能一次撤掉。

### 三种把命令串起来的办法

| | 谁来写 | 能撤回 | 跨域重载 | 派发 |
|---|---|---|---|---|
| `group` | 调用方 | 能 | 能 | 一次 |
| 描述符的 `Before` / `After` | 命令作者，固定的前后置命令 | 不能 | 能 | host 每步各派一次 |
| `run-script` | 调用方，步骤写在文件里，可以跨 editor / runtime | 不能 | 能 | CLI 每步各派一次 |

## 12. 用别的命令串起来

一条命令可以点名在它之前、之后各跑哪些命令。名字写在描述符上：

```csharp
public static readonly InvokeDescriptor Descriptor = new DeferredInvokeDescriptor<PlayParams>(
    CommandNames.Play,
    CommandHostScope.Editor,
    "Enter Play Mode (deferred)",
    CompletionPolicy,
    defaultTimeoutMs: 120_000)
{
    Before = new[] { CommandNames.Compile },
};
```

host 收到 `play` 之后，先把 `compile` 当作一条独立命令跑完，成功了再跑 `play`。`play` 的参数不会传给 `compile`。前面任何一条失败，后面的不再跑，调用方看到的是失败那条的 `error_code`。`play` 成功之后如果还有 `After`，再按同样的方式跑。

每一步都是一次真正的派发。`compile` 触发的域重载结束之后，链上的下一条才开始。所以这条链能活过重载。

命令自己的 `Before` / `After` 会展开进去。链上出现循环，这条命令失败。

内置命令默认不串任何命令。改完脚本要先编译，调用方自己先跑 `compile`，或者由某条命令的作者把它写进 `Before`。也可以不写在命令上，用 `group` 或 `run-script` 把步骤列出来。

`ucp-cli help` 的卡片上能看到 `before` 和 `after`。

## 13. 写之前按这个顺序选

1. **`Execute` 返回时，答案已经有了吗？**
   - 有了：普通 `InvokeDescriptor`，返回那个对象。
   - 没有，而且每次都没有：写一个 `ICompletionPolicy`，命令上放 `public static readonly Type CompletionPolicy = typeof(那个类)`，描述符把这个字段传进去。
   - 有时有、有时要等：普通描述符；要等的分支调用 `ctx.Defer(typeof(那个类))`。
2. **改的是场景或 Prefab 里的节点、组件吗？** 参数继承 `EditTargetParams`，`Execute` 接 `EditTargetContext`，修改只通过 `ctx.Scope`。只读的设 `ReadsTargetOnly = true`。
3. **会改东西，而且 Unity Undo 记不到吗？** 写 `Undo`。参数里没有、撤回又要用的东西，放在自定义 Context 的字段里（见第 10、11 节）。
4. **要在编译或测试还没结束时插进去回答吗？** `Execute` 里就能答完，并且不会改坏工程或正在跑的游戏：`RunsWhileBusy = true`。默认跳过这一问。
5. **跑它之前或之后还要跑别的命令吗？** 把那些命令的名字写在描述符的 `Before` / `After` 上。调用方也可以用 `group` 或 `run-script` 自己排，不必写在命令上。

运行时命令只做第 1 问里的「有了」那一档。
