# Integration scenarios

| Scenario | Default profile | Unity prerequisite |
|----------|-----------------|-------------------|
| `editor-lifecycle` | `editor` | Editor open; `editor-play` profile for Play steps |
| `player-runtime` | `package-play` | Development Build with Dev assembly running |

```bat
cd unity-cmd
set UNITY_CMD_WORKSPACE=C:\Path\To\UnityProject

REM Editor full flow (default scenario)
set UNITY_CMD_PROFILE=editor
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

## player-runtime (7 steps)

Runtime catalog + `echo`; negative checks for Editor-only commands on player host.
