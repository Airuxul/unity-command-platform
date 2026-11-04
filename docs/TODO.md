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
- 79 Editor commands: prefab / gameobject / component / scene / asset / selection + editor control
- `EditTargetScope` — content commands run against a scene or a prefab asset from one implementation

### TODO

| ID | Pri | Title | Description |
|----|-----|-------|-------------|
| AGENT-01 | P1 | Wire `build` | Replace skeleton with real BuildPipeline. |
| AGENT-02 | P1 | Runtime Agent HTTP | Player-side adapter per architecture §10. |
| AGENT-03 | P2 | Post-reload job audit | `EditorJobLedger` / outbox consistency. |
| AGENT-04 | P2 | `profiler` params | Fix binding and document args. |
| AGENT-05 | P2 | `test_run` | Test Runner commands. Needs a `com.unity.test-framework` asmdef reference and the deferred-job pipeline, since runs are async. |
| AGENT-06 | P2 | `package_*` | Package Manager list/add/remove. `UnityEditor.PackageManager.Client` is async — same deferred-job requirement. |
| AGENT-07 | P3 | `material_*` / `shader_*` / `texture_*` | Asset-type-specific commands. `component_set` already covers material *properties* on a renderer; these would cover creating and editing the assets. |
| AGENT-08 | P3 | `script_*` | C# script CRUD. Deliberately out of scope so far: an AI editing scripts is better served by ordinary file tools plus `compile`. |
| AGENT-09 | P3 | `batch` | Run several commands in one round-trip, as unity-mcp's `batch_execute` does. Worth it only if per-command latency shows up in practice. |
| AGENT-10 | P3 | `prefab_replace` overrides | Replace re-instantiates by hand (no `ReplacePrefabAssetOfPrefabInstance` dependency), so instance overrides are dropped. Carry them across, or adopt the 2022.2+ API behind a version define. |
| AGENT-11 | P2 | Play Mode real-frame capture | `screenshot view=ui` puts the canvas on a temporary camera, which shows the layout but not the truly composited frame (post-processing, overlay sort order). `ScreenCapture.CaptureScreenshot` would, but it only resolves at end-of-frame, so it needs the deferred-job pipeline. |

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
| CLI-01 | P1 | CI integration matrix | Run `test:integration:editor` when Editor available. `npm run smoke` is the cheap gate. |
| CLI-02 | P1 | Runtime HTTP adapter | Pair with AGENT-02 for Player commands. |
| CLI-03 | P2 | SQLite queue adapter | Optional per architecture §5.1. |
| CLI-04 | P2 | Global install docs | `npm link` / PATH on Windows. |
| CLI-05 | P1 | Wait for a busy session instead of rejecting | The Editor stops ticking while it imports, so its heartbeat goes past `STALE_MS` (`host/targets/target-probe.ts`) and `ucp-host-server.ts` rejects at submit with `no_ready_session`. `RetryPolicy` only covers dispatch of queued commands. The host should hold the command until a session is ready or the caller's timeout expires. Until then every caller needs its own retry — `executeCommandResilient` exists only to paper over this. |

### Do not assign here

| Topic | Owner |
|-------|--------|
| Unity main-thread execution | `com.air.ucp-agent` |
| Business command logic | `Editor/Commands/` |
