# UCP FileQueue 可靠性说明

**版本：** 1.0（UCP）  
**日期：** 2026-06-16  
**范围：** `ucp-cli` + `ucp-host` + `com.air.ucp-agent`  
**替代：** 旧版 `EDITOR_SERVER_RELIABILITY.zh-CN.md`（HTTP connector 方案已废弃）

---

## 1. 设计目标

UCP 将 **CLI、Host、Unity** 生命周期解耦（见 [01_Architecture.md](../../../01_Architecture.md)）：

| 组件 | 崩溃 / 退出影响 |
|------|----------------|
| `ucp-cli` | 仅当次命令失败；可重试 |
| `ucp-host` | 磁盘队列保留；重启后继续 |
| Unity Editor | inbox/outbox 在 `~/.ucp/`；重载后不丢命令文件 |

Editor **禁止** 常驻 HTTP Server；通信用 **FileQueue**（`inbox` / `outbox` JSON 文件）。

---

## 2. 磁盘不变式

| 路径 | 写入方 | 说明 |
|------|--------|------|
| `~/.ucp/sessions/{projectId}.json` | Unity Agent | `status`: ready / busy / offline |
| `~/.ucp/queues/{projectId}/inbox/{cmdId}.json` | ucp-host | 待执行命令 |
| `~/.ucp/queues/{projectId}/outbox/{cmdId}.json` | Unity Agent | 完成结果 |
| `~/.ucp/host.json` | ucp-host | pid、port |

**规则：** outbox 存在则 inbox 项视为已消费；Agent 轮询跳过已有 outbox 的 id。

---

## 3. 域重载（Domain Reload）

编译 / 脚本重载时：

1. Unity 进程内静态状态清空 — **正常且预期**。
2. `~/.ucp/` 下文件 **不受影响**。
3. Agent 重新初始化后继续 poll inbox。
4. **延迟命令**（`compile` / `play` / `stop`）通过 `EditorJobStateManager` + `PendingDeferredCommands` 在重载后写回 outbox。

若重载期间命令正在执行，可能超时；CLI 侧可增大 `--timeout` 或重试 `ucp-cli run …`。

---

## 4. ucp-host 可用性

| 场景 | 行为 |
|------|------|
| Host 未运行 | `ucp-cli` 通过 `ensureUcpHost()` 自动 spawn `ucp-host` |
| 端口占用 | 检查 `host.json` / `host.lock`；必要时结束陈旧 `node … ucp-host.js` |
| `no_ready_session` | Unity 未打开、Agent 未编译、或 session 非 ready |

**注意：** 勿从已删除的旧路径启动 host（如 `unity-cmd/bin/ucp-host.js`），否则会占用目录导致无法清理。

---

## 5. 验收清单

1. 打开 Unity，等待 `com.air.ucp-agent` 编译完成。
2. `ucp-cli ping` → `ok: true`。
3. 触发脚本编译，编译完成后再次 `ucp-cli ping` 成功。
4. `ucp-cli run state` 含 `agent_state`、`play_mode`。
5. 关闭 ucp-host 进程后再次 `ucp-cli ping`，Host 自动拉起。

集成测试：`cd ucp-cli && $env:UCP_EDITOR_INTEGRATION="1"; npm run test:integration:editor`。

---

## 6. 与旧 HTTP 方案对比

| 维度 | 旧 connector (:6547) | UCP FileQueue |
|------|---------------------|---------------|
| Editor 监听 | HTTP Server | 无（仅 poll 文件） |
| 就绪判断 | instances + /health | `~/.ucp/sessions/*.json` |
| CLI 配置 | profiles | 无 profile，`--project` 可选 |
| 数据目录 | `~/.unity-cmd/` | `~/.ucp/` |
