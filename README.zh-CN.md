# UCP（Unity Command Platform）

[English](README.md)

从命令行、CI 或 Agent 操控 Unity Editor 和 Play Mode。Edit Mode 不在 Unity 里开 HTTP 端口。

| 包 | 职责 |
|----|------|
| [ucp-cli](ucp-cli/) | Node：`ucp-cli` 和 `ucp-host` |
| [com.air.ucp-agent](com.air.ucp-agent/) | Unity UPM：FileQueue Agent 和命令 |

使用步骤：[docs/GUIDE.zh-CN.md](docs/GUIDE.zh-CN.md)。命令名单：`ucp-cli help`。结构：[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。重载和投递：[docs/RELIABILITY.zh-CN.md](docs/RELIABILITY.zh-CN.md)。

## 它做什么

`ucp-cli` 连本机 `ucp-host`（`127.0.0.1:6610`）。host 会自己启动。Editor session 是 Unity Agent 写下的 `~/.ucp/sessions/{projectId}.json`，命令经 `~/.ucp/queues/` 里的文件进出。runtime session 是 Play Mode 里的监听；只有 `ucp-cli targets set --type runtime` 配过、并且 `/health` 探活成功时才会出现。只点 Play 不会多出这条 session。

只开着一个 Editor 时会自动选中。开了多个工程就加 `--project`。`--session-type` 是 `editor` 或 `runtime`；不写则选 Editor。

每条结果都是 `{ ok, data, message, error_code, hint, timing }`。只有 `ok` 为 true 时退出码才是 0。还没进 Unity 的失败用带 `host_` 的码（`host_timeout`、`host_unknown_command` 等）。Unity 自己的等待超时仍是 `timeout`。

`gameobject_*` 和 `component_*` 只有一份实现。带 `prefab` 就改那个预制体资产，不带就改打开的场景。`group` 把几条命令当一条跑：一步失败全部撤回（`rollback=all`），或只撤失败的那步（`rollback=failed`），中间有 compile、Play 也一样。

会触发域重载的命令（`compile`、`play`、`stop`、`test_run`、`package_add`、`package_remove`，以及 `ui_wait`）仍然只返回一次。细节在可靠性文档里。

## 安装

需要 Node 20 或更高。

```powershell
cd ucp-cli
.\setup.ps1
ucp-cli ping
```

不想全局链接：`node ucp-cli/bin/ucp-cli.js ping`。`setup.ps1` 之后仍然找不到 `ucp-cli`，把 `npm prefix -g` 加进 `PATH`，再开一个终端。

Unity 的 `Packages/manifest.json`：

```json
"com.air.ucp-agent": "file:C:/path/to/unity-command-platform/com.air.ucp-agent"
```

打开工程并等编译结束，然后 `ucp-cli ping` 应成功。

CLI 和 Unity 包一起升级，这台机器上的每个 `ucp-cli` 也一起升级。0.2 的 CLI 连上较新的 host 会得到 `unauthorized`。参数改名见 [docs/CHANGELOG_AGENT.md](docs/CHANGELOG_AGENT.md#upgrading-from-03)。

## 测试

```powershell
cd ucp-cli
npm run verify
```

连着打开的 Editor 跑集成测试要设 `UCP_EDITOR_INTEGRATION=1`。见 [ucp-cli/README.md](ucp-cli/README.md) 和 [docs/MAINTENANCE.md](docs/MAINTENANCE.md#node-development)。
