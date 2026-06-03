# Integration scenarios

| Scenario | Default profile | Unity prerequisite |
|----------|-----------------|-------------------|
| `editor-lifecycle` | `editor` | Editor open; `editor-play` profile for Play steps |
| `player-runtime` | `package-play` | Development Build with Dev assembly running |
| `compile-error-recovery` | `editor` | Editor open; `UNITY_CMD_WORKSPACE` set to project root |

```bat
cd unity-cmd
set UNITY_CMD_WORKSPACE=C:\Path\To\UnityProject

REM Editor full lifecycle (default scenario)
set UNITY_CMD_PROFILE=editor
npm run test:integration

REM Compile error recovery flow
set UNITY_CMD_PROFILE=editor
set UNITY_CMD_SCENARIO=compile-error-recovery
npm run test:integration

REM Dev player only
set UNITY_CMD_PROFILE=package-play
set UNITY_CMD_SCENARIO=player-runtime
npm run test:integration
```

## editor-lifecycle (~26 steps)

1. **Edit mode** — ping, state, catalog, echo (editor channel), console, profiler, exec
2. **Play** — play, wait for editor-play endpoint
3. **Play mode** — runtime echo on editor_play; catalog isolation; editor host: state, profiler, screenshot
4. **Exit** — stop, verify edit mode restored

## compile-error-recovery (13 steps)

Requires `UNITY_CMD_WORKSPACE` pointing to a writable Unity project root.

1. **Baseline** — ping + compile to confirm a clean build
2. **Inject error** — write `Assets/_IntegrationTest_CompileErrorRecovery.cs` with a syntax error
3. **Fail compile** — call `compile`; expect `EDITOR_NOT_READY` (connector is compiling / reloading)
4. **Fix** — delete the bad `.cs` (+ `.meta`) file
5. **Recover** — call `compile` again; expect success
6. **Verify** — state, ping, catalog, echo all confirm the editor is back to a clean edit-mode state

> The test file is always cleaned up by `deleteFile` even if a preceding step fails, because the runner
> stops at first failure and the file will be removed in the next full run's `deleteFile` step.
> For a CI environment, add a pre-run cleanup step or use a dedicated test project.

## player-runtime (7 steps)

Runtime catalog + `echo`; negative checks for Editor-only commands on player host.
