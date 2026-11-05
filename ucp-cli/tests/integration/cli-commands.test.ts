import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { beforeAll, describe, expect, it } from "vitest";
import { resolvePackageRoot } from "#shared/package-root.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";

/**
 * The ucp-cli subcommands that wrap a command (build, help, run-script): the real binary, so the
 * mapping from flags to arguments and the exit code are what a user gets.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

function cli(...args: string[]): {
  status: number | null;
  json: Record<string, unknown>;
  stdout: string;
} {
  const bin = path.join(resolvePackageRoot(), "bin", "ucp-cli.js");
  const res = spawnSync(process.execPath, [bin, "--project", gameDemoProjectPath(), ...args], {
    encoding: "utf8",
    timeout: 120_000,
  });
  let json: Record<string, unknown> = {};
  try {
    json = JSON.parse(res.stdout);
  } catch {
    // help --md prints markdown.
  }
  return { status: res.status, json, stdout: res.stdout };
}

describe.skipIf(!runEditorTests)("ucp-cli subcommands (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
  });

  it("build passes its flags on as the command's arguments", () => {
    // Refused by the agent before it builds, so the message shows which arguments arrived.
    const res = cli(
      "build",
      "Builds/x.exe",
      "--platform",
      "NoSuchTarget",
      "--scene",
      "Assets/A.unity",
    );
    expect(res.status).toBe(1);
    expect(res.json).toMatchObject({ ok: false, error_code: "invalid_argument" });
    expect(String(res.json.message)).toContain("NoSuchTarget");
  });

  it("help prints one card, and markdown with --md", () => {
    const card = cli("help", "ui_wait");
    expect(card.status).toBe(0);
    expect((card.json.data as { command: { name: string } }).command.name).toBe("ui_wait");

    const md = cli("help", "ui_wait", "--md");
    expect(md.status).toBe(0);
    expect(md.stdout).toContain("## ui_wait");
  });

  it("run-script reports every step and exits 1 at the first failure", () => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "ucp-flow-")), "flow.json");
    fs.writeFileSync(
      file,
      JSON.stringify([
        { command: "ping" },
        { command: "gameobject_info", args: { node: "Ucp/No/Such/Node" } },
        { command: "echo", args: { message: "never" } },
      ]),
    );
    const res = cli("run-script", file);
    expect(res.status).toBe(1);
    expect(res.json).toMatchObject({ ok: false, error_code: "node_not_found" });
    expect(res.json.data).toMatchObject({ failed_at: 1, not_run: 1 });

    fs.writeFileSync(file, "[{}]");
    const bad = cli("run-script", file);
    expect(bad.status).toBe(1);
    expect(bad.json).toMatchObject({ ok: false, error_code: "invalid_argument" });
  });
});
