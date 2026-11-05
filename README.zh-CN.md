# UCP（Unity Command Platform）

[English](README.md)

**Unity 命令平台** — 通过命令行、CI 脚本或 AI Agent 自动化操控 Unity Editor 和 Runtime，无需在 Unity 侧开启任何 HTTP 服务器。

| 包 | 职责 |
|----|------|
| [ucp-cli](ucp-cli/) | Node：`ucp-cli` 命令行 + `ucp-host` 守护进程（同一 npm 包） |
| [com.air.ucp-agent](com.air.ucp-agent/) | Unity UPM：Editor FileQueue Agent + 命令实现 |

命名：[CONVENTIONS.md](CONVENTIONS.md) · 文档：[docs/](docs/)

---

## 为什么选择 UCP？

| 痛点 | UCP 的解法 |
|------|-----------|
| Unity Editor 没有可脚本化的 CLI | `com.air.ucp-agent` 在 Editor 中安装一个轻量级轮询 inbox 的 Agent，无需额外进程，无需开放端口 |
| CI/CD 需要无头驱动 Unity | `ucp-cli` 是标准 npm 二进制，可在任何 Shell、流水线或 Agent 循环中使用 |
| HTTP 监听器在防火墙或容器环境中受阻 | Editor 传输使用本地文件（`~/.ucp/queues/`）——无需任何网络连接 |
| Play Mode Runtime 需要独立通道 | Runtime 通过 loopback HTTP `GET /health` + `POST /command` 实现，自动发现，无需配置 profile |
| 同一台机器运行多个 Unity 实例 | 每个项目有独立的队列目录（按项目路径哈希），不存在端口冲突 |
| 编译、Play 等命令生命周期超过单次 HTTP 请求 | Host 维护内存 `QueueService`，延迟型命令由 `EditorJobStateManager` 在 `Update()` 内驱动，不会超时 |

---

## 架构概览

```text
┌──────────────────────────────────────────────────────┐
│  ucp-cli（无状态，任意 Shell / CI / AI Agent）        │
│  ucp-cli run echo --session-type editor --args ...   │
└─────────────────────┬────────────────────────────────┘
                      │ HTTP POST /commands  127.0.0.1:6610
┌─────────────────────▼────────────────────────────────┐
│  ucp-host（后台守护进程，自动启动）                   │
│  ├── SessionService  — 读 ~/.ucp/sessions/*.json     │
│  ├── DiscoveryService — 监听 targets.json + 文件     │
│  ├── QueueService   — 内存命令生命周期管理           │
│  ├── SchedulerService — 调度循环                     │
│  ├── CapabilityRegistry — 校验命令类型               │
│  └── TransportRouter                                 │
│        ├── EditorFileQueueAdapter  （Editor）        │
│        └── RuntimeHttpAdapter      （Runtime）       │
└──────┬──────────────────────────┬────────────────────┘
       │ 写入 inbox JSON          │ POST /command
       ▼                          ▼
~/.ucp/queues/{id}/inbox/    http://127.0.0.1:{port}/
       │                          │
┌──────▼──────────────┐    ┌──────▼──────────────────┐
│ com.air.ucp-agent   │    │ RuntimeAgentHost        │
│（EditorAgentBootstrap│   │（MonoBehaviour，仅       │
│  每 250ms 轮询）     │    │  Play Mode 生命周期）   │
│ 执行 CliCommand      │    │ 执行 RuntimeCommand     │
│ 写入 outbox JSON    │    │ 返回 JSON 响应          │
└─────────────────────┘    └─────────────────────────┘
```

---

## 核心特性

- **Edit Mode 零 HTTP** — Editor Agent 使用文件队列，不开 `HttpListener`，无需 URL ACL，不需要管理员权限。
- **会话自动发现** — Agent 每 2 秒写一次 `~/.ucp/sessions/{projectId}.json`；`ucp-host` 自动将其提升为 ready 会话。
- **Targets 配置** — `~/.ucp/targets.json` 存储 Editor 和 Runtime 目标；Runtime 目标通过 `/health` 探测，Editor 目标自动回退到 Session 文件发现。
- **Capability 校验** — 每个会话声明自己支持哪些命令类型；`ucp-host` 在派发前拦截不支持的命令。
- **长命令延迟执行** — 编译、play、stop、exec 等命令由 `EditorJobStateManager` 在 `Update()` 内驱动，不会因 HTTP 超时而失败。
- **域重载安全（Domain Reload Safe）** — 命令在执行过程中触发脚本编译/域重载时不会丢失，详见下方说明。
- **Play Mode Runtime 通道** — `RuntimeAgentHost` 在 `AfterSceneLoad` 自动启动，绑定 `127.0.0.1:6620`（可配置），退出 Play Mode 时干净关闭。
- **多项目安全** — 队列路径以 `SHA-256(项目路径)[0:16]` 为键，多个 Unity 实例互不干扰。
- **全程结构化 JSON** — 所有命令响应格式统一为 `{ ok, data, message, error_code }`，便于脚本或 AI 工具调用解析。

---

## 域重载安全（Domain Reload Safe）

Unity 脚本编译结束时会触发 **Domain Reload**，所有 C# 静态状态都会被清空。UCP 通过双重持久化机制确保正在执行的命令不会丢失或卡死：

### 持久化方案

| 层 | 机制 | 说明 |
|----|------|------|
| 内存 | `EditorJobStateManager._commands` | 运行时主字典，`[InitializeOnLoad]` 重建后立即从下方两层恢复 |
| 轻量持久 | `SessionState`（`Air.UcpAgent.Jobs`） | Unity 内置的 Editor 会话键值存储，Domain Reload 后自动保留 |
| 磁盘持久 | `~/.ucp/jobs/{projectId}/{cmdId}.json` | `EditorJobLedger` 写入，任何崩溃或热重载都能从磁盘还原 |

### 重载后恢复流程

```text
Domain Reload 触发
    ↓
EditorJobStateManager (static ctor, [InitializeOnLoad])
    1. PurgeCorruptFiles() — 清理损坏的磁盘 job 文件
    2. LoadFromSession()   — 从 SessionState 还原内存字典
    3. MergePendingInto()  — 用磁盘文件补全 SessionState 遗漏的条目
    4. OrphanJobsAfterDomainReload() — 无法续跑的 job 标记为 Orphaned
    5. Save()              — 将合并结果写回 SessionState + 磁盘
    ↓
EditorAgentBootstrap (static ctor, [InitializeOnLoad])
    重新注册 EditorApplication.update
    重写 session 文件 → ucp-host 感知 Agent 已恢复 ready
```

### 哪些命令能跨域重载续跑？

`OrphanJobsAfterDomainReload` 负责判断：命令的 `CompletionKind` 字段决定其是否可以在域重载后继续推进。

| CompletionKind | 重载后行为 | 典型命令 |
|----------------|-----------|---------|
| `compilation` | 续跑 — `CompilationPolicy` 检查 `IsCompiling` 标志 | `compile`、`refresh` |
| `enter_play` | 续跑 — `EnterPlayModePolicy` 检查 Play Mode 状态 | `play` |
| `exit_play` | 续跑 — `ExitPlayModePolicy` 检查 Play Mode 状态 | `stop` |
| `deferred` | 续跑 — 由调用方在 `Update()` 中自行推进 | `exec` |
| 其他 / 空 | **Orphaned** — 立即写入失败结果，CLI 收到明确错误 | 未注册策略的命令 |

### 结果如何返回给 CLI？

域重载后 `EditorAgentBootstrap.PollInbox()` 在下一个 `EditorApplication.update` 周期（约 250 ms）重新轮询 inbox。对于已在 `outbox` 中写入结果的命令，`ucp-host` 会在轮询超时前取到结果并返回给 `ucp-cli`。对于被标记为 Orphaned 的命令，Agent 同样把错误写入 outbox，CLI 不会无限等待。

---

## 命令

命令目录由 Agent 自己生成，仓库里不再维护会过期的命令表：

```powershell
ucp-cli run help          # 列出全部命令、说明与参数
```

Editor 侧命令按操作对象分组：

| 命令族 | 覆盖范围 |
|--------|----------|
| `ping` `echo` `state` `console` `help` | 健康检查、Agent/Editor 状态、Console 日志、命令目录 |
| `compile` `play` `stop` `pause` `refresh` `reserialize` `menu` `screenshot` `profiler` `exec` `build` | 编辑器控制与诊断 |
| `prefab_*` | Prefab 资产、变体、实例、Overrides、嵌套、Prefab Mode |
| `gameobject_*` | 增删、复制、改名、换父级、Transform、启用、Tag、Layer、详情、层级、查找 |
| `component_*` | 加组件、删组件、列组件、读写序列化属性 |
| `scene_*` | 打开、新建、保存、另存、关闭、列表、详情 |
| `asset_*` | 列表、搜索、详情、建目录、删除、移动、复制、依赖、GUID |
| `selection_*` | 读取与设置编辑器选中项 |
| `tag_*` `layer_*` `tool_set` | 工程 Tag/Layer 表与当前变换工具 |
| `prefab_edit` `scene_edit` | 在一个 Prefab 或场景上连续执行多条节点命令，全部成功才保留 |
| `ui_snapshot` | 仅 Play Mode：列出 Canvas、文本，以及每个按钮能否点击 |

### 一套命令，两个作用域

每个 `gameobject_*` / `component_*` 命令都带一个可选的 `prefab` 参数。传了就直接改那个 Prefab
资产（**不需要打开场景，也不需要进 Prefab Mode**），不传就改当前打开的场景。因此 Prefab 与场景
的能力不会跑偏 —— 它们是同一份实现。

```powershell
# 改当前场景
ucp-cli run component_add --args '{"node":"Player/Body","component":"Rigidbody"}'

# 改磁盘上的 Prefab 资产
ucp-cli run component_add --args '{"prefab":"Assets/P/Player.prefab","node":"Player/Body","component":"Rigidbody"}'
```

### 批量编辑

`prefab_edit` / `scene_edit` 在同一个目标上按顺序执行一组节点命令（`gameobject_*`、
`component_*`、Prefab 实例命令），op 直接用这些命令的名字和参数。Prefab 只保存一次；任何一个 op
失败就什么都不保留：Prefab 文件不变，场景整体撤销。所有 op 在打开目标之前就先校验。单条命令也按
同一规则执行，相当于只有一个 op 的批量。

```powershell
ucp-cli run prefab_edit --args '{"prefab":"Assets/UI/Page.prefab","ops":[
  {"command":"gameobject_create","args":{"parent":"Page","name":"Card"}},
  {"command":"component_add","args":{"node":"Page/Card","component":"Image"}}]}'
```

op 里不能带 `prefab` / `scene`，目标只在批量命令上指定。

对象引用类属性可以用资产路径、GUID、节点路径或 instance id 赋值；`component_get` 读回的格式
与 `component_set` 接受的格式完全一致，可以直接改完塞回去。

---

## 快速开始

```powershell
cd ucp-cli
.\setup.ps1          # npm install + npm link（一次性配置）

ucp-cli ping
ucp-cli run state
ucp-cli host status
```

Unity `Packages/manifest.json`：

```json
"com.air.ucp-agent": "file:../CustomPackages/packages/unity-command-platform/com.air.ucp-agent"
```

打开 Unity 工程并等待编译完成，Agent 会写入 `~/.ucp/sessions/{projectId}.json`。

### Runtime（Play Mode）

```powershell
# 可选：预配置具名 Runtime 目标
ucp-cli targets set my-runtime --type runtime --host 127.0.0.1 --port 6620 --project C:/Project/GameDemo

ucp-cli run play --session-type editor
ucp-cli run echo --session-type runtime --args '{"message":"hello from runtime"}'
ucp-cli run stop --session-type editor
```

### Runtime Cmd 面板（Play Mode 内 UGUI）

将 `Resources/UcpCmdPanel.prefab` 拖入场景 Canvas 下即可使用（可用 `` ` `` 键显隐）。布局与 `_fontSize` 均在 Prefab 上配置。

```text
com.air.ucp-agent/Resources/UcpCmdPanel.prefab
com.air.ucp-agent/Runtime/CmdPanel/UcpCmdPanel.cs
```

场景需包含 Canvas、EventSystem。

| 区域 | 说明 |
|------|------|
| 历史区 | 每次执行两行：第一行 `> 命令`，第二行执行结果 |
| 提示区 | 根据当前输入实时显示可用命令及 `--参数` 说明 |
| 输入框 | 点击聚焦后输入；`Tab` 补全；`Enter` 执行；`↑`/`↓` 浏览历史 |

**扩展 Runtime 命令：** 在项目的 `Assets/Scripts/` 下新增 `CliCommand` / `CliCommand<TParams>`，编译后即可在面板中执行（与 `ucp-cli --session-type runtime` 共用命令发现机制）。

```csharp
// Assets/Scripts/MyRuntimeCommand.cs
public class MyRuntimeCommand : CliCommand<MyParams> { ... }
```

示例：

```text
> ping
pong

> echo --message hello
echo ok {"channel":"runtime","message":"hello"}
```

---

## 数据目录（`~/.ucp/`）

```text
~/.ucp/
├── host.json                        # 正在运行的 ucp-host 的 pid + port
├── host.lock                        # 进程启动锁
├── targets.json                     # 用户配置的目标
├── sessions/{projectId}.json        # Unity Agent 写入（Editor）
└── queues/{projectId}/
    ├── inbox/{cmdId}.json           # ucp-host → Unity
    └── outbox/{cmdId}.json          # Unity → ucp-host
```

---

## 测试

```powershell
cd ucp-cli
npm run verify                       # 构建 + lint + 单元测试

# 集成测试（需要 Unity Editor 打开并编译完成）
$env:UCP_EDITOR_INTEGRATION="1"
npm run test:integration
```

---

## 请求路径

```text
ucp-cli → ucp-host (:6610) → ~/.ucp/queues/.../inbox    （Editor）
                            → POST http://…/command       （Runtime）
         → com.air.ucp-agent 轮询 / RuntimeAgentHost
         → outbox / HTTP 响应
         → ucp-host → ucp-cli 打印 JSON
```

Edit Mode 无 Unity 侧 HTTP，无 `~/.unity-cmd/` profile。
