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
| 编译、Play、跑测试等命令会触发域重载 | Agent 为每条这类命令保留一个跨重载的 job，完成后再返回结果；CLI 期间长轮询 ucp-host |

---

## 架构概览

```text
┌──────────────────────────────────────────────────────┐
│  ucp-cli（无状态，任意 Shell / CI / AI Agent）        │
│  ucp-cli run echo --session-type editor --args ...   │
└─────────────────────┬────────────────────────────────┘
                      │ HTTP 127.0.0.1:6610，带 X-UCP-Token
┌─────────────────────▼────────────────────────────────┐
│  ucp-host（后台守护进程，自动启动）                   │
│  ├── SessionDirectory — sessions/*.json、targets.json│
│  ├── CommandRunner   — 每个 session 一条执行链，      │
│  │                     deadline、长轮询              │
│  ├── EditorFileQueueAdapter  （Editor）              │
│  └── RuntimeHttpAdapter      （Runtime）             │
└──────┬──────────────────────────┬────────────────────┘
       │ 写入 inbox JSON          │ POST /command
       ▼                          ▼
~/.ucp/queues/{id}/inbox/    http://127.0.0.1:{port}/
       │                          │
┌──────▼───────────────┐   ┌──────▼──────────────────┐
│ com.air.ucp-agent    │   │ RuntimeAgentHost        │
│（EditorAgentBootstrap│   │（MonoBehaviour，仅      │
│  每个 tick 检查）     │   │  Play Mode）            │
│ CommandExecutor 执行 │   │ CommandExecutor 执行    │
│ CliCommand           │   │ CliCommand              │
│ 写入 outbox JSON     │   │ 返回 JSON 响应          │
└──────────────────────┘   └─────────────────────────┘
```

---

## 核心特性

- **Edit Mode 零 HTTP** — Editor Agent 使用文件队列，不开 `HttpListener`，无需 URL ACL，不需要管理员权限。
- **会话自动发现** — Agent 每 2 秒写一次 `~/.ucp/sessions/{projectId}.json`；`ucp-host` 自动将其提升为 ready 会话。
- **Targets 配置** — `~/.ucp/targets.json` 存储 Editor 和 Runtime 目标；Runtime 目标通过 `/health` 探测，Editor 目标自动回退到 Session 文件发现。
- **Capability 校验** — 每个会话声明自己支持哪些命令类型；`ucp-host` 在派发前拦截不支持的命令。
- **等待而不是失败** — Editor 导入资源期间发来的命令会等它就绪；每条命令有自己的默认超时（`build` 30 分钟、`test_run` 10 分钟、`compile` 180 秒，其余多数 30 秒）。`ping`、`state`、`console` 不用等测试或编译结束（只有域重载的那几秒要等）。
- **域重载安全（Domain Reload Safe）** — 触发域重载的命令照样返回结果，且只返回一次，详见下方说明。
- **Play Mode Runtime 通道** — `RuntimeAgentHost` 在 `AfterSceneLoad` 自动启动，绑定 `127.0.0.1:6620`（可配置），退出 Play Mode 时干净关闭。
- **多项目安全** — 队列路径以 `SHA-256(项目路径)[0:16]` 为键，多个 Unity 实例互不干扰。
- **全程结构化 JSON** — 所有响应格式统一为 `{ ok, data, message, error_code, hint, timing }`。`error_code` 是固定的错误码（`node_not_found`、`play_mode_required`、`compile_error` 等），脚本可以按码分支；`ucp-cli help <命令>` 列出该命令的错误码。

---

## 域重载安全（Domain Reload Safe）

脚本重新编译或进入 Play Mode 时，Unity 会清空所有 C# 静态状态（**域重载**）。触发域重载的命令（`compile`、`refresh compile=true`、`play`、`stop`、`test_run`、`package_add`、`package_remove`）照样返回结果，且只返回一次。

### 实现方式

1. 一次调用内完成不了的命令是**延迟命令**：运行之前，Agent 先为它建一个 job，记下 completion 类型和 deadline。其余命令同步返回，不建 job。
2. job 同时存在内存、`SessionState`（`Air.UcpAgent.Jobs`，域重载后保留）和 `~/.ucp/jobs/{projectId}/{cmdId}.json`（除非磁盘丢失，否则都在）。
3. 域重载后，`EditorJobStateManager` 在第一次轮询 inbox 之前把 job 读回来。之后每个 editor tick，由对应的 **completion policy** 判断 job 是否完成：

| Completion 类型 | 完成条件 | 命令 |
|-----------------|----------|------|
| `compilation` | Unity 不再编译；脚本编译不过则以 `compile_error` 失败 | `compile`、`refresh compile=true` |
| `enter_play` / `exit_play` | 已进入 / 已退出 Play Mode | `play`、`stop` |
| `tests` | Test Runner 已上报结果，且 Editor 已回到 Edit Mode | `test_run` |
| `packages` | 包已注册（或已移除），且 Editor 已稳定 | `package_add`、`package_remove` |
| `ui_wait` | 节点条件成立；到 deadline 前主动失败 | `ui_wait` |
| `started` | 由命令自己的回调完成；回调随旧 domain 消失，重载后标为 **orphaned** | — |

4. 完成的 job 把结果写进 outbox 后删除。超过 deadline 的 job 直接删除、不写结果：ucp-host 按同一个 deadline 计时，已经回了 `timeout`。

命令运行前先把 inbox 文件改名为 `.claimed` 认领；已经有 job 的命令不会再次启动。所以重载发生在中途也不会执行两次。

---

## 命令

命令目录由 Agent 自己生成，仓库里不再维护会过期的命令表：

```powershell
ucp-cli help                  # 列出全部命令、说明与参数
ucp-cli help prefab_create    # 单条命令：参数、调用示例、错误码
ucp-cli help --md             # 全部命令卡片的 markdown，可放进 AI Agent 的说明
```

Editor 侧命令按操作对象分组：

| 命令族 | 覆盖范围 |
|--------|----------|
| `ping` `echo` `state` `console` `help` | 健康检查、Agent/Editor 状态、Console 日志、命令目录 |
| `compile` `play` `stop` `pause` `refresh` `reserialize` `menu` `screenshot` `profiler` `exec` | 编辑器控制与诊断 |
| `build` `test_run` | 出包；跑 EditMode / PlayMode 测试并返回失败用例 |
| `package_*` | 列出、添加、移除 UPM 包 |
| `prefab_*` | Prefab 资产、变体、实例、Overrides、嵌套、Prefab Mode |
| `gameobject_*` | 增删、复制、改名、换父级、Transform、启用、Tag、Layer、详情、层级、查找 |
| `component_*` | 加组件、删组件、列组件、读写序列化属性 |
| `scene_*` | 打开、新建、保存、另存、关闭、列表、详情 |
| `asset_*` | 列表、搜索、详情、建目录、删除、移动、复制、依赖、GUID |
| `selection_*` | 读取与设置编辑器选中项 |
| `tag_*` `layer_*` `tool_set` | 工程 Tag/Layer 表与当前变换工具 |
| `prefab_edit` `scene_edit` | 在一个 Prefab 或场景上连续执行多条节点命令，全部成功才保留 |
| `ui_snapshot` `ui_click` `ui_input` `ui_wait` | 仅 Play Mode：读取 UI、点击和输入、等待后续变化 |

`ucp-cli run-script flow.json` 按顺序执行一组命令，遇到第一个失败就停：

```json
[
  { "command": "play" },
  { "command": "ui_click", "args": { "node": "Canvas/Login/Go" } },
  { "command": "ui_wait", "args": { "node": "Canvas/Home", "until": "active" }, "timeout": 10000 },
  { "command": "stop" }
]
```

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

需要 Node 20 及以上。在本仓库的克隆目录里：

```powershell
cd ucp-cli
.\setup.ps1          # npm install、构建、npm link：把 ucp-cli 和 ucp-host 放进 PATH

ucp-cli ping
ucp-cli run state
ucp-cli host status
```

不想全局链接时：`node <仓库>/ucp-cli/bin/ucp-cli.js ping`。跑完 `setup.ps1` 仍提示找不到 `ucp-cli`，说明 npm 全局目录（`npm prefix -g`）不在 `PATH` 里；加进去后重开终端。

Unity `Packages/manifest.json`，用本地克隆或直接用 git 地址：

```json
"com.air.ucp-agent": "file:C:/path/to/unity-command-platform/com.air.ucp-agent"
"com.air.ucp-agent": "https://github.com/Airuxul/unity-command-platform.git?path=/com.air.ucp-agent"
```

打开 Unity 工程并等待编译完成，Agent 会写入 `~/.ucp/sessions/{projectId}.json`。

### 从 0.3 升级

CLI 和 Unity 包要一起升级，同一台机器上装的所有 `ucp-cli` 也要一起升级：0.2 的 CLI 连 0.3 的 host，每次调用都会得到 `unauthorized`。有若干参数改了名，所有失败结果都带上了错误码；完整清单见
[docs/CHANGELOG_AGENT.md](docs/CHANGELOG_AGENT.md#upgrading-from-03)。

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

**扩展 Runtime 命令：** 在项目脚本里任意位置新增 scope 为 `CommandHostScope.Runtime` 的 `CliCommand` / `CliCommand<TParams>`，编译后面板和 `ucp-cli --session-type runtime` 都能找到它。

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
├── host.json                        # 正在运行的 ucp-host 的 pid、port、token
├── host.lock                        # 进程启动锁
├── targets.json                     # 用户配置的目标
├── sessions/{projectId}.json        # Unity Agent 写入（Editor）
├── jobs/{projectId}/{cmdId}.json    # 进行中的延迟命令；空闲时为空
└── queues/{projectId}/
    ├── inbox/{cmdId}.json           # ucp-host → Unity；执行期间改名为 .claimed
    └── outbox/{cmdId}.json          # Unity → ucp-host；读取后删除
```

---

## 测试

```powershell
cd ucp-cli
npm run verify                       # 构建 + lint + 单元测试（集成测试没有 Unity 时自动跳过）

# 连接一个已打开且编译完成的 Editor
$env:UCP_EDITOR_INTEGRATION="1"
$env:UCP_TEST_PROJECT="C:/path/to/UnityProject"
npm run test:integration             # 约 6 分钟；加 $env:UCP_EDITOR_SLOW="1" 会跑 compile/play 和真实出包
npm run smoke                        # 一次 Prefab 往返，几秒
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
