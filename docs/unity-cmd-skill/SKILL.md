---
name: unity-cmd
description: Operate Unity via unity-cmd (Editor edit mode or runtime Play/build). Use when the user asks to run actions in the Unity Editor or at runtime, refresh the command cache, or work on unity-cmd/com.air.unity-connector in this repo.
---

# unity-cmd

Node CLI →HTTP →**com.air.unity-connector**. Command catalog from Unity `POST /list` —**never invent flags**.

## Install

Copy `docs/unity-cmd-skill/` →`.cursor/skills/unity-cmd/`. See [README.md](README.md).

## 阅读顺序（Agent）

1. **本文** —五条前提、流程概览、profile 路由  
2. **[references/guide.zh-CN.md](references/guide.zh-CN.md)** —阶段表、catalog 字段、错误码、示例（唯一详述）

## 五条前提（违反则说明原因后中断对话）

| # | 要点 |
|---|------|
| 1 | 缺 `editor` / `editor-play` profile →按指引创建 |
| 2 | 当前 profile 不可用→中断 |
| 3 | 先 `list`；若 catalog 过期则重新 `list`；按 `commands` 匹配。无匹配则列出 2 个相关命令后中断 |
| 4 | catalog 未过期但未命中：`list --refresh-catalog` 后再匹配；仍无则中断 |
| 5 | `ping` / 远程命令 / 截图校验失败 →中断 |

**中断** = 停止本轮；不切换 profile、不用 MCP、不臆造命令、不硬跑远程。

## 流程概览

```text
阶段零 profile 补齐 →阶段一 list + 匹配 →阶段二 ping →阶段三 执行 →阶段四 截图校验（若需要）
```

步骤表、catalog 字段、Play 串联：见 [guide.zh-CN.md · 操作流程](references/guide.zh-CN.md#操作流程)。

## Profile 路由

| 任务 | Profile |
|------|---------|
| compile, play, stop, console, screenshot, profiler, state | `editor` |
| runtime `echo`（Editor 播放中） | `editor-play` |
| runtime（Development Build） | `package-play` |

`play` / `stop` 仅用 **`editor`**。播放中截图仍用 **`editor`**。
