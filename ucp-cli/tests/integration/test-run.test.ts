import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { projectIdFromPath, queueLayout, resolveUcpRoot } from "#shared/index.js";
import {
  SCRATCH_FOLDER,
  cleanupScratch,
  expectShapesUnchanged,
  ok,
  rejected,
  run,
} from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import { ensureEditMode } from "./helpers/play-mode.js";

/**
 * EditMode test_run against a throwaway test assembly: the summary, failures, filters, the guards
 * that keep the Test Runner from stopping on a dialog, and the busy guard while a run outlives
 * its caller.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

const PROBE_FOLDER = "Assets/UcpTestRunProbe";
const PROBE_ASSEMBLY = "UcpProbe.EditTests";
const COMPILE_TIMEOUT = 180_000;
const SLOW_TEST_SECONDS = 4;

const PROBE_ASMDEF = {
  name: PROBE_ASSEMBLY,
  references: ["UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  includePlatforms: ["Editor"],
  overrideReferences: true,
  precompiledReferences: ["nunit.framework.dll"],
  autoReferenced: false,
  defineConstraints: ["UNITY_INCLUDE_TESTS"],
};

const PROBE_TESTS = `using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace UcpProbe
{
    public class ProbeTests
    {
        [Test]
        public void Passes() => Assert.AreEqual(2, 1 + 1);

        [Test]
        public void Fails() => Assert.AreEqual("expected", "actual", "ucp probe failure");

        [Test, Ignore("ucp probe skip")]
        public void Skipped() { }

        [UnityTest, Category("Slow")]
        public IEnumerator Slow()
        {
            var end = EditorApplication.timeSinceStartup + ${SLOW_TEST_SECONDS};
            while (EditorApplication.timeSinceStartup < end)
                yield return null;
        }
    }
}
`;

const PLAY_ASSEMBLY = "UcpProbe.PlayTests";

// Not Editor-only: PlayMode tests run in the player assembly set.
const PLAY_ASMDEF = {
  ...PROBE_ASMDEF,
  name: PLAY_ASSEMBLY,
  references: ["UnityEngine.TestRunner"],
  includePlatforms: [],
};

const PLAY_TESTS = `using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UcpProbe.Play
{
    public class PlayProbeTests
    {
        [UnityTest]
        public IEnumerator RunsInPlayMode()
        {
            yield return null;
            Assert.IsTrue(Application.isPlaying);
        }

        [Test]
        public void FailsInPlayMode() => Assert.Fail("ucp play probe failure");
    }
}
`;

interface PlayModeOptions {
  enabled: boolean;
  flags: number;
}

/** Sets Enter Play Mode Options and returns what they were, so a case can put them back. */
async function setPlayModeOptions(options: PlayModeOptions): Promise<PlayModeOptions> {
  const data = await ok("exec", {
    code: `var before = UnityEditor.EditorSettings.enterPlayModeOptionsEnabled + "|" + (int)UnityEditor.EditorSettings.enterPlayModeOptions;
UnityEditor.EditorSettings.enterPlayModeOptionsEnabled = ${options.enabled};
UnityEditor.EditorSettings.enterPlayModeOptions = (UnityEditor.EnterPlayModeOptions)${options.flags};
// The Test Runner saves assets mid-run; save the restored value too, or the file keeps the other.
UnityEditor.AssetDatabase.SaveAssets();
return before;`,
  });
  const [enabled, flags] = String(data.result).split("|");
  return { enabled: enabled === "True", flags: Number(flags) };
}

// EnterPlayModeOptions.DisableDomainReload | DisableSceneReload
const NO_RELOAD = { enabled: true, flags: 3 };

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

async function testsRunning(): Promise<boolean> {
  // Not ok(): state's shape depends on what the console holds, so it stays out of the snapshot.
  const state = await run("state");
  return (state.data as Record<string, unknown> | null)?.tests_running === true;
}

describe.skipIf(!runEditorTests)("test_run (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
    await cleanupScratch();

    fs.mkdirSync(projectFile(`${PROBE_FOLDER}/Editor`), { recursive: true });
    fs.writeFileSync(
      projectFile(`${PROBE_FOLDER}/Editor/${PROBE_ASSEMBLY}.asmdef`),
      `${JSON.stringify(PROBE_ASMDEF, null, 2)}\n`,
    );
    fs.writeFileSync(projectFile(`${PROBE_FOLDER}/Editor/ProbeTests.cs`), PROBE_TESTS);
    fs.mkdirSync(projectFile(`${PROBE_FOLDER}/Play`), { recursive: true });
    fs.writeFileSync(
      projectFile(`${PROBE_FOLDER}/Play/${PLAY_ASSEMBLY}.asmdef`),
      `${JSON.stringify(PLAY_ASMDEF, null, 2)}\n`,
    );
    fs.writeFileSync(projectFile(`${PROBE_FOLDER}/Play/PlayProbeTests.cs`), PLAY_TESTS);
    await ok("refresh", { compile: true }, COMPILE_TIMEOUT);
  }, COMPILE_TIMEOUT + 60_000);

  afterAll(async () => {
    await run("asset_delete", { path: PROBE_FOLDER });
    await run("refresh", { compile: true }, COMPILE_TIMEOUT);
    await cleanupScratch();
  }, COMPILE_TIMEOUT + 60_000);

  it("reports counts and each failure, and fails as tests_failed", async () => {
    const result = await rejected("test_run", { assemblies: [PROBE_ASSEMBLY] }, 120_000);
    expect(result.error_code).toBe("tests_failed");

    const data = result.data as Record<string, unknown>;
    expect(data).toMatchObject({ mode: "edit", total: 4, passed: 2, failed: 1, skipped: 1 });
    const failures = data.failures as Array<Record<string, unknown>>;
    expect(failures.map((f) => f.name)).toEqual(["UcpProbe.ProbeTests.Fails"]);
    expect(String(failures[0].message)).toContain("ucp probe failure");
  });

  it("runs single tests by full name", async () => {
    const data = await ok("test_run", { tests: ["UcpProbe.ProbeTests.Passes"] }, 120_000);
    expect(data).toMatchObject({ total: 1, passed: 1, failed: 0 });
  });

  it("calls a filter that matches nothing an invalid argument", async () => {
    const result = await rejected("test_run", { filter: "^UcpProbe\\.NoSuchTest$" }, 120_000);
    expect(result.error_code).toBe("invalid_argument");
  });

  it("runs the tests a filter regex matches", async () => {
    // Passes and Fails match, Skipped and Slow do not.
    const result = await rejected("test_run", { filter: "ProbeTests\\.(Passes|Fails)$" }, 120_000);
    expect(result.error_code).toBe("tests_failed");
    expect(result.data).toMatchObject({ total: 2, passed: 1, failed: 1 });
  });

  it("refuses to start in Play Mode", async () => {
    expect((await run("play", {}, 60_000)).ok).toBe(true);
    try {
      const result = await rejected("test_run", { tests: ["UcpProbe.ProbeTests.Passes"] });
      expect(result.error_code).toBe("edit_mode_required");
    } finally {
      await ensureEditMode();
    }
  }, 150_000);

  it("refuses to start with unsaved scenes, where the Test Runner would ask in a dialog", async () => {
    const scene = `${SCRATCH_FOLDER}/TestRun.unity`;
    await ok("asset_create_folder", { path: SCRATCH_FOLDER });
    await ok("scene_new", { setup: "empty", mode: "additive" });
    await ok("scene_save_as", { path: scene });
    await ok("gameobject_create", { scene, name: "Unsaved" });

    const result = await rejected("test_run", { tests: ["UcpProbe.ProbeTests.Passes"] });
    expect(result.message).toContain(scene);

    await ok("scene_close", { scene, force: true });
  });

  it("refuses other commands while a run outlives its caller, and leaves no result behind", async () => {
    const timedOut = await rejected("test_run", { categories: ["Slow"] }, 1_500);
    expect(timedOut.error_code).toBe("timeout");
    expect(await testsRunning()).toBe(true);

    const busy = await rejected("gameobject_find", { name: "Main", limit: 1 });
    expect(busy.error_code).toBe("busy");
    expect((await run("ping")).ok).toBe(true);

    const deadline = Date.now() + (SLOW_TEST_SECONDS + 20) * 1000;
    while ((await testsRunning()) && Date.now() < deadline) {
      await new Promise((resolve) => setTimeout(resolve, 250));
    }
    expect(await testsRunning()).toBe(false);
    expect((await run("gameobject_find", { name: "Main", limit: 1 })).error_code).not.toBe("busy");
    expect(queueFiles()).toEqual([]);
  });

  // The Test Runner enters Play Mode itself; the result is reported in the domain that comes
  // after, and only once the Editor is back in Edit Mode.
  async function expectPlayModeRun(): Promise<void> {
    const result = await rejected(
      "test_run",
      { mode: "play", assemblies: [PLAY_ASSEMBLY] },
      180_000,
    );
    expect(result.error_code).toBe("tests_failed");
    expect(result.data).toMatchObject({ mode: "play", total: 2, passed: 1, failed: 1 });
    const failures = (result.data as { failures: Array<{ message: string }> }).failures;
    expect(failures[0].message).toContain("ucp play probe failure");

    const state = await run("state");
    expect((state.data as Record<string, unknown>).is_playing).toBe(false);
    expect(await testsRunning()).toBe(false);
    expect(queueFiles()).toEqual([]);
  }

  it("runs PlayMode tests across the domain reloads of entering and leaving Play Mode", async () => {
    await expectPlayModeRun();
  }, 240_000);

  it("runs PlayMode tests with domain reload turned off", async () => {
    const before = await setPlayModeOptions(NO_RELOAD);
    try {
      await expectPlayModeRun();
    } finally {
      await setPlayModeOptions(before);
    }
  }, 240_000);

  it("answers state and ping from another caller while a test run is going", async () => {
    // The run's own caller is still waiting, so the editor's lane is taken for its whole length.
    const slow = run("test_run", { categories: ["Slow"] }, 60_000);
    await new Promise((r) => setTimeout(r, 1_000));

    const started = Date.now();
    const state = await run("state", {}, 3_000);
    expect(state.ok, state.error_code).toBe(true);
    expect((state.data as Record<string, unknown>).tests_running).toBe(true);
    expect((await run("ping", {}, 3_000)).ok).toBe(true);
    expect(Date.now() - started).toBeLessThan(3_000);

    expect((await slow).ok).toBe(true);
    expect(queueFiles()).toEqual([]);
  }, 90_000);

  it("returns results of the same shape", async () => {
    await expectShapesUnchanged("shapes.tests.json");
  });
});
