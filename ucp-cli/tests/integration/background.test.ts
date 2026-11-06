import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { ok, run } from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import {
  median,
  pingTimes,
  sessionFile,
  ticksPerSecond,
  waitForFocused,
} from "./helpers/editor-latency.js";
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
    for (const probe of [PROBE]) {
      fs.rmSync(projectFile(probe), { force: true });
      fs.rmSync(projectFile(`${probe}.meta`), { force: true });
    }
    await run("compile", {}, COMPILE_TIMEOUT);
    if (pid && !wasMinimized) restore(pid);
  }, COMPILE_TIMEOUT + 30_000);

  it("reports the Editor as not focused", async () => {
    await waitForFocused(false);
  });

  it("ticks about 10 times a second", async () => {
    const perSecond = await ticksPerSecond();
    process.stdout.write(`MEASURED background ${perSecond.toFixed(1)} ticks/s
`);
    expect(perSecond, `${perSecond.toFixed(1)} ticks/s`).toBeGreaterThanOrEqual(5);
    expect(perSecond, `${perSecond.toFixed(1)} ticks/s`).toBeLessThanOrEqual(20);
  });

  it("answers a ping within about one tick (about 100 ms)", async () => {
    const sorted = await pingTimes(20);
    process.stdout.write(
      `MEASURED background ping unity_ms median ${median(sorted)} max ${sorted[sorted.length - 1]}
`,
    );
    // One tick is about 100 ms here; the agent sees the command on the next one.
    expect(median(sorted), `unity_ms ${JSON.stringify(sorted)}`).toBeLessThan(200);
    expect(sorted[sorted.length - 1], `unity_ms ${JSON.stringify(sorted)}`).toBeLessThan(400);
  });

  it("answers a ping that arrives at a random moment within half a tick or so", async () => {
    // Back to back, each ping lands just after a tick and waits a whole one; spread out, half.
    const sorted = await pingTimes(30, 200);
    process.stdout.write(
      `MEASURED background spaced ping unity_ms median ${median(sorted)} max ${sorted[sorted.length - 1]}
`,
    );
    expect(median(sorted), `unity_ms ${JSON.stringify(sorted)}`).toBeLessThan(120);
    expect(sorted[sorted.length - 1], `unity_ms ${JSON.stringify(sorted)}`).toBeLessThan(400);
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
