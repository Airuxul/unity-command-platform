import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  projectIdFromPath,
  queueLayout,
  readJsonFile,
  resolveUcpRoot,
  sessionFilePath,
} from "#shared/index.js";
import { fails, ok, rejected, run } from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import { ensureEditMode } from "./helpers/play-mode.js";
import { isMinimized, minimize, restore } from "./helpers/unity-window.js";

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

// A deferred command with no completion policy ("started"): only its own callbacks could finish
// it, and the reload it asks for takes them away.
const STARTED_COMMAND = `using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;

public sealed class UcpProbeStartedCommand : CliCommand
{
    public override InvokeDescriptor Descriptor { get; } = new DeferredInvokeDescriptor(
        "ucp_probe_started", CommandHostScope.Editor, "test probe: defers, then reloads the domain");

    public override void Run() => UnityEditor.EditorUtility.RequestScriptReload();
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
  let editorPid = 0;
  let wasMinimized = true;

  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
    // The background is the harder case (a tick about every 100 ms, no import on its own), so
    // delivery is always checked there; the window is put back afterwards.
    if (process.platform === "win32") {
      editorPid = Number(
        (
          readJsonFile(
            sessionFilePath(projectIdFromPath(gameDemoProjectPath()), process.env.UCP_ROOT),
          ) as { pid?: number } | null
        )?.pid ?? 0,
      );
      if (editorPid > 0) {
        wasMinimized = isMinimized(editorPid);
        minimize(editorPid);
      }
    }

    fs.mkdirSync(projectFile(`${PROBE_FOLDER}/Editor`), { recursive: true });
    fs.writeFileSync(projectFile(`${PROBE_FOLDER}/UcpProbeArray.cs`), PROBE_COMPONENT);
    fs.writeFileSync(projectFile(`${PROBE_FOLDER}/Editor/UcpCompileCounter.cs`), COMPILE_COUNTER);
    fs.writeFileSync(
      projectFile(`${PROBE_FOLDER}/Editor/UcpProbeStartedCommand.cs`),
      STARTED_COMMAND,
    );
    // The counter only counts from the compile after the one that brings it in.
    await ok("refresh", { compile: true }, COMPILE_TIMEOUT);
  }, COMPILE_TIMEOUT * 2);

  afterAll(async () => {
    await run("asset_delete", { path: PROBE_FOLDER });
    await run("refresh", { compile: true }, COMPILE_TIMEOUT);
    if (editorPid > 0 && !wasMinimized) restore(editorPid);
  }, COMPILE_TIMEOUT);

  it(
    "answers state while a compile runs, and holds it only for the domain reload",
    async () => {
      fs.writeFileSync(
        projectFile(`${PROBE_FOLDER}/UcpProbeLatency.cs`),
        `public static class UcpProbeLatency { public const long V = ${Date.now()}; }\n`,
      );
      let compileDone = false;
      const compiling = ok("compile", {}, COMPILE_TIMEOUT).finally(() => {
        compileDone = true;
      });
      const answers: Array<{ ms: number; ok: boolean; compiling: unknown }> = [];
      while (!compileDone) {
        const started = Date.now();
        const state = await run("state", {}, COMPILE_TIMEOUT);
        const data = state.data as Record<string, unknown> | null;
        answers.push({ ms: Date.now() - started, ok: state.ok, compiling: data?.is_compiling });
      }
      await compiling;

      const summary = JSON.stringify(answers);
      expect(
        answers.every((a) => a.ok),
        summary,
      ).toBe(true);
      // Answered while the compile it did not wait for was still compiling.
      expect(
        answers.some((a) => a.compiling === true && a.ms < 1_000),
        summary,
      ).toBe(true);
      // No answer came during the reload itself: the slowest one spans it.
      expect(Math.max(...answers.map((a) => a.ms)), summary).toBeGreaterThan(1_000);
      expect(queueFiles()).toEqual([]);
    },
    COMPILE_TIMEOUT,
  );

  it(
    "finishes a 0.3 compile after the upgrade reload and drops what 0.3 had delivered",
    async () => {
      // What SessionState holds when the 0.5 agent first loads: 0.3 kept every job forever,
      // with no deadline, and was in the middle of the compile that loads the new code.
      const now = Date.now();
      const record = (id: string, command: string, status: number, kind: string) => ({
        Id: `job-${id}`,
        Command: command,
        RequestId: `cmd-0-v03-${id}`,
        Status: status,
        CompletionKind: kind,
        ResultJson: "",
        Error: "",
        CreatedAtUtcMs: now,
        UpdatedAtUtcMs: now,
      });
      const jobs = JSON.stringify({
        Items: [
          record("ping", "ping", 2, "started"),
          record("play", "play", 2, "enter_play"),
          record("compile", "compile", 1, "compilation"),
        ],
      });
      await ok("exec", {
        code: `UnityEditor.SessionState.SetString("Air.UcpAgent.Jobs", @"${jobs.replace(/"/g, '""')}");
UnityEditor.EditorUtility.RequestScriptReload();
return 0;`,
      });

      const { outbox } = queueLayout(
        projectIdFromPath(gameDemoProjectPath()),
        process.env.UCP_ROOT,
      );
      const compileResult = path.join(outbox, "cmd-0-v03-compile.json");
      const deadline = Date.now() + COMPILE_TIMEOUT;
      while (!fs.existsSync(compileResult) && Date.now() < deadline) {
        await new Promise((r) => setTimeout(r, 200));
      }
      await ok("ping", {}, COMPILE_TIMEOUT);

      const leftovers = queueFiles();
      const delivered = JSON.parse(fs.readFileSync(compileResult, "utf8"));
      fs.rmSync(compileResult);
      expect(delivered).toMatchObject({ success: true, data: { compiled: true } });
      // The jobs 0.3 had already answered stay unanswered: nobody waits for them.
      expect(leftovers).toEqual(["outbox/cmd-0-v03-compile.json"]);
      expect(queueFiles()).toEqual([]);
    },
    COMPILE_TIMEOUT * 2,
  );

  it(
    "fails a job its domain reload cut off as orphaned, at once",
    async () => {
      const started = Date.now();
      const result = await rejected("ucp_probe_started", {}, 120_000);
      expect(result.error_code).toBe("orphaned");
      // Not the 120 s timeout: the job ends with the reload.
      expect(Date.now() - started).toBeLessThan(60_000);
      await ok("ping", {}, COMPILE_TIMEOUT);
      expect(queueFiles()).toEqual([]);
    },
    COMPILE_TIMEOUT,
  );

  it("still runs a command from a host that sends no deadline", async () => {
    const projectId = projectIdFromPath(gameDemoProjectPath());
    const { inbox, outbox } = queueLayout(projectId, process.env.UCP_ROOT);
    const key = "ucp.test.nodeadline";
    await ok("exec", { code: `UnityEditor.SessionState.EraseInt("${key}"); return 0;` });
    // What a 0.4 host writes: no deadline field.
    fs.writeFileSync(
      path.join(inbox, "cmd-0-nodeadline-probe.json"),
      JSON.stringify({
        id: "cmd-0-nodeadline-probe",
        type: "exec",
        timeout: 30_000,
        args: { code: `UnityEditor.SessionState.SetInt("${key}", 1); return 1;` },
      }),
    );

    const after = await ok("exec", {
      code: `return UnityEditor.SessionState.GetInt("${key}", 0);`,
    });
    expect(Number(after.result)).toBe(1);
    const answer = path.join(outbox, "cmd-0-nodeadline-probe.json");
    expect(fs.existsSync(answer)).toBe(true);
    fs.rmSync(answer);
    expect(queueFiles()).toEqual([]);
  });

  it("a command that outruns its timeout leaves no result behind", async () => {
    // exec blocks Unity's main thread for 4 s; the caller gives up after 2.
    const timedOut = await rejected(
      "exec",
      { code: "System.Threading.Thread.Sleep(4000); return 1;" },
      2_000,
    );
    expect(timedOut.error_code).toBe("timeout");

    // Answered only once Unity is free again, so the exec has finished by then.
    await ok("ping", {}, 30_000);
    expect(queueFiles()).toEqual([]);
  });

  it("does not run a command whose deadline passed before Unity saw it", async () => {
    // What a host that died, or a caller that gave up, leaves in the inbox.
    const projectId = projectIdFromPath(gameDemoProjectPath());
    const { inbox } = queueLayout(projectId, process.env.UCP_ROOT);
    const key = "ucp.test.expired";
    await ok("exec", { code: `UnityEditor.SessionState.EraseInt("${key}"); return 0;` });
    fs.writeFileSync(
      path.join(inbox, "cmd-0-expired-probe.json"),
      JSON.stringify({
        id: "cmd-0-expired-probe",
        type: "exec",
        timeout: 30_000,
        deadline: Date.now() - 1_000,
        args: { code: `UnityEditor.SessionState.SetInt("${key}", 1); return 1;` },
      }),
    );

    // The agent takes inbox files in name order, so the probe ("cmd-0-…") goes before this.
    const after = await ok("exec", {
      code: `return UnityEditor.SessionState.GetInt("${key}", 0);`,
    });
    expect(Number(after.result)).toBe(0);
    expect(queueFiles()).toEqual([]);
  });

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

  it(
    "compile fails with compile_error and the compiler's errors",
    async () => {
      const broken = `${PROBE_FOLDER}/UcpProbeBroken.cs`;
      fs.writeFileSync(projectFile(broken), "public static class UcpProbeBroken { int x = ; }\n");
      try {
        const failed = await rejected("compile", {}, COMPILE_TIMEOUT);
        expect(failed.error_code).toBe("compile_error");
        const errors = (failed.data as { errors: Array<{ file: string; line: number }> }).errors;
        expect(errors[0].file).toContain("UcpProbeBroken.cs");
        expect(errors[0].line).toBe(1);

        // Nothing changed, so Unity may skip compiling; the project is still broken.
        expect((await rejected("compile", {}, COMPILE_TIMEOUT)).error_code).toBe("compile_error");
      } finally {
        fs.rmSync(projectFile(broken), { force: true });
        fs.rmSync(projectFile(`${broken}.meta`), { force: true });
      }

      expect((await ok("compile", {}, COMPILE_TIMEOUT)).compiled).toBe(true);
      expect(queueFiles()).toEqual([]);
    },
    COMPILE_TIMEOUT * 3,
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
