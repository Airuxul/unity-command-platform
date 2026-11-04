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
- Editor 侧 79 个命令：prefab / gameobject / component / scene / asset / selection + 编辑器控制
- `EditTargetScope`：内容类命令一份实现同时支持"改场景"和"改 Prefab 资产"
- 完整命令目录用 `ucp-cli run help` 查，仓库不再维护命令表

### 待办

| ID | 优先级 | 标题 | 说明 |
|----|--------|------|------|
| AGENT-01 | P1 | `build` 接入 BuildPipeline | 当前为骨架，需真实构建流程。 |
| AGENT-02 | P1 | Runtime Agent HTTP | 按架构 §10 实现 Player 侧适配（独立阶段）。 |
| AGENT-03 | P2 | 域重载后延迟任务审计 | `EditorJobLedger` 与 outbox 一致性。 |
| AGENT-04 | P2 | `profiler` 参数绑定 | 修复/补全参数校验与文档。 |
| AGENT-05 | P2 | `test_run` 测试运行 | 需要 asmdef 引用 `com.unity.test-framework`，且测试是异步的，得走延迟 job 管线。 |
| AGENT-06 | P2 | `package_*` 包管理 | `UnityEditor.PackageManager.Client` 是异步 API，同样需要延迟 job。 |
| AGENT-07 | P3 | `material_*` / `shader_*` / `texture_*` | 资产类型专用命令。Renderer 上的材质**属性**已经能用 `component_set` 改，这批是用来创建/编辑资产本身的。 |
| AGENT-08 | P3 | `script_*` C# 脚本增删改查 | 本次故意不做：AI 改脚本用普通文件工具 + `compile` 更顺手。 |
| AGENT-09 | P3 | `batch` 批量命令 | 对齐 unity-mcp 的 `batch_execute`，一次往返跑多条。只有在实测单条延迟成为瓶颈时才值得做。 |
| AGENT-10 | P3 | `prefab_replace` 保留 overrides | 当前用"手工重新实例化"实现（不依赖 2022.2+ 的 `ReplacePrefabAssetOfPrefabInstance`），因此实例 overrides 会丢。要么手动搬运，要么用版本宏切到新 API。 |

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
