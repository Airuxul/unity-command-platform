# UCP FileQueue 可靠性说明

**版本：** 0.5（agent 0.5.0 / ucp-cli 0.3.0）
**日期：** 2026-11-05
**范围：** `ucp-cli` + `ucp-host` + `com.air.ucp-agent`

---

## 1. 设计目标

CLI、Host、Unity 三者生命周期解耦：

| 组件 | 崩溃 / 退出的影响 |
|------|------------------|
| `ucp-cli` | 只影响当次调用。它提交的命令仍会执行完；调用方 10 秒不来取结果，还没派发的命令会被丢弃（`caller_disconnected`） |
| `ucp-host` | 内存里的命令状态丢失，正在等的调用以 `host_lost` 失败，提示命令可能已在 Unity 里执行过。下次调用 CLI 会自动拉起新 host。留在 inbox 里、已过 deadline 的命令 Unity 不会再执行 |
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
- 命令带着 host 的 deadline（Unix 毫秒）。Agent 认领时已过 deadline 的命令不执行；同步命令执行完时已过 deadline（例如 `exec` 阻塞主线程超过超时），结果不写 outbox。这两种结果都不会有人来读。
- 超过一小时的 inbox / outbox 文件由 host 定期清理，作为兜底（例如 0.3 的 agent 迟到写出的结果）。

**超时：** deadline 从提交时开始算，排队时间也计入。排在长命令后面的命令到了自己的 deadline 就以 `timeout` 结束，不等前面的命令完成，也不会再派发给 Unity。没传 `--timeout` 时用命令自己声明的默认值（`build` 30 分钟、`test_run` 10 分钟、`package_*` 5 分钟、`compile` / `refresh` 180 秒、`play` / `stop` 120 秒，其余 30 秒）。

**排队：** 同一个 Editor 的命令按提交顺序逐条执行，Editor 和它的 Play Mode runtime 之间并行。只读命令（`ping`、`state`、`console`、`help`、`echo`，由 agent 在 session 文件的 `readOnlyCommands` 里列出）不排队：`test_run` 期间、`compile` 编译期间都能答复，不必等前面的命令结束。域重载本身是例外：Unity 重载时不 tick，期间发来的命令要等重载结束（实测一次 compile 的重载让 `state` 等了约 6 秒）。

**Editor 忙：** 导入资源时 Editor 不 tick，心跳会超过 30 秒。只要 session 文件里的 pid 还活着，host 就等它就绪，直到调用方超时；pid 已退出才立即失败。

**读 session 文件：** agent 每 2 秒替换一次 session 文件，替换瞬间读到"文件不存在"或读不全，在 Windows 上约每 1000 次读取出现 1 次（实测：20 秒内约 14 万次读取，137 次）。host 在 1 秒内保留该 Editor 上一次读到的状态，不把它当成已退出。

**runtime：** runtime 不会"忙"，host 不等它就绪。runtime 状态每 2 秒探测一次；没有就绪的 runtime 时，host 先补探一次（刚进 Play Mode、探测还没跟上的情况），仍没有才以 `no_ready_session` 失败。补探最多花探测超时的 3 秒：本机 runtime 不在时立即返回，`targets.json` 里配了一台不响应的设备时要等满 3 秒。runtime 的探测和 Editor 的 session 文件分开读，这台设备不会拖慢 Editor 状态的更新。

---

## 3. 域重载（Domain Reload）

编译、进 Play Mode、跑 PlayMode 测试、增删带脚本的包都会触发域重载，Unity 进程内的静态状态清空。

1. 延迟命令（`compile`、`refresh compile=true`、`play`、`stop`、`test_run`、`package_add`、`package_remove`、`ui_wait`）运行前先建 job，同步写入 `SessionState` 和 `~/.ucp/jobs/{projectId}/{cmdId}.json`。
2. 重载后 `EditorJobStateManager` 在第一次轮询 inbox 之前读回 job。已有 job 的命令不会被再次执行。
3. 每个 editor tick 由 completion policy 判断 job 是否完成，完成后写 outbox、删除 job。
4. 没有 policy 的 job（`started`，由回调完成）在重载后立即以 `orphaned` 失败，因为它的回调已随旧 domain 消失。
5. 超过 deadline 的 job 直接删除、不写结果：host 按同一个 deadline 计时，已经回了 `timeout`。

**从 0.3 升级：** 加载新代码的那次 `compile` 是旧 agent 建的 job，由新 agent 接着完成并返回一次结果。0.3 留在 `SessionState` 里的已完成 job 在第一次重载时全部丢弃，不会重复投递。用例 `delivery.test.ts` "finishes a 0.3 compile after the upgrade reload…" 把 0.3 格式的 job 记录写进 `SessionState` 再触发重载，断言 outbox 里只有那次 compile 的结果；去掉修复后它会多出 0.3 已答复过的 `play` 结果而失败。

---

## 4. 延迟实测

由 `tests/integration/background.test.ts` 自动测得：测试自己把 Unity 最小化，跑完再还原窗口。Unity 在后台时 editor tick 每秒 10.3–10.7 次，即约每 100 ms 一次。`ping` 从交给 Unity 到读到结果（`unity_ms`）：

| inbox 检查方式 | 连发 20 条：中位 / 最大 | 随机间隔 30 条：中位 / 最大 |
|----------------|-------------------------|-----------------------------|
| 每 250 ms 一次（0.3） | 295 / 321 ms | 约 200 / 318 ms |
| 每个 editor tick 一次（0.5） | 96 / 127 ms | 约 75 / 140 ms |

连发时每条都刚好错过一个 tick，要等满一个；随机到达平均等半个。用例的断言是：tick 每秒 5–20 次，连发中位 < 200 ms，随机间隔中位 < 120 ms。把检查间隔改回 250 ms 后两条延迟断言都失败，所以它们守住了这项优化。

前台没有测：要让 Unity 获得焦点只能抢用户的焦点。命令超时时，`hint` 会根据 session 的 `focused` 字段说明 Unity 当时是否在后台。

---

## 5. ucp-host 可用性

| 场景 | 行为 |
|------|------|
| Host 未运行 | `ucp-cli` 自动拉起 `ucp-host` |
| Host 比 CLI 旧 | `/health` 返回构建号。CLI 发现空闲的旧 host，用 `POST /shutdown` 请它退出：host 在处理请求的同一个循环里再查一次是否空闲，有命令在跑就拒绝（409）并保留；同意退出后不再接受新命令（`host_restarting`），CLI 会自己重发。0.5 之前的 host 没有这个接口，直接结束进程 |
| 0.2 的 `ucp-cli` 连 0.3 的 host | 所有调用都以 `unauthorized` 失败（不带 token），旧 CLI 也不会重启新 host。同一台机器上的 `ucp-cli` 要一起升级 |
| 端口占用 | 检查 `host.json` / `host.lock`；必要时结束陈旧的 `node … ucp-host.js` |
| `no_ready_session` | Unity 未打开、Agent 未编译，或 runtime session 不在 Play Mode |
| `editor_not_running` | session 文件里的 Unity 进程已退出 |

host 自己判定的失败（上面几种、`timeout`、`unknown_command`、`command_failed`）同样带 `message` 和 `hint`。完整错误码表见 [CHANGELOG_AGENT.md](CHANGELOG_AGENT.md#upgrading-from-03)。

---

## 6. 验收

验收只看自动化测试，不需要手工操作，也不需要手动最小化 Unity。前提是 Unity 打开测试工程并编译完成：

```powershell
cd ucp-cli
$env:UCP_EDITOR_INTEGRATION="1"; $env:UCP_EDITOR_SLOW="1"; $env:UCP_TEST_PROJECT="C:/path/to/UnityProject"
npm run check; npm test; npm run test:integration
```

每个用例结束都断言 `~/.ucp/queues/{projectId}/{inbox,outbox}` 和 `~/.ucp/jobs/{projectId}` 为空。本文的每条保证都有对应用例；每条修复的用例都确认过：去掉修复后用例失败。

| 保证 | 用例 |
|------|------|
| 只执行一次、不留文件 | `tests/integration/delivery.test.ts`（自动把 Unity 最小化后跑） |
| 过 deadline 不执行、不写结果；不带 deadline 的旧 host 命令照常执行 | `delivery.test.ts` "does not run a command whose deadline passed…"、"a command that outruns its timeout…"、"still runs a command from a host that sends no deadline" |
| 从 0.3 升级 | `delivery.test.ts` "finishes a 0.3 compile after the upgrade reload…" |
| `started` job 被重载打断时立即 orphaned | `delivery.test.ts` "fails a job its domain reload cut off as orphaned, at once" |
| 只读命令不排队；域重载期间要等 | `test-run.test.ts` "answers state and ping from another caller…"、`delivery.test.ts` "answers state while a compile runs…"、`tests/unit/host/command-runner.test.ts` |
| 排队命令按自己的 deadline 超时、host 层失败带 message | `tests/unit/host/command-runner.test.ts` |
| 替换中的 session 文件、无响应的 runtime 设备、runtime 补探 | `tests/unit/host/session-directory.test.ts`、`command-runner.test.ts` |
| `/shutdown`、对旧 host 结束进程、`host_lost`、`host_restarting` / `host_unreachable` 自动重发、401 提示 | `tests/unit/host/host-server-origin.test.ts`、`tests/integration/ping-e2e.test.ts`、`tests/unit/cli/ensure-host.test.ts` |
| 后台 tick 频率与 `ping` 延迟、后台 compile 导入新脚本 | `tests/integration/background.test.ts` |

没有自动化测试的只有一项："0.2 的 CLI 不带 token"是 master 代码的事实（它的请求里没有 `X-UCP-Token`）。用例 `host-server-origin.test.ts` 确认的是：不带 token 的请求一律 401，并提示升级 ucp-cli。
