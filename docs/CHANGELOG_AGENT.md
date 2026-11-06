# Agent Changelog — `unity-command-platform`

## Unreleased

- A deferred command points at its policy class (`CompletionPolicy = typeof(...)` on the descriptor, or `ctx.Defer(typeof(...))`). The job stores that class's full name. A job whose class is not loaded after a domain reload fails as `orphaned`.
- Changed scripts are compiled by `compile`. `exec`, `play`, `test_run` and `build` run the scripts already loaded.
- A command may name other commands in `Before` / `After`. ucp-host runs that chain in order and returns this command's result, or the first failure.
- **Breaking: commands are static classes.** A command is a static class marked `[UcpCommand]` with `public static readonly InvokeDescriptor Descriptor`, `public static object Execute(TParams p, TContext ctx)` and an optional `public static void Undo(TParams p, TContext ctx)`. `CliCommand`, `CliCommand<T>`, `IUndoable`, `MutatesTarget` and the `Message(parameters)` override are gone: set `ctx.Message`, and use `ReadsTargetOnly = true` on the descriptor. A malformed class is skipped and listed under `load_errors` in `help`.
- **Breaking: renames.** `[CliParam]` is `[UcpParam]` (`UcpParamBinder`, `UcpParamInfo`). The protocol class `UcpCommand` is `UcpRequest`; the JSON on the wire is unchanged.
- `CommandContext` is one run's record: id, message, result, failure, deferral. A command that needs to remember something for its `Undo` takes a subclass with JSON-serializable fields. Edit-target commands take `EditTargetContext`, whose `Scope` is the open target.
- **Breaking: `prefab_edit` / `scene_edit` are removed.** `group` replaces them: `steps: [{command, args, expect?}]` with any command, `rollback=all|failed`, `on_error=stop|continue`, `dry_run`. Consecutive steps on the same target are saved once. A group carries on across the domain reload of a `compile` or Play Mode step; its state is in `Library/ucp/undo/<id>/`. New error code `rollback_failed`.
- Taking a command back uses its Unity undo group, plus its own `Undo` for what Unity does not record. `prefab_create`, `asset_create_folder`, `asset_move`, `asset_duplicate`, `asset_delete` and the `prefab_*` asset commands now have one; a prefab a group already saved is written back from a copy.
- `call` runs a static method marked `[UcpFunction("name")]`, binding `args` by parameter name; without `name` it lists them.
- Commands that still answer while the Editor is busy set `RunsWhileBusy`. The session file lists them as `runsWhileBusy`.
- `--session-type` other than `editor` or `runtime`, and `run --args` that is not a JSON object, fail as `invalid_argument`.
- A dispatch that never reached Unity (`unknown_session_type`, an editor session with no queue id) is a host failure, not a completed Unity result.
- `RecordUndo`, `RegisterCreated`, `Destroy`, `SetParent` and `AddComponent` mark the edit target dirty. Saving a prefab stage goes through `EditTargetScope.SavePrefabAsset`.

## 0.5.0 — 2026-11-05 (`com.air.ucp-agent` 0.5.0, `ucp-cli` 0.3.0)

The release after master's agent 0.3.0 / ucp-cli 0.2.0. It includes the content commands of 0.4.0
below, which were never released on their own. The command list is `ucp-cli help`; the catalog
snapshot (`ucp-cli/tests/integration/__snapshots__/catalog.editor.json`) is the record of every
name and parameter.

### Upgrading from 0.3

Update both packages together. The first domain reload under the new agent finishes the command
that loaded it (a `compile` or `refresh compile=true` sent to the old agent) and drops the job
records 0.3 kept forever; nothing is delivered twice. An older `ucp-host` still running is
restarted by the new CLI the first time it is idle.

Update **every** `ucp-cli` on the machine at once. They share one ucp-host, and a 0.2 CLI cannot
talk to a 0.3 host: every call it makes fails with `unauthorized` (it sends no token), and it never
restarts the host itself.

Parameters and results that changed. An unknown parameter name is ignored, so a caller still
sending an old one gets `invalid_argument` only where the new name is required; check each row.

| Command | Before | Now |
|---------|--------|-----|
| `manage` | one command with an `action` | removed: `pause`, `tag_*`, `layer_*`, `tool_set`, `play`, `stop`, `refresh` |
| `prefab_info` `prefab_delete` `prefab_tree` `prefab_open` `prefab_duplicate` `prefab_move` `prefab_create_variant` `prefab_create_empty` | `prefab` / `prefab_path` name the asset | `path`; `prefab` only ever names the asset a content command edits |
| `prefab_replace` | `path` (alias `to`) | `source` (alias `asset`) |
| `prefab_instantiate` | `source` or `path` | `source` |
| `screenshot` | `prefab` / `prefab_path` | `source`; `output_path` must be inside the project |
| `scene_open` | alias `scene` | `path` only; mode `additivewithoutloading` synonym removed |
| `gameobject_create` | `primitive=none` / `gameobject` | omit `primitive` |
| `tool_set` | `custom` | not accepted |
| `profiler` | `action` required | defaults to `hierarchy`; `sort` also takes `calls` |
| `reserialize` | empty `paths` meant the whole project | `all=true` for that |
| `build` | skeleton returning `implemented: false` | real player build; `output_path` required, not under `Assets/` |
| `ucp-cli build` | no arguments | `ucp-cli build <output_path> [--platform] [--scene ...] [--development]` |
| `ucp-cli help` | commander's usage | the agent's command list; `ucp-cli help <command> [--md]` for one card |
| `gameobject_tree` `prefab_tree` | every node | at most `limit` (default 500); check `truncated` |
| `ui_snapshot` | rows keyed `path` | `node`; at most `limit` rows (default 500) |
| `gameobject_info` | `prefab` with its own key set | `is_part_of_instance` / `is_instance_root` / `source` / `status`, as `prefab_instance_info` |
| `prefab_overrides` | `property_modifications[].target` | `component_type` |
| `compile` | succeeded with compile errors | fails as `compile_error` with `data.errors` |
| any value outside a parameter's allowed list | fell back to the default | `invalid_argument` naming the valid values |

Every failure now has `error_code` = a code, `message` = the text, `hint` = what to try, whether
Unity or ucp-host decided it. Before, `error` held the text and the CLI reported most failures as
`command_failed` or a bare `failed`. Successful results also carry `timing` (`queued_ms`,
`unity_ms`, `run_ms`). `ucp-cli help <command>` lists the agent's codes; the ones ucp-host and the
CLI add are:

| Code | Meaning |
|------|---------|
| `no_ready_session` | no Editor (or, with `--session-type runtime`, no Play Mode) for the project |
| `editor_not_running` | the Editor's process has exited |
| `session_unavailable` | the runtime went away (Play Mode ended) before the command was sent |
| `host_unknown_command` | the session does not offer that command (was `failed`) |
| `host_timeout` | no result before the deadline; the hint says if Unity was in the background. Unity's own wait (for example `ui_wait`) is still `timeout` |
| `caller_disconnected` | the caller stopped waiting; the command was dropped before it ran |
| `host_command_failed` | ucp-host could not deliver the command; the message says why |
| `host_unreachable` | the CLI could not reach ucp-host; it starts the host and sends again |
| `host_lost` | ucp-host died while the CLI waited; the command may still have run (was an exception) |
| `host_restarting` | ucp-host is making way for a newer one; the CLI sends again by itself |
| `unauthorized` | no or wrong `X-UCP-Token`, usually a 0.2 `ucp-cli` |

Anything calling ucp-host directly (not through `ucp-cli`) must send `X-UCP-Token` from
`~/.ucp/host.json`; only `/health` is open.

### New

- **Play Mode UI**: `ui_click`, `ui_input`, `ui_wait` (exists / active / shows a text, checked
  every frame), addressed by the paths `ui_snapshot` prints, across loaded scenes and
  DontDestroyOnLoad. `ui_snapshot` takes `root` and `limit` and lists input fields.
- **`test_run`**: EditMode and PlayMode tests (domain reload on or off), counts and failures,
  `tests_failed` when any fails. Built only when `com.unity.test-framework` is installed. Refuses
  dirty scenes; other commands get `busy` while a run is going, and `state` has `tests_running`.
- **`build`**: `BuildPipeline.BuildPlayer` with the report summary; `build_failed` with its errors.
- **`package_list` / `package_add` / `package_remove`**: finish after the resolve, compile and
  reload they cause. Removing ucp-agent, its dependencies or an embedded package is refused.
- **`ucp-cli run-script flow.json`**: runs `[{command, args?, timeout?, session_type?}]` in order
  and stops at the first failure; `play` / `stop` steps resume across their reloads.
- **`help command=<name>`**: one command's card with an example call and the error codes;
  `format=md` for markdown.
- **Bounded output**: `gameobject_tree` / `prefab_tree` take `limit`, `format=compact`, `fields`;
  `prefab_tree` takes `node`.
- **Batch preconditions**: `prefab_edit` / `scene_edit` ops take `expect {node, exists,
  components}`, checked just before the op; a miss rolls the batch back (`expectation_failed`).
- **`dry_run`** for `prefab_edit` / `scene_edit`; `console` merges repeated entries and keeps the
  newest.

### Fixed

- Each command runs once. Before, the inbox file outlived the domain reload of `compile` / `play`
  and ran a second time, and ucp-host re-sent a timed-out command (`gameobject_create` or `exec`
  could run twice).
- Nothing is left in `~/.ucp`. Every command used to leave an inbox file, an outbox file and a job
  record forever, and the Editor re-read them all on every poll; `ping` took about 4 s.
- A deferred command times out at its own timeout (`compile` / `refresh` 180 s, `play` / `stop`
  120 s, `test_run` 10 min, `package_*` 5 min, `build` 30 min). It used to be killed after 20 s
  without progress, and the CLI sent 30 s for everything.
- `compile` imports changed files first; in the background it rebuilt the old scripts and
  reported success.
- A command sent while the Editor imports assets waits for it instead of failing as
  `no_ready_session`; an Editor whose process has exited fails at once as `editor_not_running`.
- The agent checks its inbox every editor tick instead of every 250 ms. With Unity minimized
  (about 10 ticks a second), 20 pings back to back: median 295 ms → 96 ms from handing the
  command over to reading the result (`background.test.ts`). In front Unity ticks 70–294 times a
  second and a ping takes about 18 ms, set by the agent's own 20 ms check interval
  (`foreground.test.ts`, opt-in with `UCP_ALLOW_FOCUS=1` since it takes the focus).
- `ping`, `state`, `console`, `help` and `echo` no longer wait for another command on the Editor
  to finish (`test_run`, `compile`); they used to queue behind it until their own timeout. During
  the domain reload itself they still wait, since Unity does not tick then.
- `exec`, `play`, `test_run` and `build` import and compile a script changed since the last
  compile before they run, and fail as `compile_error` if it does not compile. A command declares
  this with `RequiresCompiledScripts = true` on its descriptor, so a project's own command can too. `exec` and `play`
  used to run the old code: Unity in the background never imports a change on its own.
- `ui_wait` and `ui_snapshot` see TextMeshPro text: the lookup skipped `TMPro.TMP_Text` because it
  is abstract, so `ui_wait until=text` never matched a TMP label.
- A command queued behind a long one times out at its own deadline, not when the long one ends.
- Unity does not start a command whose deadline has passed (left in the inbox by a host that
  died), and drops a result that comes in after it (`exec` blocking past its timeout). Both used to
  leave an outbox file nobody reads.
- An unreachable runtime target in `targets.json` no longer slows the Editor down: the host saw an
  Editor turn ready up to 6 s late, waiting for the device's 3 s `/health` probe to give up.
- ucp-host no longer takes the Editor for gone when it reads the session file just as the agent
  replaces it (1 read in about 1000 on Windows). That failed a submit with `no_ready_session`.
- The first runtime command after `play` no longer fails with `no_ready_session` when it comes
  before the next 2 s probe; the host probes once more before turning it away.
- Restarting an older idle host goes through `POST /shutdown`, which the host refuses while a
  command is in flight. A kill could lose a command submitted just before it.
- Security: ucp-host refuses requests with an `Origin` or a foreign `Host` (a web page could POST
  `exec`) and needs its token; the runtime agent refuses `Origin` and starts only in the Editor,
  Development Builds, or with `UCP_RUNTIME_AGENT`.
- Content fixes: `component_set` refuses to write back a truncated array read; `selection_set` and
  `screenshot` look in the active scene, not the Prefab Mode stage; a failed batch rollback keeps
  the original error; `scene_new` rejects unknown modes.

### Internals

- Agent: `CommandRegistry` + `CommandExecutor` replace the 15 forwarding layers left from the HTTP
  connector (core framework 2308 → 1435 lines). Only deferred commands create a job, through
  `DeferredInvokeDescriptor` or `CliCommand.Defer(kind[, args])`; a completion policy finishes it,
  also after a domain reload. Inbox files are claimed by renaming them to `.claimed`.
- Host: `SessionDirectory` and `CommandRunner` replace seven queue / scheduler / router modules.
  One lane per session; `bypassesLane` commands skip it; deadlines count from submission;
  `GET /commands/:id?wait=` long-polls; the outbox is watched with `fs.watch`. `/health` reports a
  build id and the CLI restarts an older idle host. Editor sessions are read apart from runtime
  probes.
- Protocol additions (old agents and hosts ignore them): the command carries `deadline` (Unix ms);
  the session file carries `readOnlyCommands`, `pid`, `focused`, `defaultTimeouts`; ucp-host has
  `POST /shutdown`.
- Content layer: one copy of each helper (`SceneLookup`, `AssetQuery`, `TransformValues`,
  `TagsAndLayers`); `ParamKeys` holds the shared parameter names; binder-enforced `AllowedValues`.
- Tests: catalog snapshots lock every command's name and parameters, shape snapshots lock the keys
  of every result. Every behaviour above has a test, and every fix's test was seen failing
  without the fix; nothing is left to check by hand ([MAINTENANCE.md](MAINTENANCE.md#every-claim-has-a-test)).

## 0.4.0 — content command surface (not released on its own; part of 0.5.0)

Editor commands **15 → 82**. The agent could drive the Editor but could not touch project content;
the only escape hatch was `exec`. Command set modelled on
[CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp), reshaped to one operation per command.

**New families**

- `prefab_edit` / `scene_edit` — run several node commands on one target; kept only if all
  succeed. They replace `prefab.edit` / `scene.edit` / `HierarchyEdit` from `efa91f9`, whose op
  vocabulary duplicated the commands. `prefab.read` / `scene.read` / `asset` are covered by
  `prefab_tree`, `gameobject_*`, `component_get` and `asset_*`; `ui.snapshot` is now `ui_snapshot`.

- `prefab_*` (20) — assets (create, create_empty, create_variant, delete, duplicate, move, list,
  info, tree), instances (instantiate, apply, revert, overrides, unpack, replace, instance_info),
  Prefab Mode (open, save, close, stage_info).
- `gameobject_*` (12), `component_*` (5) — one set, optional `prefab` argument selects a prefab
  asset instead of the open scene.
- `scene_*` (7), `asset_*` (9), `selection_*` (3).
- `help` (alias `commands`) — Editor-scope command catalog from `InvokeCatalog`.

**Removed**

- `manage` and `EditorManageService`. Its actions are now `pause`, `tag_list`, `tag_add`,
  `tag_remove`, `layer_list`, `layer_add`, `layer_remove`, `tool_set`; `play` / `stop` / `refresh`
  already had their own commands, and `manage`'s blocking `Thread.Sleep` variants were dropped in
  favour of the deferred ones.

**Fixed**

- Declared `aliases` never dispatched: `CommandHandlerRegistry` now registers them as dispatcher
  keys and advertises them as capabilities, so `compile`'s long-standing `recompile` / `reload`
  aliases work for the first time.

**Testing**

- `npm run smoke` (`ucp-cli/scripts/smoke.js`) — 18 assertions over one Prefab round trip, ~3-4 min,
  human-readable pass/fail and a usable exit code. The fast tier next to the ~16 min
  `test:integration`.
- `executeCommandResilient` / `describeFailure` in `cli/client` — retries a submit that failed only
  because the Editor was momentarily not heartbeating (it stops ticking while importing assets), and
  joins `message` / `error_code` / `hint` so a failure is never reported as "unknown error". Root
  cause is logged as CLI-05.

**UI support** (found by actually building a login screen through the CLI)

- `screenshot view=ui` — the scene/game views are `camera.Render()`, which structurally cannot show
  a `ScreenSpaceOverlay` canvas. `view=ui` binds the canvas to a temporary camera: `prefab` renders
  a throwaway copy in a preview scene (no side effects), `node` renders the live canvas and restores
  `renderMode` in a `finally`. `PrefabStaging` was promoted to a shared `PreviewStage` for this.
- `{"builtin": "LegacyRuntime.ttf"}` object references — Unity's default font and UI sprites have no
  `Assets/` path and no discoverable GUID, so a UI built from scratch had no font and rendered
  nothing.
- `gameobject_create` / `component_add` returned `node: null` when given a UI component: adding one
  that requires a `RectTransform` makes Unity swap the Transform instance, and both methods were
  holding the old, now-destroyed reference. They hold the GameObject now.
- Play Mode writes to scene objects failed: Unity rejects undo registration there, and
  `MarkSceneDirty` in `EditTargetScope.Dispose` (now `Commit`) threw *after* the write had landed — reporting a
  successful edit as a failure. Both are now skipped in Play Mode.

- A failed node command no longer leaves half an edit (an orphan from `gameobject_create`, a
  component from `component_add`): every node command is a batch of one and reverts on failure.
- `overwrite` is gone. It deleted the target first, so the replacement got a new GUID, and a move
  onto its own path deleted the source. Asset writes are limited to `Assets/`, reject `..`, and
  deletes go to the trash.
- `scene_save all` raised Unity's save dialog for untitled scenes, which blocks the Editor; it
  refuses them now. `force` clears a dirty Prefab Mode stage so leaving it does not prompt.

**Infrastructure**

- `EditTargetCommand<TParams>` + `EditBatch` — node commands implemented `Apply(scope, p)`; the
  batch owned open / commit / revert. That base class is gone: a command is a `CliCommand`, and
  `EditBatch.Run` opens, commits, and reverts.

- `EditorActionCommand<TParams>` — command shell; services return payloads and throw for errors.
- `EditTargetScope` — the scene-or-prefab-asset abstraction that lets the content commands exist
  once instead of twice.
- `GameObjectPath` (`Root/Body`, `Body[1]`), `ComponentTypeResolver`, `JsonValues`,
  `SerializedValueReader` / `SerializedValueWriter` (symmetric: read output feeds back into writes),
  `AssetPaths`.
- Scene commands refuse to discard unsaved changes without `force`, because Unity's "save changes?"
  modal blocks the Editor main thread and therefore the whole agent poll loop.

## 2026-06-16 — UCP migration & doc refresh

- Renamed CLI binary `uctl` → **`ucp-cli`**; repo path **`packages/ucp`**.
- Removed `com.air.unity-connector`, `unity-cmd`, Editor HTTP stack.
- Stack: `ucp-cli` → `ucp-host` → FileQueue → `com.air.ucp-agent`.
- Data root: `~/.ucp/` (replaces `~/.unity-cmd/`).
- Commands: single `CliCommand` track; `state` returns `agent_state`.
- Rewrote `docs/*`, `README*`, `CONVENTIONS.md`, `TODO*.md` for UCP.

The `unity-cmd` / `com.air.unity-connector` notes that used to sit below this heading were removed. This entry is the record of that migration.
