import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { hasReadyEditorSession } from "./helpers/editor-commands.js";
import {
  median,
  pingTimes,
  sessionFile,
  ticksPerSecond,
  waitForFocused,
} from "./helpers/editor-latency.js";
import { ensureEditMode } from "./helpers/play-mode.js";
import { focus, foregroundWindow, isMinimized, minimize, refocus } from "./helpers/unity-window.js";

/**
 * How fast commands return when Unity is focused. This is not the stability contract: the editor
 * runs minimized, and every other integration file is pinned there. A focused window only ticks
 * faster. Bringing Unity forward takes the focus from whoever is at the machine, so this file
 * runs only with UCP_ALLOW_FOCUS=1. The window state is given back afterwards. Windows only.
 */
const runForegroundTests =
  process.env.UCP_EDITOR_INTEGRATION === "1" &&
  process.env.UCP_ALLOW_FOCUS === "1" &&
  process.platform === "win32";

describe.skipIf(!runForegroundTests)(
  "Unity in the foreground (Unity required, UCP_ALLOW_FOCUS=1)",
  () => {
    let pid = 0;
    let wasMinimized = false;
    let previousFocus = "";

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
      previousFocus = foregroundWindow();
      expect(focus(pid), "Windows refused to bring Unity to the front").toBe(true);
    });

    afterAll(() => {
      if (pid && wasMinimized) minimize(pid);
      refocus(previousFocus);
    });

    it("reports the Editor as focused", async () => {
      await waitForFocused(true);
    });

    it("ticks far faster than in the background", async () => {
      const perSecond = await ticksPerSecond();
      process.stdout.write(`MEASURED foreground ${perSecond.toFixed(1)} ticks/s\n`);
      // Background is about 10 a second (background.test.ts).
      expect(perSecond, `${perSecond.toFixed(1)} ticks/s`).toBeGreaterThan(20);
    });

    // The agent checks its inbox at most every 20 ms in the foreground (PollIntervalSec), so a
    // ping waits about half of that, not half a tick.
    it("answers pings faster than in the background", async () => {
      const back = await pingTimes(20);
      const spaced = await pingTimes(30, 200);
      process.stdout.write(
        `MEASURED foreground ping unity_ms median ${median(back)} max ${back[back.length - 1]}; spaced median ${median(spaced)} max ${spaced[spaced.length - 1]}\n`,
      );
      // Background: about 96 ms back to back, 75 ms spaced.
      expect(median(back), `unity_ms ${JSON.stringify(back)}`).toBeLessThan(60);
      expect(median(spaced), `unity_ms ${JSON.stringify(spaced)}`).toBeLessThan(60);
    });
  },
);
