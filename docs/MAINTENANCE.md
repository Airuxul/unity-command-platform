# Maintenance guide

## Layout

| Path | Role |
|------|------|
| `ucp-cli/` | Node — CLI, host, protocol, tests |
| `com.air.ucp-agent/` | Unity UPM — agent, commands |
| `docs/` | Architecture, agents, maintenance |

## Add an Editor command

1. Add constant to `com.air.ucp-agent/Runtime/Commands/CommandNames.cs`.
2. Add a param class in `Editor/Params/` (skip if the command takes none).
3. Put the logic in a service under `Editor/Services/`, returning `Dictionary<string, object>`
   and throwing `ArgumentException` / `InvalidOperationException` on bad input.
4. Add a command class in `Editor/Commands/` (namespace `Air.UcpAgent.Editor.Commands` regardless
   of subfolder) inheriting `EditorActionCommand<TParams>` — descriptor plus one line forwarding
   to the service.
5. Recompile Unity — `CliCommandDiscovery` picks it up automatically.
6. Test: `ucp-cli run <name> [--args '{}']`, then `ucp-cli run help` to check the catalog entry.
7. Check nothing else broke: `cd ucp-cli && npm run smoke -- --project <path>` (~3-4 min, 18
   assertions over one Prefab round trip). `npm run test:integration` is the exhaustive pass.

Adding a command also means adding a case to `EDITOR_COMMAND_CASES` or `SCENARIO_COMMANDS` in
`ucp-cli/tests/integration/helpers/` — a catalog guard test fails until every command appears in one
of them, and `ALL_EDITOR_COMMANDS` has to be updated to match.

`EditorActionCommand` owns the try/catch: a returned dictionary becomes the payload, a thrown
exception becomes `CompleteFail(ex.Message)`. That message is the *only* thing the caller sees, so
make it actionable — the content services list valid values, siblings, or property names in their
errors on purpose.

Deferred commands (`compile`, `play`, `stop`) bypass the base class: use `DeferredInvokeDescriptor`,
`MarkRunning()`, and complete via `EditorJobStateManager`.

Declared `aliases` really dispatch — `CommandHandlerRegistry.Register` registers them as extra
dispatcher keys *and* as session capabilities, which ucp-host checks before forwarding.

## Command shape (C#)

| Piece | Location |
|-------|----------|
| Name constants | `Runtime/Commands/CommandNames.cs` |
| Command shell (try/catch → payload) | `Editor/Commands/EditorActionCommand.cs` |
| Commands | `Editor/Commands/**/*Commands.cs` |
| Params | `Editor/Params/*Params.cs` |
| Logic | `Editor/Services/**` |
| FileQueue bridge | `Editor/Bridge/UcpCommandBridge.cs` |
| Protocol DTOs | `Runtime/Protocol/` |

## Content commands (prefab / scene / gameobject / component)

`gameobject_*` and `component_*` exist once, not twice. They resolve their target through
`Editor/Services/Objects/EditTargetScope.cs`:

| `prefab` arg | `scene` arg | Target |
|--------------|-------------|--------|
| set | — | that prefab asset (`LoadPrefabContents` → mutate → `SaveAsPrefabAsset` on dispose) |
| — | set | that loaded scene (marked dirty on dispose; saving is `scene_save`'s job) |
| — | — | the open Prefab Mode stage if there is one, else the active scene |

Rules when extending them:

- Open the scope with `using`; nothing is written unless you call `MarkDirty()`, so read-only
  commands open one just as freely.
- Address nodes via `GameObjectPath` (`"Root/Body"`, `"Body[1]"` for same-named siblings), never by
  raw `transform.Find`.
- Read/write serialized properties via `SerializedValueReader` / `SerializedValueWriter`. They are
  symmetric on purpose: whatever the reader emits, the writer accepts.
- Put value coercion in `JsonValues`, not in a command — params and the property writer share it.
- Use `NodeTargets` for undo/destroy, so scene edits get an undo entry and prefab-contents edits
  skip the pointless one.
- Every payload carries `target` (`scope.Describe()`) so the caller never has to guess which scope
  ran.

If a prefab is already open in Prefab Mode, `EditTargetScope` reuses the live stage root rather than
loading a second copy — otherwise a write would land on disk while the UI kept showing stale state.

Play Mode needs two things switched off, both of which Unity rejects there: undo registration
(`SerializedObject.ApplyModifiedProperties`) and `EditorSceneManager.MarkSceneDirty`.
`EditTargetScope.SupportsUndo` and its `Dispose` both check
`EditorApplication.isPlayingOrWillChangePlaymode` for exactly this reason — without the second
check a write would land and *then* throw, reporting a success as a failure.

## Looking at UI

`camera.Render()` cannot see a `ScreenSpaceOverlay` canvas, so `screenshot view=scene|game` never
shows UI. `view=ui` puts the canvas on a temporary camera instead:

```powershell
# renders a throwaway copy in a preview scene — no side effects at all
ucp-cli run screenshot --args '{"view":"ui","prefab":"Assets/UI/UILogin.prefab"}'

# renders the live canvas, including runtime state; restores renderMode in a finally
ucp-cli run screenshot --args '{"view":"ui","node":"UILogin","background":"#6f7d8c"}'
```

Prefer the `prefab` form. The `node` form has to mutate `canvas.renderMode` for the duration, and
a contrasting `background` is worth passing — the default is transparent, and a dark panel on a
dark backdrop looks like an empty image.

## Node development

```powershell
cd ucp-cli
npm run verify                    # build + lint + unit + file-queue e2e
$env:UCP_EDITOR_INTEGRATION="1"
npm run test:integration:editor   # needs Unity
```

## Bump agent version

When changing session/command JSON shape:

1. Update `Runtime/Protocol/` and `ucp-cli/src/shared/` together.
2. Bump `com.air.ucp-agent/package.json` version.
3. Re-run Unity + integration tests.

## Environment

| Variable | Default | Purpose |
|----------|---------|---------|
| `UCP_ROOT` | `~/.ucp` | Data root |
| `UCP_HOST_PORT` | `6610` | Host HTTP |
| `UCP_EDITOR_INTEGRATION` | unset | Enable live tests |

## Troubleshooting

| Symptom | Check |
|---------|--------|
| `no_ready_session` | Unity open, agent compiled, `~/.ucp/sessions/*.json` exists |
| Host won't start | Kill stale `node …/ucp-host.js` from old paths; delete `host.lock` |
| Command missing | Unity recompile; session `capabilities` list |
| BOM parse error | Agent writes UTF-8 without BOM; CLI strips BOM on read |
