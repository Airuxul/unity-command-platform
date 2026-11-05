import { describe, expect, it } from "vitest";
import { type Executor, parseScript, runScript } from "#cli/commands/run-script.js";
import type { CliResult } from "#shared/types.js";

function fakeExecutor(results: Record<string, CliResult>) {
  const calls: Array<{ type: string; options: Parameters<Executor>[1] }> = [];
  const execute: Executor = async (type, options) => {
    calls.push({ type, options });
    return results[type] ?? { ok: true, data: { type } };
  };
  return { calls, execute };
}

describe("parseScript", () => {
  it("takes a bare array or a steps object", () => {
    expect(parseScript('[{"command":"ping"}]')).toEqual([{ command: "ping" }]);
    expect(parseScript('{"steps":[{"command":" play ","timeout":60000}]}')).toEqual([
      { command: "play", timeout: 60000 },
    ]);
  });

  it("names the step at fault", () => {
    expect(() => parseScript('[{"command":"ping"},{"args":{}}]')).toThrow(
      'steps[1]: needs a "command"',
    );
    expect(() => parseScript('[{"command":"ping","timeout":-1}]')).toThrow("steps[0]");
    expect(() => parseScript('[{"command":"ping","args":[1]}]')).toThrow(
      '"args" must be an object',
    );
    expect(() => parseScript('[{"command":"ping","session_type":"player"}]')).toThrow(
      "session_type",
    );
    expect(() => parseScript("[]")).toThrow("no steps");
    expect(() => parseScript('{"command":"ping"}')).toThrow("array of steps");
  });
});

describe("runScript", () => {
  it("runs every step in order and reports each", async () => {
    const { calls, execute } = fakeExecutor({});
    const result = await runScript(
      [{ command: "play" }, { command: "ui_click", args: { node: "A" }, timeout: 5000 }],
      { project: "P", timeout: 30000 },
      execute,
    );

    expect(result.ok).toBe(true);
    expect(calls.map((c) => c.type)).toEqual(["play", "ui_click"]);
    expect(calls[0].options).toMatchObject({ project: "P", timeout: 30000, args: {} });
    expect(calls[1].options).toMatchObject({ project: "P", timeout: 5000, args: { node: "A" } });
    const data = result.data as { steps: unknown[]; failed_at: number | null };
    expect(data.steps).toHaveLength(2);
    expect(data.failed_at).toBeNull();
  });

  it("stops at the first failure and carries its code", async () => {
    const { calls, execute } = fakeExecutor({
      ui_click: {
        ok: false,
        error_code: "not_interactable",
        message: "'A' is inactive.",
        hint: "wait",
      },
    });
    const result = await runScript(
      [{ command: "play" }, { command: "ui_click" }, { command: "stop" }],
      {},
      execute,
    );

    expect(result.ok).toBe(false);
    expect(result.error_code).toBe("not_interactable");
    expect(result.hint).toBe("wait");
    expect(result.message).toContain("steps[1] ui_click failed: 'A' is inactive.");
    expect(calls.map((c) => c.type)).toEqual(["play", "ui_click"]);
    expect(result.data).toMatchObject({ failed_at: 1, not_run: 1 });
  });
});
