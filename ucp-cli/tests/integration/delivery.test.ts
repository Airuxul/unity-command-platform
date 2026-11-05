import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { projectIdFromPath, queueLayout, resolveUcpRoot } from "#shared/index.js";
import { fails, ok, run } from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import { ensureEditMode } from "./helpers/play-mode.js";

/**
 * How results get back to the caller: each command runs exactly once, also when it triggers a
 * domain reload, and nothing is left behind in ~/.ucp afterwards. Before this, a deferred
 * command's inbox file outlived the reload and ran a second time, and every command left an
 * inbox file, an outbox file and a job record forever.
 *
 * Also pins the content-layer fixes that need a script of our own (a string[] component) or a
 * non-prefab asset to exercise.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

const PROBE_FOLDER = "Assets/UcpDeliveryProbe";
const PROBE_PREFAB = `${PROBE_FOLDER}/Probe.prefab`;
const COMPILES_KEY = "ucp.test.compilations";
const COMPILE_TIMEOUT = 180_000;

const PROBE_COMPONENT = `using UnityEngine;

public sealed class UcpProbeArray : MonoBehaviour
{
    public string[] names;
}
`;

// Counts compilations in SessionState, which survives the reload each compilation causes.
const COMPILE_COUNTER = `using UnityEditor;
using UnityEditor.Compilation;

[InitializeOnLoad]
static class UcpCompileCounter
{
    static UcpCompileCounter()
    {
        CompilationPipeline.compilationStarted += _ =>
            SessionState.SetInt("${COMPILES_KEY}", SessionState.GetInt("${COMPILES_KEY}", 0) + 1);
    }
}
`;

function projectFile(assetPath: string): string {
  return path.join(gameDemoProjectPath(), ...assetPath.split("/"));
}

function queueFiles(): string[] {
  const projectId = projectIdFromPath(gameDemoProjectPath());
  const layout = queueLayout(projectId, process.env.UCP_ROOT);
  const jobs = path.join(resolveUcpRoot(process.env.UCP_ROOT), "jobs", projectId);
  return [layout.inbox, layout.outbox, jobs].flatMap((dir) =>
    fs.existsSync(dir) ? fs.readdirSync(dir).map((f) => `${path.basename(dir)}/${f}`) : [],
  );
}

async function compilations(): Promise<number> {
  const data = await ok("exec", {
    code: `return UnityEditor.SessionState.GetInt("${COMPILES_KEY}", 0);`,
  });
  return Number(data.result);
}

describe.skipIf(!runEditorTests)("command delivery (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();

    fs.mkdirSync(projectFile(`${PROBE_FOLDER}/Editor`), { recursive: true });
    fs.writeFileSync(projectFile(`${PROBE_FOLDER}/UcpProbeArray.cs`), PROBE_COMPONENT);
    fs.writeFileSync(projectFile(`${PROBE_FOLDER}/Editor/UcpCompileCounter.cs`), COMPILE_COUNTER);
    // The counter only counts from the compile after the one that brings it in.
    await ok("refresh", { compile: true }, COMPILE_TIMEOUT);
  }, COMPILE_TIMEOUT * 2);

  afterAll(async () => {
    await run("asset_delete", { path: PROBE_FOLDER });
    await run("refresh", { compile: true }, COMPILE_TIMEOUT);
  }, COMPILE_TIMEOUT);

  it("leaves no inbox, outbox or job file behind", async () => {
    await ok("ping");
    await ok("echo", { message: "delivery" });
    await fails("gameobject_info", { node: "Ucp/Does/Not/Exist" });

    expect(queueFiles()).toEqual([]);
  });

  it(
    "compile runs once across the domain reload it causes",
    async () => {
      const before = await compilations();
      await ok("compile", {}, COMPILE_TIMEOUT);

      expect((await compilations()) - before).toBe(1);
      expect(queueFiles()).toEqual([]);
    },
    COMPILE_TIMEOUT,
  );

  it(
    "compile imports a script written since the last refresh",
    async () => {
      // Unity imports changed files only on focus, so in the background a bare compile used to
      // rebuild the old scripts and still report success.
      fs.writeFileSync(
        projectFile(`${PROBE_FOLDER}/UcpProbeFresh.cs`),
        "public static class UcpProbeFresh { public static int V => 7; }\n",
      );
      await ok("compile", {}, COMPILE_TIMEOUT);

      const data = await ok("exec", { code: "return UcpProbeFresh.V;" });
      expect(Number(data.result)).toBe(7);
    },
    COMPILE_TIMEOUT,
  );

  it(
    "refresh compile=true runs once and completes as a compile",
    async () => {
      const before = await compilations();
      const result = await ok("refresh", { compile: true }, COMPILE_TIMEOUT);

      expect(result.compiled).toBe(true);
      expect((await compilations()) - before).toBe(1);
      expect(queueFiles()).toEqual([]);
    },
    COMPILE_TIMEOUT,
  );

  it("play and stop deliver once and clean up", async () => {
    expect((await ok("play", {}, 60_000)).is_playing).toBe(true);
    expect((await ok("stop", {}, 60_000)).is_playing).toBe(false);
    expect(queueFiles()).toEqual([]);
  }, 150_000);

  it("refuses to write back a truncated array read", async () => {
    await ok("prefab_create_empty", { path: PROBE_PREFAB, name: "Probe" });
    const names = Array.from({ length: 70 }, (_, i) => `n${i}`);
    await ok("component_add", {
      prefab: PROBE_PREFAB,
      node: "Probe",
      component: "UcpProbeArray",
      properties: { names },
    });

    const read = await ok("component_get", {
      prefab: PROBE_PREFAB,
      node: "Probe",
      component: "UcpProbeArray",
      property: "names",
    });
    const truncated = (read.properties as Record<string, unknown>).names as unknown[];
    expect(truncated).toHaveLength(65);
    expect(truncated[64]).toMatchObject({ __truncated: true });

    expect(
      await fails("component_set", {
        prefab: PROBE_PREFAB,
        node: "Probe",
        component: "UcpProbeArray",
        property: "names",
        value: truncated,
      }),
    ).toContain("truncated read");

    // The failed write kept nothing: all 70 are still there.
    const full = await ok("component_get", {
      prefab: PROBE_PREFAB,
      node: "Probe",
      component: "UcpProbeArray",
      property: "names",
      max_array: 100,
    });
    expect((full.properties as Record<string, unknown>).names).toEqual(names);
  });

  it("only copies or moves real prefabs", async () => {
    const script = `${PROBE_FOLDER}/UcpProbeArray.cs`;
    expect(
      await fails("prefab_duplicate", { path: script, target: `${PROBE_FOLDER}/Bad.prefab` }),
    ).toContain(".prefab");
    expect(
      await fails("prefab_move", { path: script, target: `${PROBE_FOLDER}/Bad.prefab` }),
    ).toContain(".prefab");
  });

  it("rejects unknown scene_new values instead of falling back to single", async () => {
    // The binder checks AllowedValues, so the typo never reaches the command.
    expect(await fails("scene_new", { mode: "additve" })).toContain(
      "Invalid value 'additve' for '--mode'",
    );
    expect(await fails("scene_new", { setup: "emtpy", mode: "additive" })).toContain(
      "Invalid value 'emtpy' for '--setup'",
    );
    // Comma-separated values are checked item by item.
    expect(await fails("console", { type: "error,warnings" })).toContain(
      "Invalid value 'warnings' for '--type'",
    );
  });

  it("console stacktrace=all returns the whole trace", async () => {
    await ok("exec", { code: 'UnityEngine.Debug.LogWarning("ucp-stack-probe"); return 0;' });
    const find = async (stacktrace: string) => {
      const data = await ok("console", { type: "warning", lines: 200, stacktrace });
      const entries = data.entries as Array<{ message: string }>;
      return entries.find((e) => e.message.startsWith("ucp-stack-probe"))?.message;
    };

    expect(await find("none")).toBe("ucp-stack-probe");
    expect((await find("all"))?.split("\n").length).toBeGreaterThan(1);
  });
});
