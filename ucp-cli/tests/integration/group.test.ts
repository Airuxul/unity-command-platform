import fs from "node:fs";
import { join } from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  SCRATCH_FOLDER,
  cleanupScratch,
  expectShapesUnchanged,
  fails,
  ok,
  rejected,
  releaseScratchStage,
  run,
} from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";

/**
 * `group`: several commands run as one. Every assertion batch-commands.test.ts made about
 * prefab_edit / scene_edit has a case here:
 *
 * | prefab_edit / scene_edit                        | here                                              |
 * |-------------------------------------------------|---------------------------------------------------|
 * | runs every op on one prefab and saves once      | "runs every step on one prefab and saves it once" |
 * | keeps nothing when an op fails, says which      | "keeps nothing when a step fails, and says which" |
 * | checks every op before touching the target      | "checks every step before running any"            |
 * | expect checked after the ops before it          | "checks each step's expect ..."                   |
 * | dry_run reports results and keeps nothing       | "dry_run ..."                                     |
 * | undoes a failed scene batch                     | "takes back a failed scene group"                 |
 * | no orphan when a single create fails            | "leaves no orphan when a single command fails"    |
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

const PREFAB = `${SCRATCH_FOLDER}/Group.prefab`;
const OTHER = `${SCRATCH_FOLDER}/GroupOther.prefab`;
const SCENE = `${SCRATCH_FOLDER}/Group.unity`;
const PROBE = "Assets/UcpGroupProbe";
const COMPILE_TIMEOUT = 180_000;

/** Counts imports of the scratch prefabs, so "saved once" is measured rather than assumed. */
const IMPORT_COUNTER = `using UnityEditor;

public sealed class UcpGroupImportCounter : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
        {
            if (path == "${PREFAB}")
                SessionState.SetInt("ucp.group.imports", SessionState.GetInt("ucp.group.imports", 0) + 1);
        }
    }
}
`;

function projectFile(assetPath: string): string {
  return join(gameDemoProjectPath(), ...assetPath.split("/"));
}

function bytes(assetPath: string): Buffer {
  return fs.readFileSync(projectFile(assetPath));
}

async function exec(code: string): Promise<unknown> {
  return (await ok("exec", { code })).result;
}

async function imports(): Promise<number> {
  return Number(await exec(`return UnityEditor.SessionState.GetInt("ucp.group.imports", 0);`));
}

async function findInScene(name: string): Promise<number> {
  const found = await ok("gameobject_find", { scene: SCENE, name, exact: true });
  return Number(found.count);
}

async function guid(path: string): Promise<string> {
  return String((await ok("asset_guid", { path })).guid);
}

function exists(assetPath: string): boolean {
  return fs.existsSync(projectFile(assetPath));
}

/** A step that always fails without changing anything. */
const FAIL = { command: "asset_info", args: { path: `${SCRATCH_FOLDER}/DoesNotExist.asset` } };

type Step = { index: number; command: string; ok: boolean; undone: boolean; error_code?: string };

function steps(data: unknown): Step[] {
  return ((data as Record<string, unknown>).steps as Step[]) ?? [];
}

describe.skipIf(!runEditorTests)("group (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await cleanupScratch();
    fs.mkdirSync(projectFile(`${PROBE}/Editor`), { recursive: true });
    fs.writeFileSync(projectFile(`${PROBE}/Editor/UcpGroupImportCounter.cs`), IMPORT_COUNTER);
    await ok("compile", {}, COMPILE_TIMEOUT);

    await ok("asset_create_folder", { path: SCRATCH_FOLDER });
    await ok("prefab_create_empty", { path: PREFAB, name: "Group" });
    await ok("prefab_create_empty", { path: OTHER, name: "Other" });
    await ok("scene_new", { setup: "empty", mode: "additive", path: SCENE });
  }, COMPILE_TIMEOUT + 60_000);

  afterAll(async () => {
    await releaseScratchStage();
    await run("scene_close", { scene: SCENE, force: true });
    await cleanupScratch();
    await run("asset_delete", { path: PROBE });
    await run("compile", {}, COMPILE_TIMEOUT);
  }, COMPILE_TIMEOUT + 60_000);

  it("runs every step on one prefab and saves it once", async () => {
    const before = await imports();
    const data = await ok("group", {
      steps: [
        { command: "gameobject_create", args: { prefab: PREFAB, parent: "Group", name: "Panel" } },
        { command: "component_add", args: { prefab: PREFAB, node: "Group/Panel", component: "Image" } },
        { command: "gameobject_rename", args: { prefab: PREFAB, node: "Group/Panel", name: "Card" } },
      ],
    });
    expect(steps(data).map((s) => [s.command, s.ok])).toEqual([
      ["gameobject_create", true],
      ["component_add", true],
      ["gameobject_rename", true],
    ]);
    expect(await imports()).toBe(before + 1);

    const info = await ok("gameobject_info", { prefab: PREFAB, node: "Group/Card" });
    const types = (info.components as Array<{ type: string }>).map((c) => c.type);
    expect(types).toContain("Image");
  });

  it("keeps nothing when a step fails, and says which", async () => {
    const before = bytes(PREFAB);
    const failed = await rejected("group", {
      steps: [
        { command: "gameobject_create", args: { prefab: PREFAB, parent: "Group", name: "Orphan" } },
        { command: "component_add", args: { prefab: PREFAB, node: "Group/Orphan", component: "NoSuchComponent" } },
      ],
    });
    expect(failed.message).toContain("steps[1] component_add");
    expect(failed.error_code).toBe("type_not_found");
    const data = failed.data as Record<string, unknown>;
    expect(data.failed_at).toBe(1);
    expect(steps(data).map((s) => [s.ok, s.undone])).toEqual([
      [true, true],
      [false, true],
    ]);
    expect(bytes(PREFAB).equals(before)).toBe(true);
  });

  it("checks every step before running any", async () => {
    const before = bytes(PREFAB);
    const create = { command: "gameobject_create", args: { prefab: PREFAB, parent: "Group", name: "Early" } };

    const unknown = await rejected("group", { steps: [create, { command: "no_such_command", args: {} }] });
    expect(unknown.error_code).toBe("invalid_argument");
    expect(unknown.message).toContain("steps[1] 'no_such_command'");

    const badArgs = await rejected("group", {
      steps: [create, { command: "gameobject_rename", args: { prefab: PREFAB, nmae: "x" } }],
    });
    expect(badArgs.error_code).toBe("invalid_argument");
    expect(badArgs.message).toContain("steps[1] gameobject_rename");

    const nested = await rejected("group", { steps: [create, { command: "group", args: { steps: [create] } }] });
    expect(nested.message).toContain("cannot contain another group");

    const expectOnPlain = await rejected("group", {
      steps: [{ command: "asset_info", args: { path: PREFAB }, expect: { node: "Group", exists: true } }],
    });
    expect(expectOnPlain.message).toContain("'expect' only applies");

    const continueWithAll = await rejected("group", { steps: [create], rollback: "all", on_error: "continue" });
    expect(continueWithAll.message).toContain("on_error=continue needs rollback=failed");

    expect(await fails("group", { steps: [] })).toContain("at least one");
    expect(bytes(PREFAB).equals(before)).toBe(true);
  });

  it("checks each step's expect after the steps before it, and rolls back when one fails", async () => {
    const before = bytes(PREFAB);
    const failed = await rejected("group", {
      steps: [
        {
          command: "gameobject_create",
          args: { prefab: PREFAB, parent: "Group", name: "Guarded" },
          expect: { node: "Group/Guarded", exists: false },
        },
        {
          command: "gameobject_rename",
          args: { prefab: PREFAB, node: "Group/Guarded", name: "Renamed" },
          expect: { exists: true, components: ["Image"] },
        },
      ],
    });
    expect(failed.error_code).toBe("expectation_failed");
    expect(failed.message).toContain("steps[1] gameobject_rename expect failed");
    expect(failed.message).toContain("has no Image");
    expect(bytes(PREFAB).equals(before)).toBe(true);

    await ok("group", {
      steps: [
        {
          command: "gameobject_create",
          args: { prefab: PREFAB, parent: "Group", name: "Guarded", components: ["Image"] },
          expect: { node: "Group/Guarded", exists: false },
        },
        {
          command: "gameobject_rename",
          args: { prefab: PREFAB, node: "Group/Guarded", name: "Renamed" },
          expect: { exists: true, components: ["Image"] },
        },
        { command: "gameobject_delete", args: { prefab: PREFAB, node: "Group/Renamed" } },
      ],
    });

    const badShape = await rejected("group", {
      steps: [{ command: "gameobject_create", args: { prefab: PREFAB, name: "X" }, expect: { exist: true } }],
    });
    expect(badShape.error_code).toBe("invalid_argument");
    expect(badShape.message).toContain("unknown key 'exist'");
  });

  it("dry_run reports every step's real result and keeps nothing", async () => {
    const before = bytes(PREFAB);
    const preview = await ok("group", {
      dry_run: true,
      steps: [
        { command: "gameobject_create", args: { prefab: PREFAB, parent: "Group", name: "Preview" } },
        { command: "component_add", args: { prefab: PREFAB, node: "Group/Preview", component: "Image" } },
        { command: "gameobject_create", args: { scene: SCENE, name: "PreviewProbe" } },
      ],
    });
    expect(preview.dry_run).toBe(true);
    const first = steps(preview)[0] as unknown as { data: Record<string, unknown> };
    expect(first.data.node).toBe("Group/Preview");
    expect(steps(preview).every((s) => s.undone)).toBe(true);
    expect(bytes(PREFAB).equals(before)).toBe(true);
    expect(await findInScene("PreviewProbe")).toBe(0);
  });

  it("takes back a failed scene group", async () => {
    await rejected("group", {
      steps: [
        { command: "gameobject_create", args: { scene: SCENE, name: "GroupProbe" } },
        { command: "component_add", args: { scene: SCENE, node: "GroupProbe", component: "NoSuchComponent" } },
      ],
    });
    expect(await findInScene("GroupProbe")).toBe(0);

    await ok("group", { steps: [{ command: "gameobject_create", args: { scene: SCENE, name: "GroupProbe" } }] });
    expect(await findInScene("GroupProbe")).toBe(1);
    await ok("gameobject_delete", { scene: SCENE, node: "GroupProbe" });
  });

  it("rollback=all takes back every kind of step, across targets", async () => {
    const prefabBefore = bytes(PREFAB);
    const otherGuid = await guid(OTHER);
    await ok("asset_create_folder", { path: `${SCRATCH_FOLDER}/Keep` });
    await ok("prefab_create_empty", { path: `${SCRATCH_FOLDER}/Keep/Doomed.prefab`, name: "Doomed" });
    const doomedGuid = await guid(`${SCRATCH_FOLDER}/Keep/Doomed.prefab`);

    const failed = await rejected("group", {
      steps: [
        { command: "gameobject_create", args: { prefab: PREFAB, parent: "Group", name: "Saved" } },
        // A different target: the prefab above is saved here, so taking it back needs its copy.
        { command: "asset_move", args: { path: OTHER, target: `${SCRATCH_FOLDER}/Moved/Deep/Other.prefab` } },
        { command: "asset_delete", args: { path: `${SCRATCH_FOLDER}/Keep/Doomed.prefab` } },
        { command: "gameobject_create", args: { scene: SCENE, name: "AcrossProbe" } },
        FAIL,
      ],
    });
    expect(failed.error_code).toBe("invalid_argument");
    expect(steps(failed.data).slice(0, 4).every((s) => s.ok && s.undone)).toBe(true);

    expect(bytes(PREFAB).equals(prefabBefore)).toBe(true);
    expect(await guid(OTHER)).toBe(otherGuid);
    expect(exists(`${SCRATCH_FOLDER}/Moved`)).toBe(false);
    expect(await guid(`${SCRATCH_FOLDER}/Keep/Doomed.prefab`)).toBe(doomedGuid);
    expect(await findInScene("AcrossProbe")).toBe(0);
  });

  it("rollback=failed with on_error=stop keeps the steps before the failure and runs nothing after", async () => {
    const result = await rejected("group", {
      rollback: "failed",
      on_error: "stop",
      steps: [
        { command: "gameobject_create", args: { scene: SCENE, name: "StopA" } },
        { command: "gameobject_create", args: { scene: SCENE, name: "StopB", components: ["NoSuchComponent"] } },
        { command: "gameobject_create", args: { scene: SCENE, name: "StopC" } },
      ],
    });
    const data = result.data as Record<string, unknown>;
    expect(data.failed_at).toBe(1);
    expect(data.not_run).toBe(1);
    expect(steps(data).map((s) => [s.ok, s.undone])).toEqual([
      [true, false],
      [false, true],
    ]);
    expect(await findInScene("StopA")).toBe(1);
    expect(await findInScene("StopB")).toBe(0);
    expect(await findInScene("StopC")).toBe(0);
    await ok("gameobject_delete", { scene: SCENE, node: "StopA" });
  });

  it("rollback=failed with on_error=continue runs the steps after the failure", async () => {
    const result = await rejected("group", {
      rollback: "failed",
      on_error: "continue",
      steps: [
        { command: "gameobject_create", args: { scene: SCENE, name: "GoA" } },
        { command: "gameobject_create", args: { scene: SCENE, name: "GoB", components: ["NoSuchComponent"] } },
        { command: "gameobject_create", args: { scene: SCENE, name: "GoC" } },
      ],
    });
    const data = result.data as Record<string, unknown>;
    expect(data.failed_at).toBe(1);
    expect(data.not_run).toBe(0);
    expect(steps(data).map((s) => s.ok)).toEqual([true, false, true]);
    expect(await findInScene("GoA")).toBe(1);
    expect(await findInScene("GoB")).toBe(0);
    expect(await findInScene("GoC")).toBe(1);
    await ok("gameobject_delete", { scene: SCENE, node: "GoA" });
    await ok("gameobject_delete", { scene: SCENE, node: "GoC" });
  });

  it("read-only steps run in a group and are not taken back", async () => {
    const failed = await rejected("group", {
      steps: [
        { command: "gameobject_create", args: { scene: SCENE, name: "ReadProbe" } },
        { command: "gameobject_find", args: { scene: SCENE, name: "ReadProbe", exact: true } },
        FAIL,
      ],
    });
    const [create, find] = steps(failed.data);
    expect(create.undone).toBe(true);
    expect(find.ok).toBe(true);
    expect(find.undone).toBe(false);
    expect((find as unknown as { data: { count: number } }).data.count).toBe(1);
    expect(await findInScene("ReadProbe")).toBe(0);
  });

  it("reports rollback_failed when a step cannot be taken back, and still takes back the rest", async () => {
    await ok("prefab_create_empty", { path: `${SCRATCH_FOLDER}/Lost.prefab`, name: "Lost" });
    const failed = await rejected("group", {
      steps: [
        { command: "asset_create_folder", args: { path: `${SCRATCH_FOLDER}/Undone` } },
        { command: "asset_delete", args: { path: `${SCRATCH_FOLDER}/Lost.prefab` } },
        // Removes the copy asset_delete kept, so its Undo has nothing to restore.
        {
          command: "exec",
          args: {
            code:
              'foreach (var d in System.IO.Directory.GetDirectories(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Library/ucp/undo")))' +
              ' foreach (var e in System.IO.Directory.GetFileSystemEntries(d, "deleted-*")) { if (System.IO.Directory.Exists(e)) System.IO.Directory.Delete(e, true); else System.IO.File.Delete(e); }' +
              " return 0;",
          },
        },
        FAIL,
      ],
    });
    expect(failed.error_code).toBe("rollback_failed");
    const rollback = (failed.data as Record<string, unknown>).rollback as {
      ok: boolean;
      errors: Array<{ step: number; command: string }>;
    };
    expect(rollback.ok).toBe(false);
    expect(rollback.errors.map((e) => [e.step, e.command])).toEqual([[1, "asset_delete"]]);
    expect(exists(`${SCRATCH_FOLDER}/Undone`)).toBe(false);
  });

  it("leaves no orphan when a single command fails", async () => {
    const reason = await fails("gameobject_create", { scene: SCENE, name: "TaggedProbe", tag: "NoSuchTag" });
    expect(reason).toContain("NoSuchTag");
    expect(await findInScene("TaggedProbe")).toBe(0);

    await ok("gameobject_create", { scene: SCENE, name: "Holder" });
    const component = await rejected("component_add", {
      scene: SCENE,
      node: "Holder",
      component: "BoxCollider",
      properties: { m_NoSuchField: 1 },
    });
    expect(component.ok).toBe(false);
    const list = await ok("component_list", { scene: SCENE, node: "Holder" });
    expect((list.components as Array<{ type: string }>).map((c) => c.type)).not.toContain("BoxCollider");

    const missing = await rejected("gameobject_rename", { scene: SCENE, node: "NoSuchNode", name: "x" });
    expect(missing.error_code).toBe("node_not_found");
    await ok("gameobject_delete", { scene: SCENE, node: "Holder" });
  });

  it("a read-only command records no undo and saves nothing", async () => {
    const mtime = fs.statSync(projectFile(PREFAB)).mtimeMs;
    const groupBefore = Number(await exec("return UnityEditor.Undo.GetCurrentGroup();"));
    await ok("gameobject_info", { prefab: PREFAB, node: "Group" });
    await ok("component_get", { prefab: PREFAB, node: "Group", component: "Transform" });
    const groupAfter = Number(await exec("return UnityEditor.Undo.GetCurrentGroup();"));
    expect(groupAfter).toBe(groupBefore);
    expect(fs.statSync(projectFile(PREFAB)).mtimeMs).toBe(mtime);
  });

  it("one Ctrl+Z takes back a whole command, and a whole group", async () => {
    await ok("gameobject_create", { scene: SCENE, name: "UndoOne" });
    await exec("UnityEditor.Undo.PerformUndo(); return 0;");
    expect(await findInScene("UndoOne")).toBe(0);

    await ok("group", {
      steps: [
        { command: "gameobject_create", args: { scene: SCENE, name: "UndoA" } },
        { command: "gameobject_create", args: { scene: SCENE, name: "UndoB" } },
        { command: "gameobject_rename", args: { scene: SCENE, node: "UndoB", name: "UndoC" } },
      ],
    });
    expect(await findInScene("UndoA")).toBe(1);
    await exec("UnityEditor.Undo.PerformUndo(); return 0;");
    expect(await findInScene("UndoA")).toBe(0);
    expect(await findInScene("UndoC")).toBe(0);
  });

  it("prefab_edit and scene_edit are gone", async () => {
    // ucp-host turns away a type the session does not advertise before it reaches Unity.
    expect((await rejected("prefab_edit", { prefab: PREFAB, ops: [] })).error_code).toBe("host_unknown_command");
    expect((await rejected("scene_edit", { ops: [] })).error_code).toBe("host_unknown_command");
  });

  it("returns results of the same shape", async () => {
    await expectShapesUnchanged("shapes.group.json");
  });
});
