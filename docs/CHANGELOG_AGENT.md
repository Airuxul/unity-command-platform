# Agent Changelog — `unity-command-platform`

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

Every failure now has `error` = a code (`ucp-cli help <command>` lists them), `message` = the text,
`hint` = what to try. Before, `error` held the text and the CLI reported most failures as
`command_failed`. Successful results also carry `timing` (`queued_ms`, `unity_ms`, `run_ms`).

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
- The agent checks its inbox every editor tick instead of every 250 ms. With Unity minimized, 20
  pings: median 147 ms → 25 ms from handing the command over to reading the result.
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
  One lane per session; deadlines count from submission; `GET /commands/:id?wait=` long-polls;
  the outbox is watched with `fs.watch`. `/health` reports a build id and the CLI restarts an
  older idle host.
- Content layer: one copy of each helper (`SceneLookup`, `AssetQuery`, `TransformValues`,
  `TagsAndLayers`); `ParamKeys` holds the shared parameter names; binder-enforced `AllowedValues`.
- Tests: catalog snapshots lock every command's name and parameters, shape snapshots lock the keys
  of every result; integration suite 116 cases.

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

- `EditTargetCommand<TParams>` + `EditBatch` — node commands implement `Apply(scope, p)`; the
  batch owns open / commit / revert.

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

## 2026-06-04 and earlier

Entries before 2026-06-16 refer to the **legacy** `unity-cmd` + `com.air.unity-connector` HTTP architecture and are kept for historical context only.
