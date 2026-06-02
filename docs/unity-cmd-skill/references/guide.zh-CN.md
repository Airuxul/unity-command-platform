# unity-cmd 操作指南（中文）

本指南用于统一 `unity-cmd` 的调用方式，避免 profile、scope 和 host 语义混淆。

## 核心原则

- 命令是否可执行由 `CommandHostScope` 决定，不是由调用端“自动路由”决定。
- `editor_play` / `player` 只接受 `runtime` 或 `any` 命令。
- `editor` 接受 `editor` 或 `any` 命令。
- `compile` 是 Editor 命令：发到 `editor_play` 必须拒绝，发到 `editor` 才是正确路径。

## profile 与 host-kind

| Profile | host-kind | 默认端口 | 典型用途 |
|---------|-----------|----------|----------|
| `editor` | `editor` | `6547` | 编译、进出 Play、截图、Profiler、Console |
| `editor-play` | `editor_play` | `6794` | Editor Play 期间的 runtime 命令 |
| `package-play` | `player` | `6795` | Development Build runtime 命令 |

## 初始化 profile

```bash
unity-cmd profile list
unity-cmd profile create editor --host 127.0.0.1 --port 6547 --host-kind editor --no-verify
unity-cmd profile create editor-play --host 127.0.0.1 --port 6794 --host-kind editor_play --no-verify
unity-cmd profile create package-play --host 127.0.0.1 --port 6795 --host-kind player --no-verify
```

## 常用调用示例

```bash
# Editor
unity-cmd --profile editor ping
unity-cmd --profile editor list
unity-cmd --profile editor compile --timeout 20000
unity-cmd --profile editor play
unity-cmd --profile editor stop

# Editor Play runtime
unity-cmd --profile editor-play ping
unity-cmd --profile editor-play echo --message hello

# Development Build runtime
unity-cmd --profile package-play ping
unity-cmd --profile package-play echo --message hello
```

## 作用域判定（必须遵守）

| 目标 profile | 允许 scope | 拒绝 scope |
|--------------|-----------|-----------|
| `editor` | `editor`, `any` | `runtime` |
| `editor-play` | `runtime`, `any` | `editor` |
| `package-play` | `runtime`, `any` | `editor` |

## 失败即中断的推荐流程

1. `list` 拉取 catalog 并解析命令可用性。
2. 若命令 scope 与目标 profile 不匹配，直接报错并停止。
3. `ping` 检查目标实例可达。
4. 再执行命令。

## Unity C# 变更后的强制校验

每次修改 `packages/**/*.cs` 后，执行：

```powershell
.\tools\unity-compile-loop.ps1 -IncludeWarnings -ConsoleLines 400
```

该脚本固定使用 `editor` profile，执行一次：

1. `unity-cmd --profile editor compile --timeout 20000`
2. `unity-cmd --profile editor console --type error,warning --lines <N>`

若有错误，先修复再重复执行，直到清零。

## 常见错误与处理

| 错误码 | 含义 | 建议 |
|-------|------|------|
| `NO_PROFILE` | 未提供 profile | 先创建 profile 或设置 `UNITY_CMD_PROFILE` |
| `NO_INSTANCE` | profile 端点不可达 | 确认 Unity 已启动且端口正确 |
| `SCOPE_MISMATCH` | 命令与 host 不匹配 | 改用正确 profile，或改命令 |
| `EDITOR_NOT_READY` | Editor 正在编译/重载 | 稍后重试，先看 console |

## 维护要求

- 文档与代码中的命名保持一致：使用 `CommandHostScope`。
- 示例命令优先使用 `--profile editor` 明确目标，避免歧义。
- 文档中的命令行和代码块必须可直接复制执行。
