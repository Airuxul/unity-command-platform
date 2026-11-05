import { beforeAll, describe, expect, it } from "vitest";
import { executeCommand } from "#cli/client/ucp-host-client.js";
import { parseScript, runScript } from "#cli/commands/run-script.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import { ensureEditMode, waitForPlayMode } from "./helpers/play-mode.js";

/** A flow across the two domain reloads of play and stop, each step resumed like a single run. */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

const FLOW = JSON.stringify({
  steps: [
    { command: "state" },
    { command: "play", timeout: 60_000 },
    { command: "ui_snapshot" },
    { command: "stop", timeout: 60_000 },
    { command: "ui_snapshot" },
    { command: "ping" },
  ],
});

describe.skipIf(!runEditorTests)("run-script (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
  });

  it("runs play and stop in one flow, and stops at the first failure", async () => {
    const result = await runScript(
      parseScript(FLOW),
      { project: gameDemoProjectPath(), sessionType: "editor" },
      executeCommand,
    );

    // ui_snapshot after stop is out of Play Mode: the flow stops there and never pings.
    expect(result.ok).toBe(false);
    expect(result.error_code).toBe("play_mode_required");
    const data = result.data as {
      steps: Array<{ command: string; ok: boolean }>;
      failed_at: number;
    };
    expect(data.steps.map((s) => [s.command, s.ok])).toEqual([
      ["state", true],
      ["play", true],
      ["ui_snapshot", true],
      ["stop", true],
      ["ui_snapshot", false],
    ]);
    expect(data.failed_at).toBe(4);
    await waitForPlayMode("edit", 60_000);
  }, 180_000);
});
