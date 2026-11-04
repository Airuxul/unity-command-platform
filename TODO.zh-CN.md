# 待办 — UCP（`packages/ucp`）

**最后更新：** 2026-11-04 · **范围：** UCP 栈后续优化 · 本仓库唯一的待办文档

> **仓库结构：** `ucp-cli`（Node）+ `com.air.ucp-agent`（Unity UPM）。
> **对标：** [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) `e2fd09c`（v10.3.1-beta）。

## 方向

保住 UCP 相对 unity-mcp 的优势：
- CLI 原生，不占 MCP 工具 schema 的上下文；
- Edit Mode 下 Unity 不开端口；
- domain reload 后命令能续跑；
- `prefab_edit` / `scene_edit` 整批成功才保存，失败不留半成品。

不去追 unity-mcp 的工具数量（51 个）。从它那里只借 UCP 缺、且对 AI 闭环有用的部分。

### 背景：Unity 在后台时的降频

UCP 的 inbox 轮询跑在 `EditorApplication.update` 里。Unity 失去焦点、update 被降频后，没有任何线程能唤醒它。

unity-mcp 用三层机制处理这个问题，AGENT-12 到 15 和 AGENT-22 以此为参考：
1. **普通命令**：后台线程收到命令后，用 `SynchronizationContext.Post` 投递到主线程，并调 `QueuePlayerLoopUpdate()`（`TransportCommandDispatcher.cs:113-177`，作者注明是 best-effort）。
2. **跑测试时**：临时设 EditorPrefs `InteractionMode=1`（No Throttling）、`ApplicationIdleTime=0`；原值存 SessionState，测试结束时还原（`TestRunnerNoThrottle.cs:87-109`）。
3. **跑测试卡住时兜底**：`focus_nudge.py`。超过 3 秒没进展就把 Unity 切到前台，停 3 到 12 秒再切回原窗口。会抢焦点，只有 `run_tests` 调用。

---

## `com.air.ucp-agent`（AGENT-）

### 现有能力

- Editor FileQueue 轮询（`~/.ucp/queues/{projectId}/inbox|outbox`）
- Session 注册（`~/.ucp/sessions/{projectId}.json`）
- `CliCommand` 自动发现 + `UcpCliCommandHandler` 桥接
- 延迟命令：`compile` / `play` / `stop` + `EditorJobStateManager`
- Editor 侧 82 个命令：prefab / gameobject / component / scene / asset / selection + 编辑器控制
- `EditTargetScope`：一份实现同时支持改场景和改 Prefab 资产。每条命令合成一个 Undo 组，失败时回退到该组
- `prefab_edit` / `scene_edit`：op 直接复用命令名和参数，整批失败不保存；支持 `dry_run`
- `console`：合并重复日志、保留最新、返回 `matched` / `truncated`，堆栈默认只保留项目代码帧
- Release Player 不启动 runtime agent（除非定义 `UCP_RUNTIME_AGENT`）；runtime agent 拒绝带 `Origin` 的请求
- 完整命令目录用 `ucp-cli run help` 查，仓库不维护命令表

### 待办

| ID | 优先级 | 标题 | 说明 |
|----|--------|------|------|
| AGENT-12 | P0 | 实测后台延迟基线 | Unity 在前台、在后台两种情况下，各连发 10 条 `ping`，记录 p50/p95；同时确认 Windows 上 `QueuePlayerLoopUpdate` 对不在前台的 Editor 是否有效。数据记入 `docs/RELIABILITY.zh-CN.md`，AGENT-13 到 15 做到什么程度以此为准。 |
| AGENT-13 | P0 | 后台线程唤醒主线程 | 用 `FileSystemWatcher` 监听 inbox，收到文件时 `SynchronizationContext.Post` + `QueuePlayerLoopUpdate()`；250ms 轮询保留作兜底。验收：后台 p95 接近前台，前台单条延迟也下降。 |
| AGENT-14 | P0 | 长任务期间临时关闭降频 | compile / play / exec 等延迟 job 开始时设 `InteractionMode=1`、`ApplicationIdleTime=0`，原值存 SessionState，跨 reload 也能还原，job 结束还原。验收：后台 compile、play 不卡，用户原设置不被改坏。 |
| AGENT-15 | P1 | 上报降频状态 | `state` 返回 `editor_is_focused`、`run_in_background`、当前交互模式；命令超时时写明原因（如 `EDITOR_THROTTLED`），能区分"Unity 在后台"和"命令卡死"。 |
| AGENT-16 | P1 | Unity 侧结构化错误码 | 现状：CLI/host 层已有 `error_code` + `hint` 字段，但 Unity 侧命令失败只给 message，到 CLI 一律变成 `command_failed`。补错误码：`NODE_NOT_FOUND`、`TYPE_NOT_FOUND`、`PLAY_MODE_REQUIRED`、`COMPILE_ERROR`、`ORPHANED`、`EDITOR_THROTTLED`，并带修复提示，如找不到节点时附同级最相近的 3 个节点名。 |
| AGENT-17 | P1 | 树类读命令控输出大小 | 现状：`prefab_tree` / `gameobject_tree` 只有 `depth`，`console` 和 `asset_*` 已有 limit + truncated。补：`root`（只看某个子树）、`limit` + `truncated`、可选字段过滤、默认一个节点一行的紧凑格式。大预制一次 dump 不应上万 token。 |
| AGENT-20 | P1 | Runtime 非 loopback 绑定要鉴权 | Release 门控和 Origin 拒绝已做，但 `UCP_RUNTIME_BIND` 仍可绑到非 loopback 地址且无鉴权（`RuntimeAgentHost.cs` `ResolveBindHost`）。非 loopback 时强制要求 token，否则拒绝启动。 |
| AGENT-01 | P1 | `build` 接入 BuildPipeline | 当前为骨架，需真实构建流程；异步，走延迟 job。 |
| AGENT-02 | P1 | Runtime Agent HTTP | 按架构 §10 实现 Player 侧适配（独立阶段）。 |
| AGENT-21 | P2 | Play Mode UI 交互 | 在 `ui_snapshot` 基础上加 `ui_click`、`ui_input`、`ui_wait`（等节点出现或文本变化），形成"改码 → 进 Play → 点击 → 截图核对"闭环。 |
| AGENT-05 | P2 | `test_run` 测试运行 | asmdef 引用 `com.unity.test-framework`；用 `TestRunnerApi` 回调接入延迟 job，`CompletionKind` 新增 `tests`；跑测试期间配合 AGENT-14 关降频。参考 unity-mcp `TestJobManager.cs`。 |
| AGENT-06 | P2 | `package_*` 包管理 | `UnityEditor.PackageManager.Client` 是异步 API，同样走延迟 job。 |
| AGENT-11 | P2 | Play Mode 真实合成帧截图 | `screenshot view=ui` 把 canvas 放到临时相机上，只能看布局，看不到后处理和 overlay 排序后的真实画面。`ScreenCapture.CaptureScreenshot` 能拿到，但要到帧末才出结果，需走延迟 job。 |
| AGENT-18 | P2 | edit op 前置条件 | `dry_run` 已做。op 可带 `expect`（节点存在、组件类型匹配），不满足则整批拒绝，用于改共享预制。 |
| AGENT-19 | P2 | Prefab 资产改动前备份 | 场景改动已能 Ctrl+Z；Prefab 资产保存后无法撤销。保存前把原文件备份到 `~/.ucp/backups/{cmdId}/`，配合 CLI-07 还原。 |
| AGENT-03 | P2 | 域重载后延迟任务审计 | `EditorJobLedger` 与 outbox 一致性。 |
| AGENT-04 | P2 | `profiler` 参数绑定 | 修复/补全参数校验与文档。 |
| AGENT-10 | P3 | `prefab_replace` 保留 overrides | 当前手工重新实例化（不依赖 2022.2+ 的 `ReplacePrefabAssetOfPrefabInstance`），实例 overrides 会丢。要么手动搬运，要么用版本宏切到新 API。 |
| AGENT-07 | P3 | `material_*` / `shader_*` / `texture_*` | 创建/编辑资产本身。Renderer 上的材质属性已能用 `component_set` 改，优先级维持最低。 |
| AGENT-22 | P3 | 抢焦点唤醒（opt-in） | 参考 `focus_nudge.py`：卡住超过 3 秒切 Unity 到前台再切回，间隔逐次加长，有次数上限。会打断用户操作，只给无人值守 CI 用，默认关闭。 |

### 请勿在本包实现

| 主题 | 归属 |
|------|------|
| CLI / Host 调度 | `ucp-cli` |
| `GameRuntime`、实体、UI | `com.air.unity-game-core` / `com.air.unity-ui` |

---

## `ucp-cli`（CLI-）

### 现有能力

- `ucp-cli` + `ucp-host` 单 npm 包（TypeScript, vitest）
- FileQueue e2e、`UCP_EDITOR_INTEGRATION` 真机测试
- `~/.ucp` 路径、Session 发现、Scheduler + Retry
- host 拒绝带 `Origin` 或 Host 不是 127.0.0.1/localhost 的请求（403），挡住浏览器页面和 DNS rebinding

### 待办

| ID | 优先级 | 标题 | 说明 |
|----|--------|------|------|
| CLI-05 | P1 | 会话忙时等待而非拒绝 | Editor 导入资源时不 tick，心跳超过 `STALE_MS`（`host/targets/target-probe.ts`），`ucp-host-server.ts` 在提交时直接回 `no_ready_session`；`RetryPolicy` 只管已入队命令。host 应持有命令，直到会话 ready 或调用方超时。session 文件写入 Unity pid：pid 还活着视为忙、等待；pid 已退出直接失败。做完后删掉 `executeCommandResilient` 这层补丁。 |
| CLI-10 | P1 | outbox 改事件驱动 | `editor-file-queue-adapter.ts` 按 `OUTBOX_POLL_INTERVAL_MS` 轮询。改用 `fs.watch`，轮询保留兜底。Unity 侧对应 AGENT-13。 |
| CLI-11 | P1 | `run-script` 跨命令流水线 | 推荐的验证流程 `play → ui_snapshot → screenshot → console → stop` 要调 5 次 CLI。`ucp-cli run-script flow.json` 由 host 按序执行，任一步失败即停，一次性返回汇总；流程里的 play/stop 跨 reload 也要能续跑。edit 类的批量已由 `prefab_edit` / `scene_edit` 覆盖，这里补的是跨类命令。 |
| CLI-01 | P1 | CI 集成矩阵 | `npm run smoke` 作廉价闸门；Editor 可用时跑 `test:integration:editor`。至少一个 Unity LTS，覆盖 reload 续跑、play/stop、Unity 在后台三类场景。 |
| CLI-02 | P1 | Runtime HTTP 适配器 | 对接 Player Agent（与 AGENT-02 配对）。 |
| CLI-06 | P2 | host 本机 token | 浏览器来源已挡住，但本机任意进程仍能调 `exec`。host 启动时生成随机 token 写进 `~/.ucp/host.json`，CLI 读出放进请求头，缺失返回 401。 |
| CLI-07 | P2 | `ucp-cli restore <cmdId>` | 用 AGENT-19 的备份还原 Prefab 资产。 |
| CLI-08 | P2 | 可选的单工具 MCP 适配层 | 只暴露 `ucp_run(command, args)` 一个工具，命令目录交给 `help` 按需查。MCP 客户端能接入，schema 开销只有 1 个工具。 |
| CLI-09 | P2 | 由 `help` 生成 AI 说明 | `help --format md` 输出命令卡片（参数、示例、错误码），打包成 skill 或 AGENTS 片段；`help --json` 供 CI 校验 README 里的命令数和分组。 |
| CLI-04 | P2 | 安装与发布 | Windows PATH / `npm link` 排错文档；UPM 支持 git URL `?path=/com.air.ucp-agent` 安装；npm 发包或提供 `npx` 用法。 |
| CLI-03 | P2 | SQLite Editor 适配器 | 架构 §5.1 可选方案，低优先级。 |

### 请勿在本包实现

| 主题 | 归属 |
|------|------|
| Unity 主线程 API | `com.air.ucp-agent` |
| 业务命令逻辑 | `Editor/Commands/` |

---

## 不做

- `script_*` C# 脚本增删改查（原 AGENT-08）：AI 改脚本用文件工具 + `compile` 更顺手。
- 通用 `batch`（原 AGENT-09）：edit 类已由 `prefab_edit` / `scene_edit` 覆盖，跨类命令走 CLI-11。
- 默认开启抢焦点唤醒（见 AGENT-22）。

## 推荐顺序

1. AGENT-12 实测，再按数据做 AGENT-13、AGENT-14。
2. AGENT-20、CLI-05：改动小，挡住的是真实问题。
3. AGENT-16、AGENT-17、CLI-10：对 AI 每轮的 token 和延迟影响最大。
4. AGENT-21 + CLI-11：和 unity-mcp 拉开差距的部分。
5. 其余按需排期。
