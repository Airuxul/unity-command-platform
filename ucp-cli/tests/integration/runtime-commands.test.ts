import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { hasReadyEditorSession } from "./helpers/editor-commands.js";
import {
  LOCAL_RUNTIME_TARGET_ID,
  ensureEditMode,
  ensureLocalRuntimeTarget,
  runUcpCommand,
  waitForPlayMode,
  waitForRuntimeSession,
} from "./helpers/play-mode.js";
import {
  ALL_RUNTIME_COMMANDS,
  BOTH_HOST_ALIASES,
  BOTH_HOST_COMMANDS,
  RUNTIME_COMMAND_ALIASES,
  RUNTIME_COMMAND_CASES,
} from "./helpers/runtime-commands.js";

const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

describe("runtime command catalog", () => {
  it("covers every runtime command on the runtime host", () => {
    const covered = new Set(
      RUNTIME_COMMAND_CASES.filter((c) => c.hosts.includes("runtime")).map((c) => c.type),
    );
    const untested = ALL_RUNTIME_COMMANDS.filter((name) => !covered.has(name));
    expect(untested, "add a runtime case for this command").toEqual([]);
  });

  it("covers every runtime alias", () => {
    const covered = new Set(RUNTIME_COMMAND_CASES.map((c) => c.type));
    const untested = RUNTIME_COMMAND_ALIASES.filter((alias) => !covered.has(alias));
    expect(untested, "dispatch this alias in RUNTIME_COMMAND_CASES").toEqual([]);
  });

  it("runs every command that exists on both hosts against both hosts", () => {
    const missing = BOTH_HOST_COMMANDS.filter(
      (name) =>
        !RUNTIME_COMMAND_CASES.some(
          (c) => c.type === name && c.hosts.includes("editor") && c.hosts.includes("runtime"),
        ),
    );
    expect(missing, "this command must be asserted on editor and runtime").toEqual([]);

    const missingAliases = BOTH_HOST_ALIASES.filter(
      (alias) =>
        !RUNTIME_COMMAND_CASES.some(
          (c) => c.type === alias && c.hosts.includes("editor") && c.hosts.includes("runtime"),
        ),
    );
    expect(missingAliases, "this alias must be asserted on editor and runtime").toEqual([]);
  });
});

describe.skipIf(!runEditorTests)("runtime commands (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
    ensureLocalRuntimeTarget();
    const play = await runUcpCommand("play", { sessionType: "editor", timeout: 60_000 });
    expect(play.ok, JSON.stringify(play)).toBe(true);
    await waitForPlayMode("playing", 60_000);
    await waitForRuntimeSession(undefined, 60_000);
  }, 120_000);

  afterAll(async () => {
    await ensureEditMode();
  }, 120_000);

  for (const testCase of RUNTIME_COMMAND_CASES) {
    for (const host of testCase.hosts) {
      it(`${testCase.type} on ${host} — ${testCase.description}`, async () => {
        const result = await runUcpCommand(testCase.type, {
          sessionType: host,
          args: testCase.args,
          target: host === "runtime" ? LOCAL_RUNTIME_TARGET_ID : undefined,
        });
        testCase.assert(host, result);
      });
    }
  }

  it("does not run an editor command on the runtime host", async () => {
    const name = "UcpRuntimeMustNotCreate";
    const created = await runUcpCommand("gameobject_create", {
      sessionType: "runtime",
      target: LOCAL_RUNTIME_TARGET_ID,
      args: { name },
    });
    expect(created.ok).toBe(false);
    expect(created.error_code).toBe("host_unknown_command");

    const found = await runUcpCommand("gameobject_find", {
      sessionType: "editor",
      args: { name, exact: true },
    });
    expect(found.ok, JSON.stringify(found)).toBe(true);
    expect((found.data as { total_matches?: number } | null)?.total_matches).toBe(0);
  });

  it("runtime help catalog matches the committed command list", async () => {
    const help = await runUcpCommand("help", {
      sessionType: "runtime",
      target: LOCAL_RUNTIME_TARGET_ID,
    });
    expect(help.ok, JSON.stringify(help)).toBe(true);
    const data = (help.data ?? {}) as {
      commands?: Array<Record<string, unknown>>;
      alias_to_command?: Record<string, string>;
    };
    const commands = [...(data.commands ?? [])].sort((a, b) =>
      String(a.name).localeCompare(String(b.name)),
    );
    const surface = {
      commands,
      alias_to_command: Object.fromEntries(
        Object.entries(data.alias_to_command ?? {}).sort(([a], [b]) => a.localeCompare(b)),
      ),
    };
    await expect(`${JSON.stringify(surface, null, 2)}\n`).toMatchFileSnapshot(
      "./__snapshots__/catalog.runtime.commands.json",
    );
  });
});
