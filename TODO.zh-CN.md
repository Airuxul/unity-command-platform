# 待办 — UCP（`unity-command-platform`）

**最后更新：** 2026-11-05 · **范围：** UCP 栈后续优化 · 本仓库唯一的待办文档

> **仓库结构：** `ucp-cli`（Node，0.3.0）+ `com.air.ucp-agent`（Unity UPM，0.5.0）。
> **对标：** [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) `e2fd09c`（v10.3.1-beta）。

## 方向

保住 UCP 相对 unity-mcp 的优势：
- CLI 原生，不占 MCP 工具 schema 的上下文；
- Edit Mode 下 Unity 不开端口；
- domain reload 后命令能续跑；
- `group` 把几条命令当一条跑，失败可以全部撤回，跨 compile、Play 也一样。

不去追 unity-mcp 的工具数量。从它那里只借 UCP 缺、且对 AI 闭环有用的部分。

### 背景：Unity 在后台时的降频

UCP 的 inbox 检查跑在 `EditorApplication.update` 里，每个 tick 一次。Unity 失去焦点后 tick 降到每秒约 10 次，没有线程能唤醒它。最小化时连发 `ping` 中位 96 ms、随机到达约 75 ms（`background.test.ts` 自动测得，见 [RELIABILITY §4](docs/RELIABILITY.zh-CN.md)），普通命令已够用；长任务期间的降频仍是开放问题（AGENT-14）。

unity-mcp 的做法供参考：
1. **普通命令**：后台线程收到命令后，`SynchronizationContext.Post` 投递到主线程并调 `QueuePlayerLoopUpdate()`（`TransportCommandDispatcher.cs:113-177`，best-effort）。
2. **跑测试时**：临时设 EditorPrefs `InteractionMode=1`、`ApplicationIdleTime=0`，原值存 SessionState，结束时还原（`TestRunnerNoThrottle.cs:87-109`）。
3. **跑测试卡住时兜底**：`focus_nudge.py`，超过 3 秒没进展就把 Unity 切到前台再切回，会抢焦点。

---

## `com.air.ucp-agent`（AGENT-）

### 现有能力

- Editor FileQueue（`~/.ucp/queues/{projectId}/inbox|outbox`），认领改名，每条命令只执行一次，执行完不留文件
- Session 注册（`~/.ucp/sessions/{projectId}.json`，含 pid / focused / 默认超时）
- `CommandRegistry` + `CommandExecutor`：Editor 与 runtime 共用一套发现、绑定、执行
- 延迟命令跨 domain reload 续跑：`compile` / `refresh` / `play` / `stop` / `test_run` / `package_*` / `ui_wait`
- 内容命令：prefab / gameobject / component / scene / asset / selection，场景和 Prefab 资产一份实现；`group` 支持 `rollback`、`on_error`、`dry_run`、`expect`
- Play Mode UI：`ui_snapshot` / `ui_click` / `ui_input` / `ui_wait`
- `test_run`、`build`、`package_list` / `package_add` / `package_remove`
- 结构化错误码 + `hint`；`help` 单命令卡片和 markdown 输出
- 树类读命令有 `limit` / `truncated` / `format=compact`
- Release Player 不启动 runtime agent（除非定义 `UCP_RUNTIME_AGENT`）；runtime agent 拒绝带 `Origin` 的请求
- 完整命令目录用 `ucp-cli help` 查，仓库不维护命令表

### 待办

| ID | 优先级 | 标题 | 说明 |
|----|--------|------|------|
| AGENT-15 | P1 | 上报降频状态 | 超时 `hint` 已按 session 的 `focused` 区分"Unity 在后台"和"命令卡住"。剩余：`state` 返回 `editor_focused`、`run_in_background`、当前交互模式。 |
| AGENT-14 | P2 | 长任务期间临时关闭降频 | 先实验：Unity 在后台时改 EditorPrefs `InteractionMode=1`、`ApplicationIdleTime=0` 并反射调 `UpdateInteractionModeSettings`，对比主循环频率。Unity 文档说后台降频和这两项是两回事，大概率无效；有效才实现（原值备份到 `~/.ucp` 文件并带 pid，EditorPrefs 是本机所有 Unity 共用的），无效就在 RELIABILITY 写明结论并关掉。 |
| AGENT-20 | P2 | Runtime 非 loopback 绑定要鉴权 | `UCP_RUNTIME_BIND` 仍可绑到非 loopback 地址且无鉴权（`RuntimeAgentHost.cs` `ResolveBindHost`）。目前 runtime 只有 `ping` / `echo` / `help`，风险取决于项目自己注册的 runtime 命令。最小做法：非 loopback 且没设 `UCP_RUNTIME_TOKEN` 时拒绝启动。 |
| AGENT-19 | P3 | Prefab 资产改动前备份 | 场景改动能 Ctrl+Z，Prefab 资产保存后不能。保存前把原文件备份到 `~/.ucp/backups/{projectId}/{cmdId}/`，配合 CLI-07 还原。工程在版本控制下时与 git 重复，按需再做。 |

### 请勿在本包实现

| 主题 | 归属 |
|------|------|
| CLI / Host 调度 | `ucp-cli` |
| `GameRuntime`、实体、UI | `com.air.unity-game-core` / `com.air.unity-ui` |

---

## `ucp-cli`（CLI-）

### 现有能力

- `ucp-cli` + `ucp-host` 单 npm 包（TypeScript, vitest）
- host 按 session 分执行链，`runsWhileBusy` 里的命令不排队；deadline 从提交时算，并随命令交给 Unity；Editor 忙时等待而非拒绝；长轮询取结果
- host 判定的失败也带错误码、message、hint；host 死掉时 CLI 返回 `host_lost` 而不是抛异常
- 重启旧 host 走 `POST /shutdown`，host 有命令在跑时拒绝
- host 本机 token（`X-UCP-Token`），拒绝带 `Origin` 或 Host 不是 127.0.0.1/localhost 的请求
- host 构建号：CLI 发现空闲的旧 host 自动重启
- `ucp-cli help [command] [--md]`、`run-script`、`build <output_path>`
- 单元测试 + 假 agent e2e（不需要 Unity）；`UCP_EDITOR_INTEGRATION` 真机测试，catalog / 返回结构快照

### 待办

| ID | 优先级 | 标题 | 说明 |
|----|--------|------|------|
| CLI-01 | P2 | CI | 仓库没有 CI。加 `.github/workflows/ci.yml` 跑 build、check、单元测试和假 agent 的 `ping-e2e`。Unity 测试需要许可证，留在本地闸门（见 MAINTENANCE）。 |
| CLI-04 | P3 | 发布 | README 已写 `setup.ps1` / PATH 排错和 UPM git URL 安装。剩余：npm 发包或 `npx` 用法。 |
| CLI-07 | P3 | `ucp-cli restore <cmdId>` | 依赖 AGENT-19 的备份。 |

### 请勿在本包实现

| 主题 | 归属 |
|------|------|
| Unity 主线程 API | `com.air.ucp-agent` |
| 业务命令逻辑 | `Editor/Commands/` |

---

## 已完成（0.5.0 / ucp-cli 0.3.0）

详细变化与升级表见 [docs/CHANGELOG_AGENT.md](docs/CHANGELOG_AGENT.md)。

| ID | 内容 |
|----|------|
| AGENT-01 | `build` 接入 `BuildPipeline.BuildPlayer`，返回报告摘要，失败为 `build_failed` |
| AGENT-02 / CLI-02 | Runtime HTTP：Play Mode 下 `RuntimeAgentHost` + `RuntimeHttpAdapter` 可用，与 Editor 共用命令执行器 |
| AGENT-03 | 域重载后的 job 与 outbox 一致：只有延迟命令建 job，结果投递后删除，ledger 保留全部字段 |
| AGENT-04 | `profiler` 的 `action` 默认 `hierarchy`，`sort` 支持 `calls`，取值由 binder 校验 |
| AGENT-05 | `test_run`：EditMode / PlayMode（域重载开关两种都测过） |
| AGENT-06 | `package_list` / `package_add` / `package_remove` |
| AGENT-12 | 延迟基线：后台（`background.test.ts`，自动最小化）tick 每秒约 10 次，连发 `ping` 中位 295 ms → 96 ms；前台（`foreground.test.ts`，需 `UCP_ALLOW_FOCUS=1`）tick 每秒 70–294 次，`ping` 中位约 18 ms |
| AGENT-16 | 结构化错误码 + `hint`，`compile` 编译失败返回 `compile_error` |
| AGENT-17 | 树类读命令 `limit` / `truncated` / `format=compact` / `fields`，`ui_snapshot` 有 `root` / `limit` |
| AGENT-18 | batch op 的 `expect` 前置条件 |
| AGENT-21 | `ui_click` / `ui_input` / `ui_wait` |
| CLI-05 | Editor 忙时等待；进程已退出时 `editor_not_running` |
| CLI-06 | host 本机 token |
| CLI-09 | `help command=` / `format=md`，`ucp-cli help [command] [--md]` |
| CLI-10 | outbox 用 `fs.watch`，轮询兜底 |
| CLI-11 | `ucp-cli run-script`（由 CLI 执行而非 host：放在 host 不多任何能力，CLI 更简单） |

---

## 不做

- `script_*` C# 脚本增删改查（原 AGENT-08）：AI 改脚本用文件工具 + `compile` 更顺手。
- 通用 `batch`（原 AGENT-09）：已由 `group` 实现，任何命令都能放进去。
- `material_*` / `shader_*` / `texture_*`（AGENT-07）：Renderer 上的材质属性已能用 `component_set` 改。
- `prefab_replace` 保留 overrides（AGENT-10）：要么逐项搬运 override，要么按 Unity 版本切到 2022.2+ 的 API，使用频率低。
- Play Mode 真实合成帧截图（AGENT-11）：`screenshot view=ui` 已能核对布局，后处理画面需求低。
- 后台线程唤醒主线程（AGENT-13）：每个 tick 检查 inbox 后，最小化 `ping` 中位已降到约一个 tick（约 100 ms）以内，唤醒的收益不再值得一个后台线程。
- 抢焦点唤醒（AGENT-22）：会打断用户操作。
- SQLite Editor 适配器（CLI-03）：FileQueue 已满足需求。
- 单工具 MCP 适配层（CLI-08）：方向是 CLI 原生；需要时 `help --md` 可直接作为 AI 说明。
