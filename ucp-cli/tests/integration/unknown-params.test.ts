import { beforeAll, describe, expect, it } from "vitest";
import { run } from "./helpers/content-scenario.js";
import { hasReadyEditorSession } from "./helpers/editor-commands.js";

/**
 * Every command that declares parameters rejects a key it does not declare, and does so before
 * it runs. A command with no parameter type (ping) still accepts extra keys: there is nothing
 * to bind them to.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

describe.skipIf(!runEditorTests)("unknown parameters (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
  });

  it("rejects an unknown key on every command that declares parameters", async () => {
    const help = await run("help");
    expect(help.ok, JSON.stringify(help)).toBe(true);
    const commands = (
      (help.data as { commands?: Array<{ name: string; params?: string[] }> } | null)?.commands ??
      []
    ).filter((command) => (command.params?.length ?? 0) > 0);

    expect(commands.length).toBeGreaterThan(0);
    for (const command of commands) {
      const result = await run(command.name, { not_a_parameter: 1 });
      expect(result.ok, command.name).toBe(false);
      expect(result.error_code, `${command.name}: ${result.message}`).toBe("invalid_argument");
      expect(result.message, command.name).toContain("not_a_parameter");
    }
  }, 180_000);

  it("does not create a node when gameobject_create is given an unknown key", async () => {
    const name = "UcpUnknownParamProbe";
    const before = await run("gameobject_find", { name, exact: true });
    expect(before.ok, JSON.stringify(before)).toBe(true);
    const beforeCount = Number(
      (before.data as { total_matches?: number } | null)?.total_matches ?? 0,
    );

    const rejected = await run("gameobject_create", { name, not_a_parameter: 1 });
    expect(rejected.error_code).toBe("invalid_argument");

    const after = await run("gameobject_find", { name, exact: true });
    expect(after.ok, JSON.stringify(after)).toBe(true);
    expect(Number((after.data as { total_matches?: number } | null)?.total_matches ?? 0)).toBe(
      beforeCount,
    );
  });

  it("still runs ping when given a key, because ping declares no parameters", async () => {
    const result = await run("ping", { not_a_parameter: 1 });
    expect(result.ok, JSON.stringify(result)).toBe(true);
    expect(result.message).toBe("pong");
  });
});
