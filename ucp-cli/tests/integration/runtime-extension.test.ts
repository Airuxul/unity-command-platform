import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { run } from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import {
  LOCAL_RUNTIME_TARGET_ID,
  ensureEditMode,
  ensureLocalRuntimeTarget,
  runUcpCommand,
  waitForPlayMode,
  waitForRuntimeSession,
} from "./helpers/play-mode.js";

/**
 * A command added by the project, with no registration list. Runtime scope is visible only on
 * the runtime host, and only after it has been compiled.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";
const FOLDER = "Assets/UcpRuntimeProbe";
const COMMAND = "ucp_project_runtime";
const COMPILE_TIMEOUT = 180_000;

const SOURCE = `using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;

[UcpCommand]
public static class UcpProjectRuntimeCommand
{
    public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
        "${COMMAND}", CommandHostScope.Runtime, "project runtime probe");

    public static object Execute(CommandContext ctx)
    {
        ctx.Message = "project runtime";
        return new Dictionary<string, object> { ["where"] = "runtime" };
    }
}
`;

function namesOf(result: Awaited<ReturnType<typeof run>>): string[] {
  const commands = (result.data as { commands?: Array<{ name: string }> } | null)?.commands ?? [];
  return commands.map((command) => command.name);
}

describe.skipIf(!runEditorTests)("project runtime command (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
    const folder = path.join(gameDemoProjectPath(), ...FOLDER.split("/"));
    fs.mkdirSync(folder, { recursive: true });
    fs.writeFileSync(path.join(folder, "UcpProjectRuntimeCommand.cs"), SOURCE);
    const compiled = await run("compile", {}, COMPILE_TIMEOUT);
    expect(compiled.ok, JSON.stringify(compiled)).toBe(true);
  }, COMPILE_TIMEOUT + 30_000);

  afterAll(async () => {
    await ensureEditMode();
    await run("asset_delete", { path: FOLDER });
    await run("compile", {}, COMPILE_TIMEOUT);
  }, COMPILE_TIMEOUT + 60_000);

  it("is not a command on the editor host", async () => {
    const help = await run("help");
    expect(help.ok, JSON.stringify(help)).toBe(true);
    expect(namesOf(help)).not.toContain(COMMAND);

    const direct = await run(COMMAND);
    expect(direct.ok).toBe(false);
    expect(direct.error_code).toBe("host_unknown_command");
  });

  it("is discovered and runs on the runtime host", async () => {
    ensureLocalRuntimeTarget();
    const play = await runUcpCommand("play", { sessionType: "editor", timeout: 60_000 });
    expect(play.ok, JSON.stringify(play)).toBe(true);
    await waitForPlayMode("playing", 60_000);
    await waitForRuntimeSession(undefined, 60_000);

    try {
      const help = await runUcpCommand("help", {
        sessionType: "runtime",
        target: LOCAL_RUNTIME_TARGET_ID,
      });
      expect(help.ok, JSON.stringify(help)).toBe(true);
      expect(namesOf(help)).toContain(COMMAND);

      const ran = await runUcpCommand(COMMAND, {
        sessionType: "runtime",
        target: LOCAL_RUNTIME_TARGET_ID,
      });
      expect(ran.ok, JSON.stringify(ran)).toBe(true);
      expect(ran.message).toBe("project runtime");
      expect((ran.data as { where?: string } | null)?.where).toBe("runtime");
    } finally {
      await ensureEditMode();
    }
  }, 180_000);
});
