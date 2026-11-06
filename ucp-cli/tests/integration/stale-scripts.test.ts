import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { SCRATCH_FOLDER, cleanupScratch, ok, run } from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import { sessionFile } from "./helpers/editor-latency.js";
import { ensureEditMode } from "./helpers/play-mode.js";
import { isMinimized, minimize, restore } from "./helpers/unity-window.js";

/**
 * Unity in the background does not import a changed script on its own. Compiling is its own
 * command: edit the file, run compile, then run the command that needs the new code.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1" && process.platform === "win32";

const FOLDER = "Assets/UcpStaleProbe";
const COMPILE_TIMEOUT = 180_000;

function projectFile(assetPath: string): string {
  return path.join(gameDemoProjectPath(), ...assetPath.split("/"));
}

/** The probe value as the script on disk says; the loaded code may still say an older one. */
function writeProbe(value: number): void {
  fs.writeFileSync(
    projectFile(`${FOLDER}/Runtime/UcpStaleProbe.cs`),
    `public static class UcpStaleProbe { public static int V => ${value}; }\n`,
  );
}

/** A test that passes only against the version of the script it was written with. */
function writeProbeTest(value: number): void {
  fs.writeFileSync(
    projectFile(`${FOLDER}/Tests/UcpStaleProbeTests.cs`),
    `using NUnit.Framework;

public class UcpStaleProbeTests
{
    [Test]
    public void SeesTheScriptOnDisk() => Assert.AreEqual(${value}, UcpStaleProbe.V, "ran the old script");
}
`,
  );
}

let next = 100;
function freshValue(): number {
  next += 1;
  return next;
}

describe.skipIf(!runEditorTests)(
  "a script edit is invisible until compile (Unity required)",
  () => {
    let pid = 0;
    let wasMinimized = true;

    beforeAll(async () => {
      if (!(await hasReadyEditorSession())) {
        throw new Error(
          "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
        );
      }
      await ensureEditMode();
      pid = sessionFile().pid ?? 0;
      if (pid > 0) {
        wasMinimized = isMinimized(pid);
        minimize(pid);
      }

      fs.mkdirSync(projectFile(`${FOLDER}/Runtime`), { recursive: true });
      fs.mkdirSync(projectFile(`${FOLDER}/Tests`), { recursive: true });
      fs.writeFileSync(
        projectFile(`${FOLDER}/Runtime/UcpStaleProbe.Runtime.asmdef`),
        JSON.stringify({ name: "UcpStaleProbe.Runtime" }),
      );
      fs.writeFileSync(
        projectFile(`${FOLDER}/Tests/UcpStaleProbe.Tests.asmdef`),
        JSON.stringify({
          name: "UcpStaleProbe.Tests",
          references: ["UcpStaleProbe.Runtime", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
          includePlatforms: ["Editor"],
          overrideReferences: true,
          precompiledReferences: ["nunit.framework.dll"],
          autoReferenced: false,
          defineConstraints: ["UNITY_INCLUDE_TESTS"],
        }),
      );
      fs.mkdirSync(projectFile(`${FOLDER}/Editor`), { recursive: true });
      // A project's own command, declared the way a built-in one is.
      fs.writeFileSync(
        projectFile(`${FOLDER}/Editor/UcpStaleProbeCommand.cs`),
        `using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;

[UcpCommand]
public static class UcpStaleProbeCommand
{
    public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
        "ucp_probe_compiled", CommandHostScope.Editor, "test probe: returns UcpStaleProbe.V");

    public static object Execute(CommandContext ctx) =>
        new System.Collections.Generic.Dictionary<string, object> { ["value"] = UcpStaleProbe.V };
}
`,
      );
      writeProbe(1);
      writeProbeTest(1);
      await ok("compile", {}, COMPILE_TIMEOUT);
    }, COMPILE_TIMEOUT + 30_000);

    afterAll(async () => {
      await ensureEditMode();
      await run("asset_delete", { path: FOLDER });
      await run("compile", {}, COMPILE_TIMEOUT);
      if (pid > 0 && !wasMinimized) restore(pid);
    }, COMPILE_TIMEOUT + 60_000);

    it(
      "exec keeps the last compiled script until compile is run",
      async () => {
        const value = freshValue();
        writeProbe(value);
        const stale = await run("exec", { code: "return UcpStaleProbe.V;" }, COMPILE_TIMEOUT);
        expect(stale.ok, JSON.stringify(stale)).toBe(true);
        expect(Number((stale.data as { result: unknown }).result)).toBe(1);

        await ok("compile", {}, COMPILE_TIMEOUT);
        const fresh = await run("exec", { code: "return UcpStaleProbe.V;" }, COMPILE_TIMEOUT);
        expect(fresh.ok, JSON.stringify(fresh)).toBe(true);
        expect(Number((fresh.data as { result: unknown }).result)).toBe(value);
      },
      COMPILE_TIMEOUT * 2,
    );

    it(
      "play sees the script compile loaded",
      async () => {
        const value = freshValue();
        writeProbe(value);
        await ok("compile", {}, COMPILE_TIMEOUT);
        try {
          const played = await run("play", {}, COMPILE_TIMEOUT);
          expect(played.ok, JSON.stringify(played)).toBe(true);
          const result = await run("exec", { code: "return UcpStaleProbe.V;" }, COMPILE_TIMEOUT);
          expect(Number((result.data as { result: unknown }).result)).toBe(value);
        } finally {
          await ensureEditMode();
        }
      },
      COMPILE_TIMEOUT * 2,
    );

    it(
      "compile reports a script edit that does not compile",
      async () => {
        fs.writeFileSync(
          projectFile(`${FOLDER}/Runtime/UcpStaleProbe.cs`),
          "public static class UcpStaleProbe { public static int V => ; }\n",
        );
        try {
          const result = await run("compile", {}, COMPILE_TIMEOUT);
          expect(result.error_code, JSON.stringify(result)).toBe("compile_error");
        } finally {
          writeProbe(freshValue());
          await ok("compile", {}, COMPILE_TIMEOUT);
        }
      },
      COMPILE_TIMEOUT * 2,
    );

    it(
      "component_add of a class written since the last compile says to compile first",
      async () => {
        fs.writeFileSync(
          projectFile(`${FOLDER}/Runtime/UcpStaleComponent.cs`),
          "public class UcpStaleComponent : UnityEngine.MonoBehaviour { }\n",
        );
        const scene = `${SCRATCH_FOLDER}/Stale.unity`;
        await ok("asset_create_folder", { path: SCRATCH_FOLDER });
        await ok("scene_new", { setup: "empty", mode: "additive" });
        await ok("scene_save_as", { path: scene });
        try {
          await ok("gameobject_create", { scene, name: "Host" });
          const result = await run(
            "component_add",
            {
              scene,
              node: "Host",
              component: "UcpStaleComponent",
            },
            COMPILE_TIMEOUT,
          );
          process.stdout.write(
            `MEASURED component_add of an uncompiled class: ${JSON.stringify({ ok: result.ok, error: result.error_code, message: result.message })}\n`,
          );
          // Adding a component changes no code, so it does not compile on its own.
          expect(result.error_code, JSON.stringify(result)).toBe("type_not_found");
          expect(result.hint).toContain("compile first");
        } finally {
          await run("scene_close", { scene, force: true });
          await cleanupScratch();
        }
      },
      COMPILE_TIMEOUT * 2,
    );

    it(
      "a project command sees the script compile loaded",
      async () => {
        const value = freshValue();
        writeProbe(value);
        await ok("compile", {}, COMPILE_TIMEOUT);
        const result = await run("ucp_probe_compiled", {}, COMPILE_TIMEOUT);
        expect(result.ok, JSON.stringify(result)).toBe(true);
        expect(Number((result.data as { value: unknown }).value)).toBe(value);
      },
      COMPILE_TIMEOUT,
    );

    it(
      "test_run runs the tests compile loaded",
      async () => {
        const value = freshValue();
        writeProbe(value);
        writeProbeTest(value);
        await ok("compile", {}, COMPILE_TIMEOUT);
        const result = await run(
          "test_run",
          { tests: ["UcpStaleProbeTests.SeesTheScriptOnDisk"] },
          COMPILE_TIMEOUT,
        );
        expect(result.ok, JSON.stringify(result)).toBe(true);
      },
      COMPILE_TIMEOUT,
    );
  },
);
