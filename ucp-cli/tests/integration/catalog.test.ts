import fs from "node:fs";
import { beforeAll, describe, expect, it } from "vitest";
import { projectIdFromPath, sessionFilePath } from "#shared/index.js";
import { ok } from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";

/**
 * Locks the agent's external surface: every command name, alias, scope, completion policy,
 * timeout and parameter line, as `help` reports them and as the session file advertises them.
 *
 * A refactor must leave these files unchanged. An intended change to the surface updates the
 * snapshot in the same commit (`vitest -u`) and is listed in the README migration notes, so
 * the diff of this file is the review of what changed for callers.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

describe.skipIf(!runEditorTests)("command catalog snapshot (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
  });

  it("editor help catalog is unchanged", async () => {
    const help = await ok("help");
    const commands = (help.commands as Array<Record<string, unknown>>)
      .map((c) => ({ ...c }))
      .sort((a, b) => String(a.name).localeCompare(String(b.name)));
    const surface = {
      commands,
      alias_to_command: Object.fromEntries(
        Object.entries((help.alias_to_command as Record<string, string>) ?? {}).sort(([a], [b]) =>
          a.localeCompare(b),
        ),
      ),
    };

    await expect(`${JSON.stringify(surface, null, 2)}\n`).toMatchFileSnapshot(
      "./__snapshots__/catalog.editor.json",
    );
  });

  it("editor session advertises the same capabilities", async () => {
    const file = sessionFilePath(projectIdFromPath(gameDemoProjectPath()), process.env.UCP_ROOT);
    const session = JSON.parse(fs.readFileSync(file, "utf8").replace(/^﻿/, "")) as {
      capabilities: string[];
    };
    const capabilities = [...session.capabilities].sort();

    await expect(`${JSON.stringify(capabilities, null, 2)}\n`).toMatchFileSnapshot(
      "./__snapshots__/capabilities.editor.json",
    );
  });
});
