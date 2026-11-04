import { readFileSync } from "node:fs";
import { join } from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  SCRATCH_FOLDER,
  cleanupScratch,
  fails,
  ok,
  releaseScratchStage,
  run,
} from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";

/**
 * prefab_edit / scene_edit, and the rule they share with every node command: a write is kept only
 * if all of it succeeded. Ordered like prefab-commands.test.ts — later cases reuse earlier assets.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

const PREFAB = `${SCRATCH_FOLDER}/Batch.prefab`;
const SCENE = `${SCRATCH_FOLDER}/Batch.unity`;

function prefabBytes(): Buffer {
  return readFileSync(join(gameDemoProjectPath(), PREFAB));
}

async function findInScene(name: string): Promise<number> {
  const found = await ok("gameobject_find", { scene: SCENE, name, exact: true });
  return Number(found.count);
}

describe.skipIf(!runEditorTests)("batch edits (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await cleanupScratch();
    await ok("asset_create_folder", { path: SCRATCH_FOLDER });
    await ok("prefab_create_empty", { path: PREFAB, name: "Batch" });
    await ok("scene_new", { setup: "empty", mode: "additive", path: SCENE });
  });

  afterAll(async () => {
    await releaseScratchStage();
    await run("scene_close", { scene: SCENE, force: true });
    await cleanupScratch();
  });

  it("runs every op on one prefab and saves once", async () => {
    const edited = await ok("prefab_edit", {
      prefab: PREFAB,
      ops: [
        { command: "gameobject_create", args: { parent: "Batch", name: "Panel" } },
        { command: "component_add", args: { node: "Batch/Panel", component: "Image" } },
        { command: "gameobject_rename", args: { node: "Batch/Panel", name: "Card" } },
      ],
    });
    const results = edited.results as Array<{ command: string }>;
    expect(results.map((r) => r.command)).toEqual([
      "gameobject_create",
      "component_add",
      "gameobject_rename",
    ]);

    const info = await ok("gameobject_info", { prefab: PREFAB, node: "Batch/Card" });
    expect(info.components).toContain("Image");
  });

  it("keeps nothing when an op fails, and says which one", async () => {
    const before = prefabBytes();

    const reason = await fails("prefab_edit", {
      prefab: PREFAB,
      ops: [
        { command: "gameobject_create", args: { parent: "Batch", name: "Orphan" } },
        { command: "component_add", args: { node: "Batch/Orphan", component: "NoSuchComponent" } },
      ],
    });
    expect(reason).toContain("ops[1] component_add");
    expect(prefabBytes().equals(before)).toBe(true);
  });

  it("checks every op before touching the target", async () => {
    const before = prefabBytes();

    const onOp = await fails("prefab_edit", {
      prefab: PREFAB,
      ops: [
        { command: "gameobject_create", args: { parent: "Batch", name: "Early" } },
        { command: "gameobject_create", args: { prefab: PREFAB, parent: "Batch", name: "Late" } },
      ],
    });
    expect(onOp).toContain("belong on the batch");

    const notBatchable = await fails("prefab_edit", {
      prefab: PREFAB,
      ops: [{ command: "asset_delete", args: { path: PREFAB } }],
    });
    expect(notBatchable).toContain("cannot run in a batch");

    expect(prefabBytes().equals(before)).toBe(true);
  });

  it("undoes a failed scene batch", async () => {
    await fails("scene_edit", {
      scene: SCENE,
      ops: [
        { command: "gameobject_create", args: { name: "BatchProbe" } },
        { command: "component_add", args: { node: "BatchProbe", component: "NoSuchComponent" } },
      ],
    });
    expect(await findInScene("BatchProbe")).toBe(0);

    await ok("scene_edit", {
      scene: SCENE,
      ops: [{ command: "gameobject_create", args: { name: "BatchProbe" } }],
    });
    expect(await findInScene("BatchProbe")).toBe(1);
  });

  it("leaves no orphan when a single create fails validation", async () => {
    const reason = await fails("gameobject_create", {
      scene: SCENE,
      name: "TaggedProbe",
      tag: "NoSuchTag",
    });
    expect(reason).toContain("NoSuchTag");
    expect(await findInScene("TaggedProbe")).toBe(0);
  });
});
