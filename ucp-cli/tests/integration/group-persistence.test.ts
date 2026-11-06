import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { projectIdFromPath, queueLayout } from "#shared/index.js";
import {
  SCRATCH_FOLDER,
  cleanupScratch,
  ok,
  rejected,
  releaseScratchStage,
  run,
} from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import { ensureEditMode } from "./helpers/play-mode.js";

/**
 * A group writes its progress and journal to Library/ucp/undo/<id>/ before and after every step,
 * so a step that reloads the domain (compile) or enters Play Mode does not lose it. These cases
 * cross a reload and check the group carries on, takes back exactly what ran, and cleans up.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";
const COMPILE_TIMEOUT = 240_000;
const ROOT = `${SCRATCH_FOLDER}/Persist`;
const PREFAB = `${ROOT}/Persist.prefab`;
const SCENE = `${ROOT}/Persist.unity`;
const PROBE = "Assets/UcpPersistProbe";

const FAIL = { command: "asset_info", args: { path: `${ROOT}/DoesNotExist.asset` } };

/** Every context type a command declares, written and read back the way a group journals it. */
const ROUND_TRIP = `using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;
using Newtonsoft.Json;

public static class UcpContextRoundTrip
{
    [UcpFunction("ucp_probe_context_roundtrip", Scope = CommandHostScope.Editor)]
    public static object Run()
    {
        var results = new List<Dictionary<string, object>>();
        var types = CommandRegistry.Entries.Select(e => e.ContextType).Where(t => t != typeof(CommandContext)).Distinct();
        foreach (var type in types)
        {
            var ctx = Activator.CreateInstance(type);
            Fill(ctx, 0);
            var json = JsonConvert.SerializeObject(ctx);
            var back = JsonConvert.DeserializeObject(json, type);
            var ignoredEmpty = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.DeclaringType != typeof(CommandContext) && p.IsDefined(typeof(JsonIgnoreAttribute), true) && !p.PropertyType.IsValueType)
                .All(p => p.GetValue(back) == null);
            results.Add(new Dictionary<string, object>
            {
                ["type"] = type.Name,
                ["same"] = json == JsonConvert.SerializeObject(back),
                ["ignored_empty"] = ignoredEmpty,
                ["json"] = json,
            });
        }

        return new Dictionary<string, object> { ["contexts"] = results };
    }

    static void Fill(object target, int depth)
    {
        foreach (var field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!field.IsDefined(typeof(JsonIgnoreAttribute), true))
                field.SetValue(target, Sample(field.FieldType, field.Name, depth));
        }
    }

    static object Sample(Type type, string name, int depth)
    {
        if (type == typeof(string)) return "s-" + name;
        if (type == typeof(int)) return 7;
        if (type == typeof(bool)) return true;
        if (type == typeof(List<string>)) return new List<string> { "a", "b" };
        if (depth < 3 && type.IsClass && type.GetConstructor(Type.EmptyTypes) != null)
        {
            var value = Activator.CreateInstance(type);
            Fill(value, depth + 1);
            return value;
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
`;

function projectFile(assetPath: string): string {
  return path.join(gameDemoProjectPath(), ...assetPath.split("/"));
}

function exists(assetPath: string): boolean {
  return fs.existsSync(projectFile(assetPath));
}

function bytes(assetPath: string): Buffer {
  return fs.readFileSync(projectFile(assetPath));
}

const UNDO_ROOT = () => path.join(gameDemoProjectPath(), "Library", "ucp", "undo");

function undoFolders(): string[] {
  return fs.existsSync(UNDO_ROOT()) ? fs.readdirSync(UNDO_ROOT()) : [];
}

function queueFiles(): string[] {
  const layout = queueLayout(projectIdFromPath(gameDemoProjectPath()), process.env.UCP_ROOT);
  return [layout.inbox, layout.outbox].flatMap((dir) => (fs.existsSync(dir) ? fs.readdirSync(dir) : []));
}

async function findInScene(name: string): Promise<number> {
  return Number((await ok("gameobject_find", { scene: SCENE, name, exact: true })).count);
}

async function guid(assetPath: string): Promise<string> {
  return String((await ok("asset_guid", { path: assetPath })).guid);
}

async function waitUntilReady(): Promise<void> {
  // ping waits behind the lane, and answers once the Editor is ready again after a reload.
  await ok("ping", {}, COMPILE_TIMEOUT);
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

type Step = { index: number; command: string; ok: boolean; undone: boolean };

function steps(data: unknown): Step[] {
  return ((data as Record<string, unknown>).steps as Step[]) ?? [];
}

describe.skipIf(!runEditorTests)("group across domain reloads (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
    await cleanupScratch();
    fs.mkdirSync(projectFile(`${PROBE}/Editor`), { recursive: true });
    fs.writeFileSync(projectFile(`${PROBE}/Editor/UcpContextRoundTrip.cs`), ROUND_TRIP);
    await ok("compile", {}, COMPILE_TIMEOUT);
    await ok("asset_create_folder", { path: ROOT });
    await ok("prefab_create_empty", { path: PREFAB, name: "Persist" });
    await ok("scene_new", { setup: "empty", mode: "additive", path: SCENE });
  }, COMPILE_TIMEOUT + 60_000);

  afterAll(async () => {
    await ensureEditMode();
    await releaseScratchStage();
    await run("scene_close", { scene: SCENE, force: true });
    await cleanupScratch();
    await run("asset_delete", { path: PROBE });
    await run("compile", {}, COMPILE_TIMEOUT);
  }, COMPILE_TIMEOUT + 60_000);

  it("carries on after compile reloads the domain, and takes back what ran before it", async () => {
    const prefabBefore = bytes(PREFAB);
    await ok("prefab_create_empty", { path: `${ROOT}/Mover.prefab`, name: "Mover" });
    const moverGuid = await guid(`${ROOT}/Mover.prefab`);

    const failed = await rejected(
      "group",
      {
        steps: [
          { command: "asset_move", args: { path: `${ROOT}/Mover.prefab`, target: `${ROOT}/Moved/Mover.prefab` } },
          { command: "gameobject_create", args: { prefab: PREFAB, parent: "Persist", name: "BeforeReload" } },
          { command: "compile", args: {} },
          FAIL,
        ],
      },
      COMPILE_TIMEOUT,
    );
    expect(failed.error_code).toBe("invalid_argument");
    const reported = steps(failed.data);
    expect(reported.map((s) => [s.index, s.command, s.ok])).toEqual([
      [0, "asset_move", true],
      [1, "gameobject_create", true],
      [2, "compile", true],
      [3, "asset_info", false],
    ]);
    expect(reported[0].undone && reported[1].undone).toBe(true);

    expect(await guid(`${ROOT}/Mover.prefab`)).toBe(moverGuid);
    expect(exists(`${ROOT}/Moved`)).toBe(false);
    expect(bytes(PREFAB).equals(prefabBefore)).toBe(true);
    expect(undoFolders()).toEqual([]);
    expect(queueFiles()).toEqual([]);
  }, COMPILE_TIMEOUT * 2);

  it("runs every step exactly once across a reload, keeps them, and one Ctrl+Z takes back the scene edits", async () => {
    const data = await ok(
      "group",
      {
        steps: [
          { command: "gameobject_create", args: { scene: SCENE, name: "PersistA" } },
          { command: "asset_create_folder", args: { path: `${ROOT}/KeptA` } },
          { command: "compile", args: {} },
          { command: "gameobject_create", args: { scene: SCENE, name: "PersistB" } },
          { command: "asset_create_folder", args: { path: `${ROOT}/KeptB` } },
        ],
      },
      COMPILE_TIMEOUT,
    );
    expect(steps(data).map((s) => [s.index, s.ok])).toEqual([
      [0, true],
      [1, true],
      [2, true],
      [3, true],
      [4, true],
    ]);
    expect(exists(`${ROOT}/KeptA`) && exists(`${ROOT}/KeptB`)).toBe(true);
    expect(await findInScene("PersistA")).toBe(1);
    expect(await findInScene("PersistB")).toBe(1);
    expect(undoFolders()).toEqual([]);

    await ok("exec", { code: "UnityEditor.Undo.PerformUndo(); return 0;" });
    expect(await findInScene("PersistA")).toBe(0);
    expect(await findInScene("PersistB")).toBe(0);
  }, COMPILE_TIMEOUT * 2);

  it("takes back a scene edit made before Play Mode", async () => {
    const failed = await rejected(
      "group",
      {
        steps: [
          { command: "gameobject_create", args: { scene: SCENE, name: "BeforePlay" } },
          { command: "play", args: {} },
          { command: "stop", args: {} },
          FAIL,
        ],
      },
      COMPILE_TIMEOUT,
    );
    await ensureEditMode();
    // Unity keeps the undo group through entering and leaving Play Mode, so the node is gone.
    expect(failed.error_code).toBe("invalid_argument");
    const [create, play, stop] = steps(failed.data);
    expect([create.undone, play.ok, stop.ok]).toEqual([true, true, true]);
    expect((failed.data as { rollback: { errors: unknown[] } }).rollback.errors).toEqual([]);
    expect(await findInScene("BeforePlay")).toBe(0);
    expect(undoFolders()).toEqual([]);
  }, COMPILE_TIMEOUT * 2);

  it("takes back what ran when the deadline passes in the middle of a synchronous group", async () => {
    const result = await run(
      "group",
      {
        steps: [
          { command: "asset_create_folder", args: { path: `${ROOT}/LateSync` } },
          { command: "exec", args: { code: "System.Threading.Thread.Sleep(8000); return 0;" } },
          { command: "asset_info", args: { path: ROOT } },
        ],
      },
      4_000,
    );
    expect(result.ok).toBe(false);
    expect(result.error_code).toBe("host_timeout");
    await sleep(6_000);
    await waitUntilReady();
    expect(exists(`${ROOT}/LateSync`)).toBe(false);
    expect(undoFolders()).toEqual([]);
  }, 60_000);

  it("takes back what ran when the deadline passes while a deferred step runs", async () => {
    const result = await run(
      "group",
      {
        steps: [
          { command: "asset_create_folder", args: { path: `${ROOT}/LateDeferred` } },
          { command: "compile", args: {} },
        ],
      },
      3_000,
    );
    expect(result.ok).toBe(false);
    expect(result.error_code).toBe("host_timeout");
    await waitUntilReady();
    // The job is dropped on the first tick after its deadline; give it a few.
    for (let i = 0; i < 20 && (exists(`${ROOT}/LateDeferred`) || undoFolders().length > 0); i++) await sleep(500);
    expect(exists(`${ROOT}/LateDeferred`)).toBe(false);
    expect(undoFolders()).toEqual([]);
  }, COMPILE_TIMEOUT);

  it("reports a group folder Unity left behind, and does not touch it", async () => {
    const fake = path.join(UNDO_ROOT(), "ucp-probe-crashed");
    fs.mkdirSync(fake, { recursive: true });
    fs.writeFileSync(path.join(fake, "progress.json"), JSON.stringify({ id: "ucp-probe-crashed", steps: [], next: 0 }));
    try {
      await ok("console", { clear: true });
      await ok("compile", {}, COMPILE_TIMEOUT);
      const log = await ok("console", { type: "warning", lines: 200, stacktrace: "none" });
      const messages = (log.entries as Array<{ message: string }>).map((e) => e.message);
      expect(messages.some((m) => m.includes("group 'ucp-probe-crashed' did not finish"))).toBe(true);
      expect(fs.existsSync(path.join(fake, "progress.json"))).toBe(true);
    } finally {
      fs.rmSync(fake, { recursive: true, force: true });
    }
  }, COMPILE_TIMEOUT + 30_000);

  it("every command context survives being written to the journal and read back", async () => {
    const data = await ok("call", { name: "ucp_probe_context_roundtrip" });
    const contexts = data.contexts as Array<{ type: string; same: boolean; ignored_empty: boolean; json: string }>;
    expect(contexts.map((c) => c.type)).toEqual(
      expect.arrayContaining(["EditTargetContext", "PrefabCreateContext", "AssetFileContext"]),
    );
    for (const context of contexts) {
      expect(context.same, `${context.type}: ${context.json}`).toBe(true);
      expect(context.ignored_empty, context.type).toBe(true);
      expect(context.json, context.type).not.toContain("CommandId");
    }
  });
});
