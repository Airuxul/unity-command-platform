import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { SCRATCH_FOLDER, cleanupScratch, ok, rejected, run } from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";

/**
 * The commands whose Undo is written by hand: Unity's undo does not record AssetDatabase moves,
 * deletes, new folders or new asset files. Each case runs the command as the first step of a
 * group whose last step fails, so the group takes it back, and checks the project is as before.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";
const ROOT = `${SCRATCH_FOLDER}/Undo`;
const BASE = `${ROOT}/Base.prefab`;
const HOLDER = `${ROOT}/Holder.prefab`;

const FAIL = { command: "asset_info", args: { path: `${ROOT}/DoesNotExist.asset` } };

function projectFile(assetPath: string): string {
  return path.join(gameDemoProjectPath(), ...assetPath.split("/"));
}

function exists(assetPath: string): boolean {
  return fs.existsSync(projectFile(assetPath));
}

function meta(assetPath: string): string {
  return fs.readFileSync(`${projectFile(assetPath)}.meta`, "utf8");
}

async function guid(assetPath: string): Promise<string> {
  return String((await ok("asset_guid", { path: assetPath })).guid);
}

/** Runs `step` and then a failing step, and returns the group's report on the first one. */
async function takenBack(step: { command: string; args: Record<string, unknown> }) {
  const failed = await rejected("group", { steps: [step, FAIL] });
  const steps = (failed.data as { steps: Array<{ ok: boolean; undone: boolean; message: string }> }).steps;
  expect(steps[0].ok, steps[0].message).toBe(true);
  expect(steps[0].undone).toBe(true);
  const rollback = (failed.data as { rollback: { ok: boolean; errors: unknown[] } }).rollback;
  expect(rollback.errors).toEqual([]);
  return steps[0];
}

describe.skipIf(!runEditorTests)("hand-written undo (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await cleanupScratch();
    await ok("asset_create_folder", { path: ROOT });
    await ok("prefab_create_empty", { path: BASE, name: "Base" });
    // Holder nests Base, so deleting Base and restoring it must keep Holder's reference.
    await ok("prefab_create_empty", { path: HOLDER, name: "Holder" });
    await ok("prefab_instantiate", { prefab: HOLDER, source: BASE, parent: "Holder" });
  });

  afterAll(async () => {
    await cleanupScratch();
  });

  it("asset_create_folder: removes every folder it created, deepest first", async () => {
    await takenBack({ command: "asset_create_folder", args: { path: `${ROOT}/A/B/C` } });
    expect(exists(`${ROOT}/A`)).toBe(false);
    expect(exists(`${ROOT}/A.meta`)).toBe(false);
  });

  it("asset_create_folder: leaves a folder that was already there", async () => {
    await ok("asset_create_folder", { path: `${ROOT}/Existing` });
    await takenBack({ command: "asset_create_folder", args: { path: `${ROOT}/Existing/Inner` } });
    expect(exists(`${ROOT}/Existing`)).toBe(true);
    expect(exists(`${ROOT}/Existing/Inner`)).toBe(false);
  });

  it("asset_move and prefab_move: move back with the same GUID, and remove the folders made for the target", async () => {
    const before = await guid(BASE);
    const metaBefore = meta(BASE);
    await takenBack({ command: "asset_move", args: { path: BASE, target: `${ROOT}/New/Deep/Base.prefab` } });
    expect(await guid(BASE)).toBe(before);
    expect(meta(BASE)).toBe(metaBefore);
    expect(exists(`${ROOT}/New`)).toBe(false);

    await takenBack({ command: "prefab_move", args: { path: BASE, target: `${ROOT}/Renamed.prefab` } });
    expect(await guid(BASE)).toBe(before);
    expect(exists(`${ROOT}/Renamed.prefab`)).toBe(false);
  });

  it("asset_duplicate, prefab_duplicate, prefab_create_empty, prefab_create_variant: delete what they created", async () => {
    await takenBack({ command: "asset_duplicate", args: { path: BASE, target: `${ROOT}/Copies/Copy.prefab` } });
    expect(exists(`${ROOT}/Copies`)).toBe(false);

    await takenBack({ command: "prefab_duplicate", args: { path: BASE, target: `${ROOT}/Copy2.prefab` } });
    expect(exists(`${ROOT}/Copy2.prefab`)).toBe(false);

    await takenBack({ command: "prefab_create_empty", args: { path: `${ROOT}/Fresh/Empty.prefab` } });
    expect(exists(`${ROOT}/Fresh`)).toBe(false);

    await takenBack({ command: "prefab_create_variant", args: { path: BASE, target: `${ROOT}/BaseVariant.prefab` } });
    expect(exists(`${ROOT}/BaseVariant.prefab`)).toBe(false);
  });

  it("asset_delete and prefab_delete: restore the asset with its GUID, so references still resolve", async () => {
    const before = await guid(BASE);
    await takenBack({ command: "asset_delete", args: { path: BASE } });
    expect(await guid(BASE)).toBe(before);
    const deps = (await ok("asset_dependencies", { path: HOLDER })).dependencies as string[];
    expect(deps).toContain(BASE);

    await takenBack({ command: "prefab_delete", args: { path: BASE } });
    expect(await guid(BASE)).toBe(before);
  });

  it("asset_delete of a folder restores everything in it", async () => {
    await ok("asset_create_folder", { path: `${ROOT}/Box/Inner` });
    await ok("prefab_create_empty", { path: `${ROOT}/Box/Inner/Item.prefab`, name: "Item" });
    const item = await guid(`${ROOT}/Box/Inner/Item.prefab`);
    const folder = await guid(`${ROOT}/Box`);

    await takenBack({ command: "asset_delete", args: { path: `${ROOT}/Box` } });
    expect(await guid(`${ROOT}/Box`)).toBe(folder);
    expect(await guid(`${ROOT}/Box/Inner/Item.prefab`)).toBe(item);
  });

  it("prefab_create: deletes the new file and disconnects the node", async () => {
    await ok("gameobject_create", { prefab: HOLDER, parent: "Holder", name: "Source" });
    await takenBack({
      command: "prefab_create",
      args: { prefab: HOLDER, node: "Holder/Source", path: `${ROOT}/Made/Source.prefab` },
    });
    expect(exists(`${ROOT}/Made`)).toBe(false);
    const info = await ok("prefab_instance_info", { prefab: HOLDER, node: "Holder/Source" });
    expect(info.is_part_of_instance).toBe(false);
  });

  it("does not delete an asset that was already there when the command failed", async () => {
    await ok("prefab_create_empty", { path: `${ROOT}/Taken.prefab`, name: "Taken" });
    const failed = await rejected("prefab_create_empty", { path: `${ROOT}/Taken.prefab` });
    expect(failed.message).toContain("already exists");
    expect(exists(`${ROOT}/Taken.prefab`)).toBe(true);

    const inGroup = await rejected("group", {
      steps: [{ command: "asset_duplicate", args: { path: BASE, target: `${ROOT}/Taken.prefab` } }],
    });
    expect(inGroup.message).toContain("already exists");
    expect(exists(`${ROOT}/Taken.prefab`)).toBe(true);
  });

  it("a command on its own keeps no copy: nothing is left under Library/ucp/undo", async () => {
    await ok("prefab_create_empty", { path: `${ROOT}/Plain.prefab`, name: "Plain" });
    await ok("asset_delete", { path: `${ROOT}/Plain.prefab` });
    const undoRoot = path.join(gameDemoProjectPath(), "Library", "ucp", "undo");
    const left = fs.existsSync(undoRoot) ? fs.readdirSync(undoRoot) : [];
    expect(left).toEqual([]);
    expect((await run("asset_info", { path: `${ROOT}/Plain.prefab` })).ok).toBe(false);
  });
});
