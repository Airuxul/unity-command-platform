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
   and throwing on bad input: `UcpException(code, message, hint)` for a known failure,
   `ArgumentException` (→ `invalid_argument`) for a bad parameter. Parameter names used by more
   than one command come from `ParamKeys`.
4. Add a command class in `Editor/Commands/` (namespace `Air.UcpAgent.Editor.Commands` regardless
   of subfolder) inheriting `EditorActionCommand<TParams>` — descriptor plus one line forwarding
   to the service. If it works inside a scene or prefab, inherit `EditTargetCommand<TParams>`
   and implement `Apply(scope, p)` instead; the service takes the scope as its first argument and
   never opens or saves one itself. That makes it usable in `prefab_edit` / `scene_edit` too.
5. Recompile Unity (`ucp-cli run compile`) — `CommandRegistry` picks it up automatically.
6. Test: `ucp-cli run <name> [--args '{}']`, then `ucp-cli help <name>` to check its card.
7. Check nothing else broke: `cd ucp-cli && npm run smoke -- --project <path>` (a few seconds,
   18 assertions over one Prefab round trip). `npm run test:integration` is the exhaustive pass.

Adding a command also means adding a case to `EDITOR_COMMAND_CASES` or `SCENARIO_COMMANDS` in
`ucp-cli/tests/integration/helpers/` — a catalog guard test fails until every command appears in one
of them, and `ALL_EDITOR_COMMANDS` has to be updated to match. Then update the snapshots with
`-u` and check the diff: `catalog.editor.json` (names and parameters) and the `shapes.*.json` of
the test file (result keys).

`EditorActionCommand` owns the try/catch: a returned dictionary becomes the payload, a thrown
exception becomes `CompleteFail(ex)`, which keeps a `UcpException`'s code and hint. The message is
what the caller reads, so make it actionable — the content services list valid values, siblings,
or property names in their errors on purpose.

### Deferred commands

A command that finishes after `Run` returns (it waits for a compile, Play Mode, a test run, a
package resolve, a UI condition) is deferred:

1. Declare it with `DeferredInvokeDescriptor<TParams>(name, scope, description, completionKind,
   defaultTimeoutMs: ...)`, or call `Defer(kind, args)` from `Run` when only some calls defer
   (`refresh compile=true`, `ui_wait` when the condition does not hold yet).
2. Add the kind to `InvokeCompletionCatalog` and an `ICompletionPolicy` under `Editor/Completion/`,
   registered in `EditorJobStateManager.Policies`. The policy runs every editor tick, also after a
   domain reload, and reads what it needs from the job (`CompletionArgs`, a JSON string you passed
   to `Defer`) — never from a static, which the reload clears.
3. Give it a `defaultTimeoutMs` that covers a slow machine; it is also the job's deadline.

`InvokeJobRecord` is read back by the next package version after an upgrade: never rename its
fields or reorder `InvokeJobStatus`, add fields only at the end, and treat their zero value as
"written by an older version".

Declared `aliases` really dispatch — `CommandRegistry` uses them as lookup keys *and* lists them
as session capabilities, which ucp-host checks before forwarding.

## Command shape (C#)

| Piece | Location |
|-------|----------|
| Name constants | `Runtime/Commands/CommandNames.cs` |
| Command shell (try/catch → payload) | `Editor/Commands/EditorActionCommand.cs` |
| Commands | `Editor/Commands/**/*Commands.cs` |
| Params | `Editor/Params/*Params.cs`, shared names in `Editor/Params/ParamKeys.cs` |
| Logic | `Editor/Services/**` |
| Error codes | `Runtime/Cli/UcpException.cs` (`UcpErrorCodes`) |
| Completion policies | `Editor/Completion/*Policy.cs` |
| FileQueue loop | `Editor/EditorAgentBootstrap.cs` → `Runtime/Cli/CommandExecutor.cs` |
| Protocol DTOs | `Runtime/Protocol/` |

`test_run` lives in its own assembly, `Editor/TestRunner/com.air.UcpAgent.Editor.TestRunner.asmdef`,
compiled only when `com.unity.test-framework` is installed (`versionDefines` → `UCP_TEST_FRAMEWORK`).
Do not name it `*.Tests` or Unity treats it as a test assembly.

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
- Use the scope's undo / destroy methods, so scene edits get an undo entry and prefab-contents
  edits skip the pointless one. A read-only command uses `EditTargetScope.Peek`, which opens no
  undo group.
- `EditTargetCommand` adds `target` (`scope.Describe()`) to every payload, so the caller never has
  to guess which scope ran; services do not add it themselves.

If a prefab is already open in Prefab Mode, `EditTargetScope` reuses the live stage root rather than
loading a second copy — otherwise a write would land on disk while the UI kept showing stale state.
A write is refused if that stage already had unsaved changes, because saving the stage would write
them too.

`EditTargetScope.Dispose` only releases what it loaded. Writes are persisted by `Commit` and undone
by `Revert` (an undo group for scenes and the stage; off-screen prefab contents are simply not
saved), and only `EditBatch.Run` calls them.

Asset writes follow `AssetPaths`: create, move and delete only under `Assets/`, never `..`, never
over an existing asset (a replaced asset gets a new GUID and breaks references), and deletes go to
the OS trash.

Play Mode needs two things switched off, both of which Unity rejects there: undo registration
(`SerializedObject.ApplyModifiedProperties`) and `EditorSceneManager.MarkSceneDirty`.
`EditTargetScope.SupportsUndo` and its `Commit` both check
`EditorApplication.isPlayingOrWillChangePlaymode` for exactly this reason — without the second
check a write would land and *then* throw, reporting a success as a failure.

## Looking at UI

`camera.Render()` cannot see a `ScreenSpaceOverlay` canvas, so `screenshot view=scene|game` never
shows UI. `view=ui` puts the canvas on a temporary camera instead:

```powershell
# renders a throwaway copy in a preview scene — no side effects at all
ucp-cli run screenshot --args '{"view":"ui","source":"Assets/UI/UILogin.prefab"}'

# renders the live canvas, including runtime state; restores renderMode in a finally
ucp-cli run screenshot --args '{"view":"ui","node":"UILogin","background":"#6f7d8c"}'
```

Prefer the `source` form. The `node` form has to mutate `canvas.renderMode` for the duration, and
a contrasting `background` is worth passing — the default is transparent, and a dark panel on a
dark backdrop looks like an empty image.

## Node development

```powershell
cd ucp-cli
npm run verify                    # build + lint + unit + fake-agent e2e (Unity suites skip)

$env:UCP_EDITOR_INTEGRATION="1"
$env:UCP_TEST_PROJECT="C:/path/to/UnityProject"
npm run test:integration          # needs Unity, about 3 min
$env:UCP_EDITOR_SLOW="1"          # also compile/play cases and a real player build
```

The gate before a commit: `npm run check`, `npm test`, the integration suite with
`UCP_EDITOR_SLOW=1`, and `delivery.test.ts` once more with Unity minimized. Afterwards
`~/.ucp/queues/{projectId}/{inbox,outbox}` and `~/.ucp/jobs/{projectId}` must be empty. No hosted
CI runs the Unity suites: they need a licensed Editor.

The integration suites change the test project and put it back: scratch assets under
`Assets/UcpIntegration`, a probe test assembly, `Packages/manifest.json` (package tests), Enter Play
Mode Options (PlayMode test case). Point `UCP_TEST_PROJECT` at a throwaway project.

## Bump versions

When changing session/command JSON shape or releasing:

1. Update `Runtime/Protocol/` and `ucp-cli/src/shared/` together.
2. Bump `com.air.ucp-agent/package.json`, and `UcpAgentBuild.Id` (reported by `state`).
3. Bump `ucp-cli/package.json` and `.version(...)` in `ucp-cli/src/cli/main.ts`. The host build id
   is written by tsup on every build; nothing to bump.
4. Re-run Unity + integration tests, and the upgrade from the previous release
   ([RELIABILITY.zh-CN.md](RELIABILITY.zh-CN.md) §3).
5. Add the release to [CHANGELOG_AGENT.md](CHANGELOG_AGENT.md), with an upgrade table if a
   parameter or result key changed.

## Environment

| Variable | Default | Purpose |
|----------|---------|---------|
| `UCP_ROOT` | `~/.ucp` | Data root |
| `UCP_HOST_PORT` | `6610` | Host HTTP |
| `UCP_LOG_LEVEL` | `info` | Host log level |
| `UCP_RUNTIME_BIND` / `UCP_RUNTIME_PORT` | `127.0.0.1` / `6620` | Runtime agent listener (Play Mode) |
| `UCP_EDITOR_INTEGRATION` | unset | `1` enables the Unity test suites |
| `UCP_EDITOR_SLOW` | unset | `1` adds compile/play cases and the real build |
| `UCP_TEST_PROJECT` | `C:/Project/GameDemo` | Unity project the suites and smoke run against |

## Troubleshooting

| Symptom | Check |
|---------|--------|
| `no_ready_session` | Unity open, agent compiled, `~/.ucp/sessions/*.json` exists |
| Host won't start | Kill stale `node …/ucp-host.js` from old paths; delete `host.lock` |
| Command missing | Unity recompile; session `capabilities` list |
| BOM parse error | Agent writes UTF-8 without BOM; CLI strips BOM on read |
