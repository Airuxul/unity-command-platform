# UCP FileQueue 可靠性说明

**版本：** 0.5（agent 0.5.0 / ucp-cli 0.3.0）
**日期：** 2026-11-05
**范围：** `ucp-cli` + `ucp-host` + `com.air.ucp-agent`

---

## 1. 设计目标

CLI、Host、Unity 三者生命周期解耦：

| 组件 | 崩溃 / 退出的影响 |
|------|------------------|
| `ucp-cli` | 只影响当次调用。它提交的命令仍会执行完；调用方 10 秒不来取结果，还没派发的命令会被丢弃 |
| `ucp-host` | 内存里的命令状态丢失，正在等的调用失败。下次调用 CLI 会自动拉起新 host；Unity 写出的孤儿结果一小时后被清理 |
| Unity Editor | 域重载不丢命令（见第 3 节）。进程退出时，正在等它的命令以 `editor_not_running` 立即失败 |

Edit Mode 下 Editor **不开** HTTP 端口，通信全走 FileQueue（`inbox` / `outbox` JSON 文件）。

---

## 2. 一条命令的生命周期

| 步骤 | 谁 | 做什么 |
|------|----|--------|
| 1 | ucp-host | 写 `inbox/{cmdId}.json`（先写临时文件再原子改名） |
| 2 | Agent | 把它改名为 `{cmdId}.claimed`，改名成功才执行 |
| 3 | Agent | 写 `outbox/{cmdId}.json`，删除 `.claimed` |
| 4 | ucp-host | 读到结果，删除 inbox 和 outbox 文件 |

**不变式：**
- 一条命令最多执行一次。host 超时撤回时只删 `inbox/{cmdId}.json`；改名认领和删除只有一方能成功，所以要么撤回、要么执行。
- 正常结束后 `inbox/`、`outbox/`、`jobs/` 都是空的。0.3 及以前每条命令都会在三处各留一个文件，Editor 每次轮询都要重读一遍，`ping` 要约 4 秒。
- 超过一小时的 inbox / outbox 文件（调用方早已放弃）由 host 定期清理。

**超时：** deadline 从提交时开始算，排队时间也计入。排队期间过期的命令不会派发给 Unity。没传 `--timeout` 时用命令自己声明的默认值（`build` 30 分钟、`test_run` 10 分钟、`package_*` 5 分钟、`compile` / `refresh` 180 秒、`play` / `stop` 120 秒，其余 30 秒）。

**Editor 忙：** 导入资源时 Editor 不 tick，心跳会超过 30 秒。只要 session 文件里的 pid 还活着，host 就等它就绪，直到调用方超时；pid 已退出才立即失败。runtime session 不等待。

---

## 3. 域重载（Domain Reload）

编译、进 Play Mode、跑 PlayMode 测试、增删带脚本的包都会触发域重载，Unity 进程内的静态状态清空。

1. 延迟命令（`compile`、`refresh compile=true`、`play`、`stop`、`test_run`、`package_add`、`package_remove`、`ui_wait`）运行前先建 job，同步写入 `SessionState` 和 `~/.ucp/jobs/{projectId}/{cmdId}.json`。
2. 重载后 `EditorJobStateManager` 在第一次轮询 inbox 之前读回 job。已有 job 的命令不会被再次执行。
3. 每个 editor tick 由 completion policy 判断 job 是否完成，完成后写 outbox、删除 job。
4. 没有 policy 的 job（`started`，由回调完成）在重载后立即以 `orphaned` 失败，因为它的回调已随旧 domain 消失。
5. 超过 deadline 的 job 直接删除、不写结果：host 按同一个 deadline 计时，已经回了 `timeout`。

**从 0.3 升级：** 加载新代码的那次 `compile` 是旧 agent 建的 job，由新 agent 接着完成并返回一次结果。0.3 留在 `SessionState` 里的已完成 job 在第一次重载时全部丢弃，不会重复投递。2026-11-05 实测通过（master `7ce511b` → 0.5.0）。

---

## 4. 延迟实测

Unity 最小化，连发 20 条 `ping`，从交给 Unity 到读到结果：

| 版本 | 中位数 | 最大值 |
|------|--------|--------|
| 每 250 ms 轮询 inbox | 147 ms | 268 ms |
| 每个 editor tick 检查 inbox（0.5） | 25 ms | 86 ms |

Unity 在后台时 editor tick 约每秒 10 次，前台最多每秒 50 次（agent 自己限的上限）。命令超时时，`hint` 会根据 session 的 `focused` 字段说明 Unity 当时是否在后台。

---

## 5. ucp-host 可用性

| 场景 | 行为 |
|------|------|
| Host 未运行 | `ucp-cli` 自动拉起 `ucp-host` |
| Host 比 CLI 旧 | `/health` 返回构建号；空闲时 CLI 自动重启旧 host，有命令在跑时不动它 |
| 端口占用 | 检查 `host.json` / `host.lock`；必要时结束陈旧的 `node … ucp-host.js` |
| `no_ready_session` | Unity 未打开、Agent 未编译，或 runtime session 不在 Play Mode |
| `editor_not_running` | session 文件里的 Unity 进程已退出 |

---

## 6. 验收清单

1. 打开 Unity，等 `com.air.ucp-agent` 编译完成。
2. `ucp-cli ping` → `ok: true`。
3. `ucp-cli run compile` 成功；之后 `~/.ucp/queues/{projectId}/{inbox,outbox}` 和 `~/.ucp/jobs/{projectId}` 为空。
4. `ucp-cli run state` 含 `agent_state`、`play_mode`、`tests_running`。
5. 结束 ucp-host 进程后再 `ucp-cli ping`，Host 自动拉起。

集成测试：`cd ucp-cli && $env:UCP_EDITOR_INTEGRATION="1"; npm run test:integration`；其中 `delivery.test.ts` 覆盖本文的不变式，建议在 Unity 最小化时也跑一遍。
