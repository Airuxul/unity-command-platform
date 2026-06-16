# 待办 — UCP（`packages/ucp`）

**最后更新：** 2026-06-16 · **范围：** UCP 栈后续优化（中文）

> **仓库结构：** `ucp-cli`（Node）+ `com.air.ucp-agent`（Unity UPM）。  
> **英文条目：** [`docs/TODO.md`](docs/TODO.md)

---

## `com.air.ucp-agent`（AGENT-）

### 现有能力

- Editor FileQueue 轮询（`~/.ucp/queues/{projectId}/inbox|outbox`）
- Session 注册（`~/.ucp/sessions/{projectId}.json`）
- `CliCommand` 自动发现 + `UcpCliCommandHandler` 桥接
- 延迟命令：`compile` / `play` / `stop` + `EditorJobStateManager`
- 命令：`ping`, `echo`, `state`, `console`, `refresh`, `screenshot`, `profiler`, `manage`, `menu`, `reserialize`, `compile`, `play`, `stop`, `exec`, `build`（骨架）

### 待办

| ID | 优先级 | 标题 | 说明 |
|----|--------|------|------|
| AGENT-01 | P1 | `build` 接入 BuildPipeline | 当前为骨架，需真实构建流程。 |
| AGENT-02 | P1 | Runtime Agent HTTP | 按架构 §10 实现 Player 侧适配（独立阶段）。 |
| AGENT-03 | P2 | 域重载后延迟任务审计 | `EditorJobLedger` 与 outbox 一致性。 |
| AGENT-04 | P2 | `profiler` 参数绑定 | 修复/补全参数校验与文档。 |

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

### 待办

| ID | 优先级 | 标题 | 说明 |
|----|--------|------|------|
| CLI-01 | P1 | CI 集成矩阵 | Editor 打开时跑 `test:integration:editor`。 |
| CLI-02 | P1 | Runtime HTTP 适配器 | 对接 Player Agent（与 AGENT-02 配对）。 |
| CLI-03 | P2 | SQLite Editor 适配器 | 架构可选方案，低优先级。 |
| CLI-04 | P2 | `ucp-cli` 全局安装文档 | Windows PATH / `npm link` 排错。 |

### 请勿在本包实现

| 主题 | 归属 |
|------|------|
| Unity 主线程 API | `com.air.ucp-agent` |
| 业务命令逻辑 | `Editor/Commands/` |
