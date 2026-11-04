import { beforeAll, describe, expect, it } from "vitest";
import { SCENARIO_COMMANDS, run } from "./helpers/content-scenario.js";
import {
  ALL_EDITOR_COMMANDS,
  EDITOR_COMMAND_ALIASES,
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
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
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
      if (!result.ok && !testCase.expectFailure) {
        console.error("command failed", testCase.type, result);
      }
      testCase.assert(result);
      expect(result.ok).toBe(!testCase.expectFailure);
    });
  }
});

describe("editor command catalog", () => {
  it("covers every editor command with a case or a scenario", () => {
    const covered = new Set([...EDITOR_COMMAND_CASES.map((c) => c.type), ...SCENARIO_COMMANDS]);
    const untested = ALL_EDITOR_COMMANDS.filter((name) => !covered.has(name));
    expect(untested, "add a case to EDITOR_COMMAND_CASES or SCENARIO_COMMANDS").toEqual([]);
  });

  it("does not reference commands that no longer exist", () => {
    const known = new Set([...ALL_EDITOR_COMMANDS, ...EDITOR_COMMAND_ALIASES]);
    const unknown = [...EDITOR_COMMAND_CASES.map((c) => c.type), ...SCENARIO_COMMANDS].filter(
      (name) => !known.has(name),
    );
    expect(unknown).toEqual([]);
  });

  it("lists each command once", () => {
    expect(new Set(ALL_EDITOR_COMMANDS).size).toBe(ALL_EDITOR_COMMANDS.length);
    expect(ALL_EDITOR_COMMANDS.length).toBe(82);
  });
});

describe.skipIf(!runEditorTests)("editor capability advertisement (Unity required)", () => {
  it("advertises exactly the expected commands, aliases included", async () => {
    const result = await run("help");
    expect(result.ok, String(result.message ?? result.error)).toBe(true);

    const data = (result.data ?? {}) as Record<string, unknown>;
    const live = (data.commands as Array<{ name: string }>).map((c) => c.name).sort();
    expect(live).toEqual([...ALL_EDITOR_COMMANDS].sort());

    // Aliases only dispatch if the registry advertised them as capabilities too.
    const aliasMap = (data.alias_to_command ?? {}) as Record<string, string>;
    for (const alias of EDITOR_COMMAND_ALIASES) {
      expect(Object.keys(aliasMap), `alias ${alias} missing from catalog`).toContain(alias);
    }
  });

  it("dispatches an alias to its canonical command", async () => {
    const result = await run("commands");
    expect(result.ok, String(result.message ?? result.error)).toBe(true);
    const data = (result.data ?? {}) as Record<string, unknown>;
    expect(Array.isArray(data.commands)).toBe(true);
  });
});
