import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { SCRATCH_FOLDER, cleanupScratch, fails, ok, run } from "./helpers/content-scenario.js";
import { hasReadyEditorSession } from "./helpers/editor-commands.js";

/**
 * Scene lifecycle and the project tag/layer tables. Scenes are only ever loaded additively here so
 * the scene the user has open is never closed or modified.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

const SCENE = `${SCRATCH_FOLDER}/Workspace.unity`;
const TAG = "UcpIntegrationTag";
const LAYER = "UcpIntegrationLayer";

describe.skipIf(!runEditorTests)("editor workspace commands (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await cleanupScratch();
    await ok("asset_create_folder", { path: SCRATCH_FOLDER });
  });

  afterAll(async () => {
    await run("scene_close", { scene: SCENE, force: true });
    await run("tag_remove", { tag: TAG });
    await run("layer_remove", { layer: LAYER });
    await cleanupScratch();
  });

  it("creates, saves, reopens and closes an additive scene", async () => {
    await ok("scene_new", { setup: "empty", mode: "additive" });
    const saved = await ok("scene_save_as", { path: SCENE });
    expect(saved.path).toBe(SCENE);

    const closed = await ok("scene_close", { scene: SCENE });
    expect(closed.path).toBe(SCENE);

    const reopened = await ok("scene_open", { path: SCENE, mode: "additive" });
    expect(reopened.mode).toBe("Additive");

    const listed = await ok("scene_list", { folder: SCRATCH_FOLDER });
    const paths = (listed.scenes as Array<Record<string, unknown>>).map((s) => s.path);
    expect(paths).toContain(SCENE);

    await ok("gameobject_create", { scene: SCENE, name: "WorkspaceProbe" });
    await ok("scene_save", { scene: SCENE });

    const info = await ok("scene_info");
    const loaded = (info.loaded_scenes as Array<Record<string, unknown>>).map((s) => s.path);
    expect(loaded).toContain(SCENE);

    await ok("scene_close", { scene: SCENE });
  });

  it("refuses to discard unsaved scene changes without force", async () => {
    await ok("scene_open", { path: SCENE, mode: "additive" });
    await ok("gameobject_create", { scene: SCENE, name: "Unsaved" });

    // Unity would raise a modal "save changes?" dialog here, which would block the Editor main
    // thread and therefore the agent's poll loop. The command has to refuse instead.
    const message = await fails("scene_close", { scene: SCENE });
    expect(message).toContain("unsaved changes");

    const forced = await ok("scene_close", { scene: SCENE, force: true });
    expect(forced.closed).toBeTruthy();
  });

  it("adds and removes tags", async () => {
    const added = await ok("tag_add", { tag: TAG });
    expect(added.tags).toContain(TAG);

    expect(await fails("tag_add", { tag: TAG })).toContain("already exists");

    const removed = await ok("tag_remove", { tag: TAG });
    expect(removed.tags).not.toContain(TAG);
  });

  it("adds and removes layers", async () => {
    const added = await ok("layer_add", { layer: LAYER });
    expect(Number(added.index)).toBeGreaterThanOrEqual(8);

    const listed = await ok("layer_list");
    const names = (listed.layers as Array<Record<string, unknown>>).map((l) => l.name);
    expect(names).toContain(LAYER);

    const removed = await ok("layer_remove", { layer: LAYER });
    expect(removed.removed).toBe(true);

    expect(await fails("layer_remove", { layer: LAYER })).toContain("not found");
  });

  it("selects assets and reads the selection back", async () => {
    const selected = await ok("selection_set", { assets: [SCENE] });
    const assets = (selected.assets as Array<Record<string, unknown>>).map((a) => a.path);
    expect(assets).toContain(SCENE);

    await ok("selection_clear");
    const cleared = await ok("selection_get");
    expect(cleared.count).toBe(0);
  });
});
