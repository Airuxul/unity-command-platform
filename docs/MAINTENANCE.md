# Maintenance guide

## Layout

| Path | Role |
|------|------|
| `ucp-cli/` | Node — CLI, host, protocol, tests |
| `com.air.ucp-agent/` | Unity UPM — agent, commands |
| `docs/` | Architecture, agents, maintenance |

## Add an Editor command

1. Put the command name on its descriptor (`{domain}_{operation}`). Add a constant to
   `CommandNames.cs` only when other C# has to mention that name. The file is not a registry;
   `CommandRegistry` reads the descriptor.
2. Add a param class in `Editor/Params/` (skip if the command takes none).
3. Put the logic in a service under `Editor/Services/`, returning `Dictionary<string, object>`
   and throwing on bad input: `UcpException(code, message, hint)` for a known failure,
   `ArgumentException` (→ `invalid_argument`) for a bad parameter. Parameter names used by more
   than one command come from `ParamKeys`.
4. Add a static class marked `[UcpCommand]` in `Editor/Commands/` (namespace
   `Air.UcpAgent.Editor.Commands` regardless of subfolder) with a static `Descriptor` and a static
   `Execute(TParams p, TContext ctx)`. A thrown exception becomes a coded failure; `ctx.Fail`
   records one without throwing; `ctx.Defer(kind, args)` leaves the call for a completion policy;
   a `DeferredInvokeDescriptor` also leaves the call even when `Execute` returns null. Anything
   else, including null, is success. The message is `ctx.Message`, or the command name.
   If the param type is `EditTargetParams` (or a subclass), `Execute` takes an `EditTargetContext`
   and `EditorExecutionHost` opens the target before it runs; `ctx.Scope` is that
   `EditTargetScope`. `ReadsTargetOnly = true` on the descriptor peeks instead of recording an
   undo group. When the command fails the host reverts the step's undo group and calls the
   command's `Undo`, if it has one. Write an `Undo` only for what Unity's undo does not record
   (asset files, folders); keep what it needs on a context subclass, never on the class. The
   service never opens or saves a target itself, so the same command works inside a `group`.
   `help` lists any class `CommandRegistry` could not load under `load_errors`.
5. Recompile Unity (`ucp-cli run compile`) — `CommandRegistry` picks it up automatically.
6. Test: `ucp-cli run <name> [--args '{}']`, then `ucp-cli help <name>` to check its card.
7. Check nothing else broke: `cd ucp-cli && npm run smoke -- --project <path>` (a few seconds,
   18 assertions over one Prefab round trip). `npm run test:integration` is the exhaustive pass.

A command can name other commands to run before or after it (`Before` / `After` on the descriptor).
ucp-host runs each as its own command, in order, and stops at the first failure. The caller's
result is this command's result, or the failing step's. Arguments are not forwarded. A command
that appears again inside its own chain fails the call. Publish happens through the session file;
`ucp-cli help` lists the names. Built-in commands leave both empty. To compile and then enter Play,
either set `Before = new[] { CommandNames.Compile }` on `play`, or let the caller order the two
steps with `group` or `run-script`.

Adding an editor command also means adding a case to `EDITOR_COMMAND_CASES` or `SCENARIO_COMMANDS`
in `ucp-cli/tests/integration/helpers/`. A catalog guard test fails until every command in
`catalog.editor.json` appears in one of them. `ALL_EDITOR_COMMANDS` is read from that snapshot.
A runtime command, and any command name that exists on both hosts, needs a case in
`RUNTIME_COMMAND_CASES`: the guard reads `catalog.runtime.commands.json` and requires each of
those names to run on every host that advertises it. `unknown-params.test.ts` rejects an
undeclared key on every command that has parameters, before the command runs. `shell-input.test.ts`
checks the in-editor command line. `runtime-extension.test.ts` checks that a project script
command of runtime scope is discovered only on the runtime host.
Then update the snapshots with `-u` and check the diff: `catalog.editor.json` (names and
parameters) and the `shapes.*.json` of the test file (result keys).

The message is what the caller reads, so make it actionable — the content services list valid
values, siblings, or property names in their errors on purpose. A parameter key the command does
not declare fails as `invalid_argument` and the hint lists the keys `UcpParamBinder.Inspect`
knows. There are no alternate keys.

### Deferred commands

The choice — when the result is ready, and how that differs from lane bypass, compiled scripts, and edit targets — is [COMMAND-DESIGN.zh-CN.md](COMMAND-DESIGN.zh-CN.md). The steps below are the file-level checklist.

A command that finishes after `Execute` returns (it waits for a compile, Play Mode, a test run, a
package resolve, a UI condition) is deferred:

1. Point the command at the policy class: `public static readonly Type CompletionPolicy = typeof(ThePolicy)`,
   then `DeferredInvokeDescriptor<TParams>(name, scope, description, CompletionPolicy, defaultTimeoutMs: ...)`.
   The job stores `ThePolicy`'s full name, which is what survives a domain reload. Call `ctx.Defer(typeof(ThePolicy), args)`
   when only some calls defer (`refresh compile=true`, `ui_wait` when the condition does not hold yet).
   When the descriptor already names a policy, `ctx.Defer` must pass that same class (`package_add` does this to
   store `CompletionArgs`); a different class fails the command. A deferred command with `timeoutMs <= 0` fails
   as `invalid_argument`. The runtime host has no job store, so a deferred descriptor fails as `command_failed`
   before `Execute`.
2. Add an `ICompletionPolicy` under `Editor/Completion/`. The job finds it by its full name.
   `EditorJobStateManager` finds every concrete policy by reflection. A duplicate policy class throws
   at editor startup, and so does a deferred command whose policy class is missing. A job whose
   policy class is not loaded after a domain reload fails as `orphaned`. The policy runs every
   editor tick, also after a domain reload,
   and reads what it needs from the job (`CompletionArgs`, JSON you passed to `Defer`) — never
   from a static, which the reload clears. Return a `PolicyOutcome` when the message is not the
   default `"ok"`.
3. Give it a `defaultTimeoutMs` that covers a slow machine; it is also the job's deadline.

Jobs are Newtonsoft JSON of `InvokeJobRecord` itself (`UcpJson`). `Result` is not written; the
reload reads `ResultJson`. A file this agent does not write is dropped. Enums are names.

Declared `aliases` really dispatch — `CommandRegistry` uses them as lookup keys *and* lists them
as session capabilities, which ucp-host checks before forwarding.

`RunsWhileBusy` on the descriptor lists the commands that change nothing in the project or the play
state. The agent writes those names in the session file as `runsWhileBusy`. An empty list waits
for every command. ucp-host lets those commands past a command still running on the Editor, and
they still run during a test run. Set it only if running the command in the middle of a compile,
a test run or Play Mode entry can do no harm, and it answers synchronously. `ReadsTargetOnly = true`
only skips the undo group; it does not skip the queue.

## Command shape (C#)

| Piece | Location |
|-------|----------|
| Name constants | `Runtime/Commands/CommandNames.cs` (only names other C# mentions) |
| Command shape (`[UcpCommand]`, `Descriptor`, `Execute`, `Undo`) | `Runtime/Cli/UcpCommandAttribute.cs`, checked by `Runtime/Cli/CommandRegistry.cs` |
| Per-run context, and how a step is run and taken back | `Runtime/Cli/CommandContext.cs`, `Runtime/Cli/ExecutionHost.cs`, `Editor/Services/Objects/EditorExecutionHost.cs` |
| `[UcpFunction]` and `call` | `Runtime/Cli/FunctionRegistry.cs`, `Runtime/Commands/CallCommand.cs` |
| `group`, its journal and resume after a reload | `Editor/Group/GroupRun.cs`, `Editor/Group/GroupPolicy.cs` |
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
| set | — | that prefab asset (`LoadPrefabContents` → mutate → `SaveAsPrefabAsset` on commit) |
| — | set | that loaded scene (marked dirty on commit; saving the file is `scene_save`'s job) |
| — | — | the open Prefab Mode stage if there is one, else the active scene |

Rules when extending them:

- Open the scope with `using`. `RecordUndo`, `RegisterCreated`, `Destroy`, `SetParent` and
  `AddComponent` mark the target dirty; `Commit` persists only then, so a read-only command can
  open one freely. A `PrefabUtility` write that does not go through those methods still calls
  `MarkDirty` itself (`prefab_apply`, `prefab_create` when it connects the source node).
- Address nodes via `GameObjectPath` (`"Root/Body"`, `"Body[1]"` for same-named siblings), never by
  raw `transform.Find`.
- Read/write serialized properties via `SerializedValueReader` / `SerializedValueWriter`. They are
  symmetric on purpose: whatever the reader emits, the writer accepts.
- Put value coercion in `JsonValues`, not in a command — params and the property writer share it.
- Use the scope's undo / destroy methods, so scene edits get an undo entry and prefab-contents
  edits skip the pointless one. A read-only command uses `EditTargetScope.Peek`, which opens no
  undo group.
- `EditorExecutionHost` adds `target` (`scope.Describe()`) to every payload, also for each step of
  a `group`, so the caller never has to guess which scope ran; services do not add it themselves.

If a prefab is already open in Prefab Mode, `EditTargetScope` reuses the live stage root rather than
loading a second copy — otherwise a write would land on disk while the UI kept showing stale state.
A write is refused if that stage already had unsaved changes, because saving the stage would write
them too.

`EditTargetScope.Dispose` only releases what it loaded. Writes are persisted by `Commit` and undone
by `Revert` (an undo group for scenes and the stage; off-screen prefab contents are simply not
saved), and only `EditorExecutionHost` and `SharedTargets` call them.

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
npm run test:integration          # needs Unity, about 6 min
$env:UCP_EDITOR_SLOW="1"          # also compile/play cases and a real player build
```

The gate before a commit: `npm run check`, `npm test`, and the integration suite with
`UCP_EDITOR_SLOW=1`. Nothing is checked by hand: `delivery.test.ts` and `background.test.ts`
minimize Unity themselves (Windows) and put the window back, and every test asserts
`~/.ucp/queues/{projectId}/{inbox,outbox}` and `~/.ucp/jobs/{projectId}` empty. No hosted CI runs
the Unity suites: they need a licensed Editor.

### Every claim has a test

A behaviour this repository states — in a doc, a changelog entry, a commit message, a code
comment that promises something — is backed by an automated test, or it is not stated.

- **A fix starts with its test.** Write the test, run it on the code before the fix and see it fail
  for the reason the fix addresses, then fix it and see it pass. A test that passes without the
  fix does not test the fix.
- **A finding is a failing test**, not a reading of the code. Two of the problems fixed in 0.5
  (the session file read mid-replace, a runtime command just after `play`) were found only by
  writing tests; one sentence in RELIABILITY ("answers during a compile") was wrong until a test
  measured it.
- **Numbers come from tests**, which print them (`MEASURED …` in `background.test.ts`) and assert
  a bound, so a regression fails the suite instead of only changing a number nobody reads.
- **What cannot be tested deterministically** (a race of a few milliseconds) is said so in the
  doc, and the code is changed so that a deterministic mechanism closes it — that mechanism is
  what gets the test (see `POST /shutdown`).
- Do not hand anything to a person to confirm by hand. [RELIABILITY.zh-CN.md](RELIABILITY.zh-CN.md)
  §6 maps each guarantee to its tests; add the row when you add the guarantee.

The integration suites change the test project and put it back: scratch assets under
`Assets/UcpIntegration`, a probe test assembly, `Packages/manifest.json` (package tests), Enter Play
Mode Options (PlayMode test case). Point `UCP_TEST_PROJECT` at a throwaway project.

## Bump versions

When changing session/command JSON shape or releasing:

1. Update `Runtime/Protocol/` and `ucp-cli/src/shared/` together.
2. Bump `com.air.ucp-agent/package.json`, and `UcpAgentBuild.Id` (reported by `state`).
3. Bump `ucp-cli/package.json` and `.version(...)` in `ucp-cli/src/cli/main.ts`. The host build id
   is written by tsup on every build; nothing to bump.
4. Re-run Unity + integration tests. The upgrade from the previous release is a test too
   (`delivery.test.ts`, "finishes a 0.3 compile after the upgrade reload…"): when the job record
   gains a field, seed the previous version's format there.
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
| `UCP_ALLOW_FOCUS` | unset | `1` also runs `foreground.test.ts`, which brings Unity to the front and so takes the focus from whoever is at the machine; for when nobody is |

## Troubleshooting

| Symptom | Check |
|---------|--------|
| `no_ready_session` | Unity open, agent compiled, `~/.ucp/sessions/*.json` exists |
| Host won't start | Kill stale `node …/ucp-host.js` from old paths; delete `host.lock` |
| Command missing | Unity recompile; session `capabilities` list |
| BOM parse error | Agent writes UTF-8 without BOM; CLI strips BOM on read |
