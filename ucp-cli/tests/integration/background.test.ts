import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { projectIdFromPath, readJsonFile, sessionFilePath } from "#shared/index.js";
import { ok, run } from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import { ensureEditMode } from "./helpers/play-mode.js";
import { isMinimized, minimize, restore } from "./helpers/unity-window.js";

/**
 * Unity minimized, as when it runs unattended: how often the Editor ticks, how long a command
 * takes to come back, and that compile still picks up new scripts. The window is put back the way
 * it was. Windows only (the window is minimized through user32).
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1" && process.platform === "win32";

const PROBE = "Assets/UcpBackgroundProbe.cs";
const COMPILE_TIMEOUT = 180_000;

function sessionFile(): { pid?: number; focused?: boolean } {
  const id = projectIdFromPath(gameDemoProjectPath());
  return (readJsonFile(sessionFilePath(id, process.env.UCP_ROOT)) ?? {}) as {
    pid?: number;
    focused?: boolean;
  };
}

function projectFile(assetPath: string): string {
  return path.join(gameDemoProjectPath(), ...assetPath.split("/"));
}

describe.skipIf(!runEditorTests)("Unity in the background (Unity required, Windows)", () => {
  let pid = 0;
  let wasMinimized = false;

  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
    pid = sessionFile().pid ?? 0;
    expect(pid, "session file has no pid").toBeGreaterThan(0);
    wasMinimized = isMinimized(pid);
    minimize(pid);
    expect(isMinimized(pid)).toBe(true);
  });

  afterAll(async () => {
    fs.rmSync(projectFile(PROBE), { force: true });
    fs.rmSync(projectFile(`${PROBE}.meta`), { force: true });
    await run("compile", {}, COMPILE_TIMEOUT);
    if (pid && !wasMinimized) restore(pid);
  }, COMPILE_TIMEOUT + 30_000);

  it("reports the Editor as not focused", async () => {
    // The agent rewrites the session file every 2 s.
    const deadline = Date.now() + 10_000;
    while (sessionFile().focused !== false && Date.now() < deadline) {
      await new Promise((r) => setTimeout(r, 250));
    }
    expect(sessionFile().focused).toBe(false);
  });

  it("ticks about 10 times a second", async () => {
    const key = "ucp.test.ticks";
    await ok("exec", {
      code: `UnityEditor.SessionState.SetInt("${key}", 0);
var end = UnityEditor.EditorApplication.timeSinceStartup + 3;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () => {
    UnityEditor.SessionState.SetInt("${key}", UnityEditor.SessionState.GetInt("${key}", 0) + 1);
    if (UnityEditor.EditorApplication.timeSinceStartup >= end) UnityEditor.EditorApplication.update -= tick;
};
UnityEditor.EditorApplication.update += tick;
return 0;`,
    });
    await new Promise((r) => setTimeout(r, 4_000));
    const ticks = Number(
      (await ok("exec", { code: `return UnityEditor.SessionState.GetInt("${key}", 0);` })).result,
    );
    const perSecond = ticks / 3;
    process.stdout.write(`MEASURED ${perSecond.toFixed(1)} ticks/s
`);
    expect(perSecond, `${perSecond.toFixed(1)} ticks/s`).toBeGreaterThanOrEqual(5);
    expect(perSecond, `${perSecond.toFixed(1)} ticks/s`).toBeLessThanOrEqual(20);
  });

  it("answers a ping within about one tick (about 100 ms)", async () => {
    const times: number[] = [];
    for (let i = 0; i < 20; i++) {
      const result = await run("ping");
      expect(result.ok).toBe(true);
      times.push(result.timing?.unity_ms ?? Number.POSITIVE_INFINITY);
    }
    const sorted = [...times].sort((a, b) => a - b);
    const median = sorted[10];
    process.stdout.write(`MEASURED ping unity_ms median ${median} max ${sorted[19]}
`);
    // One tick is about 100 ms here; the agent sees the command on the next one.
    expect(median, `unity_ms ${JSON.stringify(sorted)}`).toBeLessThan(200);
    expect(sorted[19], `unity_ms ${JSON.stringify(sorted)}`).toBeLessThan(400);
  });

  it("answers a ping that arrives at a random moment within half a tick or so", async () => {
    // Back to back, each ping lands just after a tick and waits a whole one; spread out, half.
    const times: number[] = [];
    for (let i = 0; i < 30; i++) {
      await new Promise((r) => setTimeout(r, Math.random() * 200));
      const result = await run("ping");
      expect(result.ok).toBe(true);
      times.push(result.timing?.unity_ms ?? Number.POSITIVE_INFINITY);
    }
    const sorted = [...times].sort((a, b) => a - b);
    const median = sorted[15];
    process.stdout.write(`MEASURED spaced ping unity_ms median ${median} max ${sorted[29]}\n`);
    expect(median, `unity_ms ${JSON.stringify(sorted)}`).toBeLessThan(120);
    expect(sorted[29], `unity_ms ${JSON.stringify(sorted)}`).toBeLessThan(400);
  });

  it(
    "compiles a script written while Unity was in the background",
    async () => {
      // Unity imports changed files on focus only; compile has to import them itself.
      const value = Date.now() % 100_000;
      fs.writeFileSync(
        projectFile(PROBE),
        `public static class UcpBackgroundProbe { public static int V => ${value}; }\n`,
      );
      await ok("compile", {}, COMPILE_TIMEOUT);
      const data = await ok("exec", { code: "return UcpBackgroundProbe.V;" });
      expect(Number(data.result)).toBe(value);
    },
    COMPILE_TIMEOUT,
  );
});
