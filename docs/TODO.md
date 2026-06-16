# TODO — UCP (`packages/ucp`)

**Last Updated:** 2026-06-16 · **Scope:** follow-ups (English)

> **Chinese:** [../TODO.zh-CN.md](../TODO.zh-CN.md)

> **Layout:** `ucp-cli` (Node) + `com.air.ucp-agent` (Unity UPM).  
> **Out of scope:** `GameRuntime` composition, product UI, duplicate HTTP in game-core.

---

## `com.air.ucp-agent` (AGENT-)

### Baseline

- Editor FileQueue poll (`~/.ucp/queues/{projectId}/inbox|outbox`)
- Session file (`~/.ucp/sessions/{projectId}.json`)
- `CliCommand` discovery + `UcpCliCommandHandler`
- Deferred: `compile`, `play`, `stop`

### TODO

| ID | Pri | Title | Description |
|----|-----|-------|-------------|
| AGENT-01 | P1 | Wire `build` | Replace skeleton with real BuildPipeline. |
| AGENT-02 | P1 | Runtime Agent HTTP | Player-side adapter per architecture §10. |
| AGENT-03 | P2 | Post-reload job audit | `EditorJobLedger` / outbox consistency. |
| AGENT-04 | P2 | `profiler` params | Fix binding and document args. |

### Do not assign here

| Topic | Owner |
|-------|--------|
| CLI / Host scheduling | `ucp-cli` |
| Game UI / entities | `com.air.unity-game-core` |

---

## `ucp-cli` (CLI-)

### Baseline

- Single npm package: `ucp-cli` + `ucp-host`
- TypeScript, vitest, FileQueue e2e
- `UCP_EDITOR_INTEGRATION` live tests

### TODO

| ID | Pri | Title | Description |
|----|-----|-------|-------------|
| CLI-01 | P1 | CI integration matrix | Run `test:integration:editor` when Editor available. |
| CLI-02 | P1 | Runtime HTTP adapter | Pair with AGENT-02 for Player commands. |
| CLI-03 | P2 | SQLite queue adapter | Optional per architecture §5.1. |
| CLI-04 | P2 | Global install docs | `npm link` / PATH on Windows. |

### Do not assign here

| Topic | Owner |
|-------|--------|
| Unity main-thread execution | `com.air.ucp-agent` |
| Business command logic | `Editor/Commands/` |
