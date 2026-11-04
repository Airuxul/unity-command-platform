import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  SCRATCH_FOLDER,
  cleanupScratch,
  fails,
  ok,
  releaseScratchStage,
  run,
} from "./helpers/content-scenario.js";
import { hasReadyEditorSession } from "./helpers/editor-commands.js";

/**
 * The acceptance test for "an agent can fully operate a prefab". Ordered on purpose: each step
 * builds on the previous one, which is what proves the commands compose — a set of independent
 * cases would not catch, say, a write that reports success but never reaches disk.
 *
 * Everything happens under Assets/UcpIntegration and in an additive scratch scene, so a run never
 * touches whatever the user has open.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

const PREFAB = `${SCRATCH_FOLDER}/Smoke.prefab`;
const VARIANT = `${SCRATCH_FOLDER}/Smoke_Variant.prefab`;
const NESTED = `${SCRATCH_FOLDER}/Gun.prefab`;
const SCRATCH_SCENE = `${SCRATCH_FOLDER}/Scratch.unity`;

function child(tree: Record<string, unknown>, name: string): Record<string, unknown> | undefined {
  const roots = tree.roots as Array<Record<string, unknown>> | undefined;
  const children = roots?.[0]?.children as Array<Record<string, unknown>> | undefined;
  return children?.find((c) => c.name === name);
}

describe.skipIf(!runEditorTests)("prefab content commands (Unity required)", () => {
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
    // Prefab Mode has to be left before the scratch folder can be deleted, and before anything
    // else can change scenes.
    await releaseScratchStage();
    await run("scene_close", { scene: SCRATCH_SCENE, force: true });
    await cleanupScratch();
  });

  it("creates a prefab asset without opening a scene", async () => {
    const created = await ok("prefab_create_empty", { path: PREFAB, name: "Smoke" });
    expect(created.path).toBe(PREFAB);
    expect(created.root).toBe("Smoke");
    expect(String(created.guid)).toHaveLength(32);

    const info = await ok("prefab_info", { path: PREFAB });
    expect(info.asset_type).toBe("Regular");
    expect(info.editable).toBe(true);
    expect(info.child_count).toBe(0);
  });

  it("adds a child node inside the prefab asset", async () => {
    const created = await ok("gameobject_create", {
      prefab: PREFAB,
      parent: "Smoke",
      name: "Body",
      primitive: "cube",
    });
    expect(created.node).toBe("Smoke/Body");
    // The payload states which scope ran, so a silently-wrong target shows up here.
    expect((created.target as Record<string, unknown>).kind).toBe("prefab_asset");
    expect(created.components).toContain("BoxCollider");

    const tree = await ok("prefab_tree", { path: PREFAB });
    expect(child(tree, "Body")).toBeTruthy();
  });

  it("writes and reads back component properties", async () => {
    const added = await ok("component_add", {
      prefab: PREFAB,
      node: "Smoke/Body",
      component: "Rigidbody",
      properties: { mass: 7.5, useGravity: false },
    });
    expect(added.component).toBe("Rigidbody");
    expect(added.properties_set).toContain("m_Mass");

    // Reading it back from a fresh load is the only proof the write reached the asset.
    const got = await ok("component_get", {
      prefab: PREFAB,
      node: "Smoke/Body",
      component: "Rigidbody",
    });
    const props = got.properties as Record<string, unknown>;
    expect(props.m_Mass).toBeCloseTo(7.5);
    expect(props.m_UseGravity).toBe(false);

    await ok("component_set", {
      prefab: PREFAB,
      node: "Smoke/Body",
      component: "Rigidbody",
      properties: { mass: 3.25, useGravity: true },
    });
    const after = await ok("component_get", {
      prefab: PREFAB,
      node: "Smoke/Body",
      component: "Rigidbody",
      property: "mass",
    });
    expect((after.properties as Record<string, unknown>).m_Mass).toBeCloseTo(3.25);
  });

  it("lists components and node details", async () => {
    const list = await ok("component_list", { prefab: PREFAB, node: "Smoke/Body" });
    const types = (list.components as Array<Record<string, unknown>>).map((c) => c.type);
    expect(types).toContain("Transform");
    expect(types).toContain("Rigidbody");

    const info = await ok("gameobject_info", { prefab: PREFAB, node: "Smoke/Body" });
    expect(info.name).toBe("Body");
    expect(info.parent).toBe("Smoke");
    expect(info.child_count).toBe(0);
  });

  it("moves, renames and reparents nodes", async () => {
    const moved = await ok("gameobject_transform", {
      prefab: PREFAB,
      node: "Smoke/Body",
      position: [1, 2, 3],
      scale: 2,
    });
    const transform = moved.transform as Record<string, unknown>;
    expect(transform.local_position).toEqual([1, 2, 3]);
    // A single number broadcasts to every component.
    expect(transform.local_scale).toEqual([2, 2, 2]);

    await ok("gameobject_rename", { prefab: PREFAB, node: "Smoke/Body", name: "Torso" });
    await ok("gameobject_create", { prefab: PREFAB, parent: "Smoke", name: "Arm" });

    const reparented = await ok("gameobject_reparent", {
      prefab: PREFAB,
      node: "Smoke/Arm",
      parent: "Smoke/Torso",
    });
    expect(reparented.node).toBe("Smoke/Torso/Arm");

    const duplicated = await ok("gameobject_duplicate", {
      prefab: PREFAB,
      node: "Smoke/Torso/Arm",
      name: "Arm2",
    });
    expect(duplicated.node).toBe("Smoke/Torso/Arm2");

    await ok("gameobject_activate", { prefab: PREFAB, node: "Smoke/Torso/Arm2", active: false });
    await ok("gameobject_tag", { prefab: PREFAB, node: "Smoke/Torso", tag: "EditorOnly" });
    await ok("gameobject_layer", { prefab: PREFAB, node: "Smoke/Torso", layer: "Default" });

    const tree = await ok("gameobject_tree", { prefab: PREFAB, depth: -1 });
    expect(tree.node_count).toBeGreaterThanOrEqual(4);
  });

  it("disambiguates same-named siblings with an index", async () => {
    await ok("gameobject_create", { prefab: PREFAB, parent: "Smoke", name: "Dup" });
    await ok("gameobject_create", { prefab: PREFAB, parent: "Smoke", name: "Dup" });

    const second = await ok("gameobject_rename", {
      prefab: PREFAB,
      node: "Smoke/Dup[1]",
      name: "DupSecond",
    });
    expect(second.previous_name).toBe("Dup");
    expect(second.node).toBe("Smoke/DupSecond");

    await ok("gameobject_delete", { prefab: PREFAB, node: "Smoke/DupSecond" });
    await ok("gameobject_delete", { prefab: PREFAB, node: "Smoke/Dup" });
  });

  it("removes components and nodes", async () => {
    const removed = await ok("component_remove", {
      prefab: PREFAB,
      node: "Smoke/Torso",
      component: "Rigidbody",
    });
    expect(removed.remaining).not.toContain("Rigidbody");

    await ok("gameobject_delete", { prefab: PREFAB, node: "Smoke/Torso/Arm2" });
    const tree = await ok("prefab_tree", { path: PREFAB });
    const torso = child(tree, "Torso");
    expect((torso?.children as unknown[])?.length).toBe(1);
  });

  it("nests one prefab inside another", async () => {
    await ok("prefab_create_empty", { path: NESTED, name: "Gun", primitive: "cube" });

    const nested = await ok("prefab_instantiate", {
      source: NESTED,
      prefab: PREFAB,
      parent: "Smoke/Torso",
      name: "Weapon",
    });
    expect(nested.nested).toBe(true);
    expect(nested.node).toBe("Smoke/Torso/Weapon");

    const tree = await ok("prefab_tree", { path: PREFAB });
    const torso = child(tree, "Torso");
    const weapon = (torso?.children as Array<Record<string, unknown>>).find(
      (c) => c.name === "Weapon",
    );
    expect(weapon?.nested_prefab).toBe(NESTED);

    const deps = await ok("asset_dependencies", { path: PREFAB });
    expect(deps.dependencies).toContain(NESTED);

    expect(await fails("prefab_instantiate", { source: PREFAB, prefab: PREFAB })).toContain(
      "itself",
    );
  });

  it("creates a variant and copies, moves and deletes prefab assets", async () => {
    const variant = await ok("prefab_create_variant", { path: PREFAB, target: VARIANT });
    expect(variant.is_variant).toBe(true);

    const variantInfo = await ok("prefab_info", { path: VARIANT });
    expect(variantInfo.base_prefab).toBe(PREFAB);

    const copyPath = `${SCRATCH_FOLDER}/Smoke_Copy.prefab`;
    const movedPath = `${SCRATCH_FOLDER}/Smoke_Moved.prefab`;
    const copy = await ok("prefab_duplicate", { path: PREFAB, target: copyPath });
    const moved = await ok("prefab_move", { path: copyPath, target: movedPath });
    // A move keeps the GUID; a copy must not.
    expect(moved.guid).toBe(copy.guid);
    await ok("prefab_delete", { path: movedPath });

    expect(await fails("prefab_info", { path: movedPath })).toContain("not found");
  });

  it("resolves asset paths, guids and metadata", async () => {
    const info = await ok("asset_info", { path: PREFAB, dependencies: true, subAssets: true });
    expect(info.is_folder).toBe(false);
    expect(info.type).toBe("GameObject");

    const guid = await ok("asset_guid", { path: PREFAB });
    const back = await ok("asset_guid", { guid: guid.guid });
    expect(back.path).toBe(PREFAB);

    expect(await fails("asset_guid", { path: PREFAB, guid: guid.guid })).toContain("exactly one");

    const assetCopy = `${SCRATCH_FOLDER}/Smoke_Asset.prefab`;
    const assetMoved = `${SCRATCH_FOLDER}/Sub/Smoke_Asset.prefab`;
    await ok("asset_duplicate", { path: PREFAB, target: assetCopy });
    // The parent folder does not exist yet — move must create it rather than fail.
    await ok("asset_move", { path: assetCopy, target: assetMoved });
    await ok("asset_delete", { path: `${SCRATCH_FOLDER}/Sub` });
  });

  it("drives prefab instances in a scene: overrides, apply, revert, replace, unpack", async () => {
    await ok("scene_new", { setup: "empty", mode: "additive" });
    await ok("scene_save_as", { path: SCRATCH_SCENE });

    const instance = await ok("prefab_instantiate", { source: PREFAB, scene: SCRATCH_SCENE });
    expect(instance.nested).toBe(false);
    const node = String(instance.node);

    const link = await ok("prefab_instance_info", { node, scene: SCRATCH_SCENE });
    expect(link.is_instance_root).toBe(true);
    expect(link.source).toBe(PREFAB);
    expect(link.status).toBe("Connected");

    // A fresh instance already carries Unity's default overrides (root transform and name), which
    // are never applied — so the override checks have to use a child, not the root.
    const fresh = await ok("prefab_overrides", { node, scene: SCRATCH_SCENE });
    expect(fresh.has_overrides).toBe(false);

    await ok("gameobject_transform", {
      node: `${node}/Torso`,
      scene: SCRATCH_SCENE,
      position: [5, 0, 0],
    });
    const overrides = await ok("prefab_overrides", { node, scene: SCRATCH_SCENE });
    expect(overrides.has_overrides).toBe(true);
    const mods = overrides.property_modifications as Array<Record<string, unknown>>;
    expect(mods.some((m) => String(m.property).includes("m_LocalPosition"))).toBe(true);

    const applied = await ok("prefab_apply", { node, scene: SCRATCH_SCENE });
    expect(applied.applied_to).toBe(PREFAB);
    const assetAfterApply = await ok("gameobject_info", { prefab: PREFAB, node: "Smoke/Torso" });
    expect((assetAfterApply.transform as Record<string, unknown>).local_position).toEqual([
      5, 0, 0,
    ]);

    await ok("gameobject_transform", {
      node: `${node}/Torso`,
      scene: SCRATCH_SCENE,
      position: [9, 0, 0],
    });
    await ok("prefab_revert", { node, scene: SCRATCH_SCENE });
    const afterRevert = await ok("prefab_overrides", { node, scene: SCRATCH_SCENE });
    expect(afterRevert.has_overrides).toBe(false);

    const replaced = await ok("prefab_replace", { node, scene: SCRATCH_SCENE, path: VARIANT });
    expect(replaced.source).toBe(VARIANT);
    expect(replaced.overrides_preserved).toBe(false);

    // Unpacking the outermost root of a *variant* instance strips one level of linkage, leaving an
    // instance of the variant's base. Only completely=true breaks the link entirely.
    const unpacked = await ok("prefab_unpack", {
      node: String(replaced.node),
      scene: SCRATCH_SCENE,
    });
    expect(unpacked.still_instance).toBe(true);
    expect(unpacked.still_instance_of).toBe(PREFAB);

    const fully = await ok("prefab_unpack", {
      node: String(replaced.node),
      scene: SCRATCH_SCENE,
      completely: true,
    });
    expect(fully.still_instance).toBe(false);

    await ok("selection_set", { node: [String(replaced.node)], scene: SCRATCH_SCENE });
    await ok("gameobject_delete", { node: String(replaced.node), scene: SCRATCH_SCENE });
    await ok("scene_save", { scene: SCRATCH_SCENE });
  });

  it("opens, saves and closes Prefab Mode", async () => {
    const opened = await ok("prefab_open", { path: PREFAB });
    expect(opened.open).toBe(true);
    expect(opened.path).toBe(PREFAB);

    // While the stage is open, content commands must reuse it rather than load a second copy.
    const edited = await ok("gameobject_create", {
      prefab: PREFAB,
      parent: "Smoke",
      name: "Staged",
    });
    expect((edited.target as Record<string, unknown>).kind).toBe("prefab_stage");

    await ok("prefab_save");

    // Deleting the asset under a live stage is another route to a blocking modal.
    expect(await fails("prefab_delete", { path: PREFAB })).toContain("open in Prefab Mode");

    const closed = await ok("prefab_close", { save: true });
    expect(closed.closed).toBe(true);

    const stage = await ok("prefab_stage_info");
    expect(stage.open).toBe(false);

    const tree = await ok("prefab_tree", { path: PREFAB });
    expect(child(tree, "Staged")).toBeTruthy();
  });

  it("never leaves Prefab Mode dirty, so closing cannot raise a blocking modal", async () => {
    await ok("prefab_open", { path: PREFAB });
    await ok("gameobject_create", { prefab: PREFAB, parent: "Smoke", name: "Throwaway" });

    // Editing through the open stage writes to the asset and clears the stage's dirty flag. If it
    // did not, Unity would raise a "save changes?" modal on the way out — and that modal blocks the
    // Editor main thread, which is also the agent's poll loop, hanging every later command.
    const stage = await ok("prefab_stage_info");
    expect(stage.dirty).toBe(false);

    await ok("gameobject_delete", { prefab: PREFAB, node: "Smoke/Throwaway" });
    await ok("prefab_close", { save: false });
    expect((await ok("prefab_stage_info")).open).toBe(false);
  });

  it("reports actionable errors instead of silent no-ops", async () => {
    expect(await fails("gameobject_info", { prefab: PREFAB, node: "Smoke/Nope" })).toContain(
      "Children:",
    );
    expect(
      await fails("component_get", { prefab: PREFAB, node: "Smoke", component: "Rigidbody" }),
    ).toContain("It has:");
    expect(
      await fails("component_add", { prefab: PREFAB, node: "Smoke", component: "Nonsense" }),
    ).toContain("Unknown component type");
    expect(
      await fails("component_add", { prefab: PREFAB, node: "Smoke", component: "Transform" }),
    ).toContain("already has one");
    expect(await fails("gameobject_delete", { prefab: PREFAB, node: "Smoke" })).toContain(
      "root node",
    );
    expect(await fails("prefab_apply", { prefab: PREFAB, node: "Smoke" })).toContain(
      "not part of a prefab instance",
    );
  });
});
