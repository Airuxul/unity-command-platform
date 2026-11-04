import { expect } from "vitest";
import { describeFailure, executeCommandResilient } from "#cli/client/ucp-host-client.js";
import type { CliResult } from "#shared/types.js";
import { gameDemoProjectPath } from "./editor-commands.js";

/**
 * Command types exercised by the ordered content scenarios rather than by a standalone case in
 * EDITOR_COMMAND_CASES. The catalog test unions the two lists, so a new command has to land in one
 * of them — it cannot quietly ship untested.
 */
export const SCENARIO_COMMANDS: readonly string[] = [
  // prefab assets
  "prefab_create",
  "prefab_create_empty",
  "prefab_create_variant",
  "prefab_delete",
  "prefab_duplicate",
  "prefab_move",
  "prefab_info",
  "prefab_tree",
  // prefab instances
  "prefab_instantiate",
  "prefab_apply",
  "prefab_revert",
  "prefab_overrides",
  "prefab_unpack",
  "prefab_replace",
  "prefab_instance_info",
  // prefab mode
  "prefab_open",
  "prefab_save",
  "prefab_close",
  // gameobjects
  "gameobject_create",
  "gameobject_delete",
  "gameobject_duplicate",
  "gameobject_rename",
  "gameobject_reparent",
  "gameobject_transform",
  "gameobject_activate",
  "gameobject_tag",
  "gameobject_layer",
  "gameobject_info",
  "gameobject_tree",
  // components
  "component_add",
  "component_remove",
  "component_list",
  "component_get",
  "component_set",
  // assets
  "asset_create_folder",
  "asset_delete",
  "asset_move",
  "asset_duplicate",
  "asset_info",
  "asset_dependencies",
  "asset_guid",
  // scenes
  "scene_open",
  "scene_new",
  "scene_save",
  "scene_save_as",
  "scene_close",
  // project settings
  "tag_add",
  "tag_remove",
  "layer_add",
  "layer_remove",
  // selection
  "selection_set",
];

/** Scratch space every scenario creates and removes, so a failed run leaves one folder behind. */
export const SCRATCH_FOLDER = "Assets/UcpIntegration";

export async function run(
  type: string,
  args: Record<string, unknown> = {},
  timeout = 30_000,
): Promise<CliResult> {
  // executeCommandResilient rides out the Editor going briefly unresponsive while it imports
  // assets — a scheduling artefact that would otherwise make this suite flaky.
  return executeCommandResilient(type, {
    project: gameDemoProjectPath(),
    sessionType: "editor",
    timeout,
    args,
  });
}

/** Runs a command and fails the test with the agent's own message if it did not succeed. */
export async function ok(
  type: string,
  args: Record<string, unknown> = {},
  timeout = 30_000,
): Promise<Record<string, unknown>> {
  const result = await run(type, args, timeout);
  if (!result.ok) {
    throw new Error(`${type} failed: ${describeFailure(result) || "unknown error"}`);
  }
  return (result.data ?? {}) as Record<string, unknown>;
}

/** Runs a command expecting rejection, and returns the reason so the test can assert on it. */
export async function fails(type: string, args: Record<string, unknown> = {}): Promise<string> {
  const result = await run(type, args);
  expect(result.ok, `${type} was expected to fail but succeeded`).toBe(false);
  // A dead session is an infrastructure problem, not the rejection the test is asserting about.
  expect(result.error_code, `${type}: agent unreachable`).not.toBe("no_ready_session");
  return describeFailure(result);
}

/**
 * Unloads any scratch scene left behind by an earlier run, so the scratch folder can be deleted.
 * A scene cannot be closed while it is the only one loaded, so an empty additive scene is added
 * first — that way the suite recovers on its own without ever touching the user's own scene.
 */
export async function releaseScratchScenes(): Promise<void> {
  const info = await run("scene_info");
  if (!info.ok) return;

  const loaded = ((info.data ?? {}) as Record<string, unknown>).loaded_scenes as
    | Array<{ path?: string }>
    | undefined;
  const scratch = (loaded ?? []).filter((s) => s.path?.startsWith(`${SCRATCH_FOLDER}/`));
  if (scratch.length === 0) return;

  if (scratch.length === (loaded ?? []).length) {
    await run("scene_new", { setup: "empty", mode: "additive" });
  }
  for (const scene of scratch) {
    await run("scene_close", { scene: scene.path, force: true });
  }
}

/**
 * Leaves Prefab Mode only when it holds a scratch prefab. Closing with force discards unsaved
 * changes, so a prefab the user has open must never be closed by the suite.
 */
export async function releaseScratchStage(): Promise<void> {
  const info = await run("prefab_stage_info");
  const path = ((info.data ?? {}) as Record<string, unknown>).path;
  if (typeof path === "string" && path.startsWith(`${SCRATCH_FOLDER}/`)) {
    await run("prefab_close", { save: false, force: true });
  }
}

export async function cleanupScratch(): Promise<void> {
  // Prefab Mode and loaded scenes both pin assets in the scratch folder.
  await releaseScratchStage();
  await releaseScratchScenes();

  const result = await run("asset_delete", { path: SCRATCH_FOLDER });
  // Nothing to clean up on a first run — that is not a failure.
  if (!result.ok && !String(result.message ?? "").includes("not found")) {
    // Leave a breadcrumb but do not fail teardown.
    console.warn("scratch cleanup:", describeFailure(result));
  }
}
