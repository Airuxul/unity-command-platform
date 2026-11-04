# UCP 优化待办（对标 unity-mcp）

**最后更新：** 2026-11-04 · **基线：** `origin/master` `efa91f9`；对照 [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) `e2fd09c`（v10.3.1-beta）

ID 接着 [TODO.zh-CN.md](../TODO.zh-CN.md) 的编号往下排。已有条目（AGENT-01/05/06/09、CLI-01）只补充新结论，不重复列。

## 方向

保住 UCP 相对 unity-mcp 的优势：
- CLI 原生，不占 MCP 工具 schema 的上下文；
- Edit Mode 下 Unity 不开端口；
- domain reload 后命令能续跑；
- `*.edit` 批量 ops 全部成功才保存，失败不留半成品。

不去追 unity-mcp 的工具数量（51 个）。

---

## P0：安全与正确性

| ID | 标题 | 现状（依据） | 做法 | 验收 |
|----|------|-------------|------|------|
| AGENT-11 | Runtime agent 只在编辑器和开发包里启用 | `RuntimeAgentHost` 靠 `[RuntimeInitializeOnLoadMethod]` 无条件自启动（`RuntimeAgentHost.cs:41`）；Runtime asmdef 没有 `defineConstraints`。结果：正式包的 Player 也会开 `HttpListener`，`UCP_RUNTIME_BIND` 还能绑到非 loopback 地址，且无鉴权（`:369`） | 1. 包一层 `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`，再加一个默认关闭的 opt-in 开关<br>2. bind 到非 loopback 地址时强制要求 token | 打 Release 包后 6620 端口不监听 |
| CLI-05 | 给 host 加本机 token | `ucp-host-server.ts:80` 只绑了 127.0.0.1，没有鉴权。本机任何进程都能调 `exec`，也就是在 Editor 里执行任意 C# | host 启动时生成随机 token 写进 `~/.ucp/host.json`；CLI 读出来放进请求头；不带 token 返回 401 | 不带 token 的 curl 请求被拒 |
| AGENT-15 | 命令设计统一（已完成） | 两套已合并：`HierarchyEdit` 那套 op 能映射的只有约 15 条命令，且值解析、路径解析、Undo 都弱于服务层；其余约 55 条命令没有 op 可对应 | 批量命令 `prefab_edit` / `scene_edit` 为主入口，op 直接复用命令名和参数（同 unity-mcp `batch_execute`）；Unity 端只留服务层一套实现，单条命令就是只有一个 op 的批量，失败不保存 | 只剩一套实现；README、TODO 与 `help` 输出一致（82 个命令） |

## P0：Unity 在后台时的响应

背景：UCP 的 inbox 轮询跑在 `EditorApplication.update` 里。Unity 失去焦点、update 被降频后，没有任何线程能唤醒它。

unity-mcp 用三层机制处理这个问题：
1. **普通命令**：后台线程收到命令后，用 `SynchronizationContext.Post` 投递到主线程，并调 `QueuePlayerLoopUpdate()`（`TransportCommandDispatcher.cs:113-177`，作者注明是 best-effort）。
2. **跑测试时**：临时开 No Throttling（`TestRunnerNoThrottle.cs:87-109`）。
3. **跑测试卡住时兜底**：`focus_nudge.py`，会抢焦点，只有 `run_tests` 调用。

| ID | 标题 | 做法 | 验收 |
|----|------|------|------|
| MEAS-01 | 先测后台延迟基线 | 1. Unity 在前台、在后台两种情况下，各连发 10 条 `ping`，记录 p50 和 p95<br>2. 确认 Windows 上 `QueuePlayerLoopUpdate` 对不在前台的 Editor 是否有效 | 数据记入 `RELIABILITY.zh-CN.md`。下面三项要做到什么程度，以这份数据为准 |
| AGENT-12 | 后台线程唤醒主线程 | 1. Unity 侧用 `FileSystemWatcher` 监听 inbox<br>2. 收到文件时用 `SynchronizationContext.Post` 加 `QueuePlayerLoopUpdate()` 唤醒主线程<br>3. 原有的 250ms 轮询保留作兜底 | 后台 p95 接近前台；前台单条命令延迟下降 |
| AGENT-13 | 长任务期间临时关闭降频 | 1. compile / play / exec 这类延迟 job 开始时，设 EditorPrefs：`InteractionMode=1`、`ApplicationIdleTime=0`<br>2. 原值存进 SessionState，跨 domain reload 也能还原；job 结束时还原 | Unity 在后台时 compile、play 不卡；用户原有设置不被改坏 |
| AGENT-14 | 把降频状态报出来 | 1. `state` 返回 `editor_is_focused`、`run_in_background` 和当前交互模式<br>2. 命令超时时，错误里写明原因（如 `EDITOR_THROTTLED`） | 超时时能区分是"Unity 在后台"还是"命令本身卡死" |

## P1：AI 使用体验

| ID | 标题 | 现状（依据） | 做法 |
|----|------|-------------|------|
| AGENT-16 | 结构化错误码加修复提示 | 整个代码库里 `error_code` 只出现 2 次，错误基本只有 message 字符串 | 1. 定义错误码：`NODE_NOT_FOUND`、`TYPE_NOT_FOUND`、`PLAY_MODE_REQUIRED`、`COMPILE_ERROR`、`ORPHANED`、`TIMEOUT`、`EDITOR_THROTTLED`<br>2. 加 `hint` 字段，例如找不到节点时附上同级最相近的 3 个节点名 |
| AGENT-17 | 读命令的输出要能控大小 | `prefab.read`、`scene.read`、`console` 都没有 limit / depth / filter 参数，大预制一次就是几万 token | 1. 加 `depth`、`root`（只看某个子树）、`fields`、`limit`，被截断时返回 `truncated: true`<br>2. 默认输出紧凑格式，一个节点一行<br>3. `console` 合并重复日志，堆栈只保留项目代码的帧 |
| AGENT-18 | edit 支持 dry-run 和前置条件 | 无 | 1. `dry_run`：只解析和校验 ops，返回将要发生的变更清单，不保存<br>2. op 可带 `expect` 前置条件（节点存在、组件类型匹配），不满足则整批拒绝 |
| AGENT-19 | 改动可撤销 | Editor 里一处 `Undo.` 调用都没有；写入用 `ApplyModifiedPropertiesWithoutUndo`（`HierarchyEdit.cs:324,433`） | 1. 场景编辑走 Undo API，每条命令合成一个 Undo 组<br>2. Prefab 资产保存前，把原文件备份到 `~/.ucp/backups/{cmdId}/` |
| CLI-06 | `ucp-cli restore <cmdId>` | 无 | 用 AGENT-19 的备份把资产还原 |
| CLI-07 | 可选的单工具 MCP 适配层 | 无 | 只暴露 `ucp_run(command, args)` 一个工具，命令目录交给 `help` 按需查询。MCP 客户端能接入，schema 开销只有 1 个工具 |
| CLI-08 | 文档由 `help` 生成 | README 里手写的命令表和代码对不上，也违反了 DOC_GOVERNANCE 里"不另维护命令目录"的规定 | 1. `help --format md` 输出命令卡片（参数、示例、错误码），打包成 skill 或 AGENTS 片段<br>2. `help --json` 在 CI 里生成 README 的命令表 |

## P1：延迟与吞吐

| ID | 标题 | 现状（依据） | 做法 |
|----|------|-------------|------|
| CLI-09 | host 侧改成事件驱动读 outbox | `editor-file-queue-adapter.ts:52` 按 `OUTBOX_POLL_INTERVAL_MS` 用 setTimeout 轮询 | 用 `fs.watch` 监听 outbox，轮询保留作兜底。Unity 侧对应的是 AGENT-12 |
| CLI-10 | `run-script` 跨命令流水线（取代 AGENT-09） | README 推荐的验证流程 `play → ui.snapshot → screenshot → console → stop` 要调 5 次 CLI | `ucp-cli run-script flow.json`：host 按顺序执行，任一步失败即停，最后一次性返回汇总结果。流程中的 play/stop 跨 domain reload 也要能续跑 |

## P2：功能补齐（从 unity-mcp 选着借）

| ID | 标题 | 做法 / 参考 |
|----|------|------------|
| AGENT-20 | Play Mode 下的 UI 交互 | 在现有 `ui.snapshot` 基础上加 `ui.click`、`ui.input`、`ui.wait`（等节点出现或文本变化），形成"改码 → 进 Play → 点击 → 截图核对"的闭环 |
| AGENT-05 | `test_run`（补充） | 通过 `TestRunnerApi` 回调接入延迟 job，`CompletionKind` 新增 `tests`；跑测试期间配合 AGENT-13 关闭降频。参考 unity-mcp 的 `TestJobManager.cs`、`TestRunnerNoThrottle.cs` |
| AGENT-01 / AGENT-06 | `build` 落地、`package_*` | 都是异步 API，同样走延迟 job |

## P2：工程化

| ID | 标题 | 做法 |
|----|------|------|
| CLI-11 | 发布与版本声明 | UPM 支持 git URL 加 `?path=/com.air.ucp-agent` 安装；npm 发包或提供 `npx` 用法；在 `package.json` 里写明 `"unity"` 最低版本 |
| CLI-12 | 清理过期会话 | session 文件写入 pid，host 发现进程已退出就标为 dead；多开 Unity 时支持 `--project` 显式选择 |
| CLI-01 | CI 矩阵（补充） | 至少在一个 Unity LTS 版本上跑集成测试，覆盖 reload 续跑、play/stop、Unity 在后台三类场景 |

## P3：可选

| ID | 标题 | 说明 |
|----|------|------|
| AGENT-21 | 抢焦点唤醒（opt-in） | 参考 `focus_nudge.py`：卡住超过 3 秒时把 Unity 切到前台，再切回原窗口，间隔逐次加长，有次数上限。会打断用户操作，**只给无人值守的 CI 用，默认关闭** |

## 不做

- 材质、Shader、ProBuilder、VFX 等大批专用命令（AGENT-07 维持 P3）。Renderer 上的材质属性已能用 edit ops 改。
- `script_*`（AGENT-08）。AI 改脚本用文件工具加 `compile` 就够了。
- 默认开启抢焦点唤醒。

## 推荐顺序

1. AGENT-11、CLI-05、AGENT-15：改动小，挡住的是真实风险。
2. MEAS-01，然后按数据决定 AGENT-12、AGENT-13、AGENT-14 做到什么程度。
3. AGENT-16、AGENT-17、CLI-09：对每轮的 token 和延迟影响最大。
4. AGENT-20 加 CLI-10：和 unity-mcp 拉开差距的部分。
5. 其余按需排期。
