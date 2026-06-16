import { beforeAll, describe, expect, it } from "vitest";
import {
  EDITOR_COMMAND_CASES,
  hasReadyEditorSession,
  runEditorCommand,
} from "./helpers/editor-commands.js";

const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";
const includeSlow = process.env.UCP_EDITOR_SLOW === "1";

describe.skipIf(!runEditorTests)("editor commands (Unity required)", () => {
  beforeAll(async () => {
    const ready = await hasReadyEditorSession();
    if (!ready) {
      throw new Error(
        "No ready Unity Editor session. Open GameDemo in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    const { ensureEditMode } = await import("./helpers/play-mode.js");
    await ensureEditMode();
  });

  for (const testCase of EDITOR_COMMAND_CASES) {
    const shouldSkip = testCase.skip && !includeSlow;
    const label = `${testCase.type} — ${testCase.description}`;

    it.skipIf(shouldSkip)(label, async () => {
      const result = await runEditorCommand(testCase);
      if (!result.ok) {
        console.error("command failed", testCase.type, result);
      }
      testCase.assert(result);
      expect(result.ok).toBe(true);
    });
  }
});

describe("editor command catalog", () => {
  it("defines cases for all migrated editor command types", () => {
    const types = EDITOR_COMMAND_CASES.map((c) => c.type).sort();
    expect(types).toEqual(
      [
        "build",
        "compile",
        "console",
        "echo",
        "exec",
        "manage",
        "menu",
        "ping",
        "play",
        "profiler",
        "refresh",
        "reserialize",
        "screenshot",
        "state",
        "stop",
      ].sort(),
    );
  });
});
