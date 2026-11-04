# Agent Changelog — `packages/ucp`

## Unreleased — content command surface (agent 0.4.0)

Editor commands **15 → 79**. The agent could drive the Editor but could not touch project content;
the only escape hatch was `exec`. Command set modelled on
[CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp), reshaped to one operation per command.

**New families**

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
  `MarkSceneDirty` in `EditTargetScope.Dispose` threw *after* the write had landed — reporting a
  successful edit as a failure. Both are now skipped in Play Mode.

**Infrastructure**

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
