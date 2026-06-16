# UCP（Unity Command Platform）

[English](README.md)

**Unity 命令平台** — CLI、Command Host、Unity Agent 三层解耦。

| 包 | 职责 |
|----|------|
| [ucp-cli](ucp-cli/) | Node：`ucp-cli` 命令行 + `ucp-host` 守护进程 |
| [com.air.ucp-agent](com.air.ucp-agent/) | Unity UPM：Editor FileQueue Agent + 命令实现 |

命名：[CONVENTIONS.md](CONVENTIONS.md) · 文档：[docs/](docs/)

## 快速开始

```powershell
cd ucp-cli
.\setup.ps1

ucp-cli ping
ucp-cli run state
ucp-cli host status
```

Unity `manifest.json`：

```json
"com.air.ucp-agent": "file:../CustomPackages/packages/unity-command-platform/com.air.ucp-agent"
```

打开 Unity 工程并等待编译完成，Agent 会写入 `~/.ucp/sessions/{projectId}.json`。

## 测试

```powershell
cd ucp-cli
npm run verify

$env:UCP_EDITOR_INTEGRATION="1"
npm run test:integration:editor
```

## 请求路径

```text
ucp-cli → ucp-host (:6610) → ~/.ucp/queues/.../inbox
         → com.air.ucp-agent 轮询 → CliCommand → outbox → ucp-cli
```

无 Unity 侧 HTTP；无 `~/.unity-cmd/` profile。
