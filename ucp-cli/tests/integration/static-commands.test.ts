import fs from "node:fs";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { ok, rejected, run } from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import { ensureEditMode, runUcpCommand } from "./helpers/play-mode.js";

/**
 * Commands are static classes marked [UcpCommand]; functions are static methods marked
 * [UcpFunction] that `call` runs. Neither is checked by the compiler, so CommandRegistry and
 * FunctionRegistry check them on load and `help` reports what they skipped as load_errors.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";
const FOLDER = "Assets/UcpStaticProbe";
const COMPILE_TIMEOUT = 180_000;

const BROKEN = `using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Completion;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

[UcpCommand]
public static class UcpProbeReturnsString
{
    public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor("ucp_probe_returns_string", CommandHostScope.Editor, "bad");
    public static string Execute(CommandContext ctx) => "x";
}

[UcpCommand]
public static class UcpProbeKeepsState
{
    public static int Count;
    public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor("ucp_probe_keeps_state", CommandHostScope.Editor, "bad");
    public static object Execute(CommandContext ctx) => Count++;
}

[UcpCommand]
public sealed class UcpProbeNotStatic
{
    public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor("ucp_probe_not_static", CommandHostScope.Editor, "bad");
    public static object Execute(CommandContext ctx) => null;
}

[UcpCommand]
public static class UcpProbeEditsWithPlainContext
{
    public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<NodeParams>("ucp_probe_plain_context", CommandHostScope.Editor, "bad");
    public static object Execute(NodeParams p, CommandContext ctx) => null;
}

[UcpCommand]
public static class UcpProbeDeferredEdit
{
    public static readonly InvokeDescriptor Descriptor = new DeferredInvokeDescriptor<NodeParams>(
        "ucp_probe_deferred_edit", CommandHostScope.Editor, "bad", typeof(CompilationPolicy));
    public static object Execute(NodeParams p, EditTargetContext ctx) => null;
}

public sealed class UcpProbeLiveContext : CommandContext
{
    public UnityEngine.GameObject Node;
}

[UcpCommand]
public static class UcpProbeLiveContextCommand
{
    public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor("ucp_probe_live_context", CommandHostScope.Editor, "bad");
    public static object Execute(UcpProbeLiveContext ctx) => null;
}

[UcpCommand]
public static class UcpProbeUndoMismatch
{
    public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor("ucp_probe_undo_mismatch", CommandHostScope.Editor, "bad");
    public static object Execute(CommandContext ctx) => null;
    public static void Undo(EchoParams p, CommandContext ctx) { }
}

public sealed class UcpProbeBrokenFunctions
{
    [UcpFunction("ucp_probe_dup", Scope = CommandHostScope.Editor)]
    public static void First() { }

    [UcpFunction("ucp_probe_dup", Scope = CommandHostScope.Editor)]
    public static void Second() { }

    [UcpFunction("ucp_probe_instance")]
    public void NotStatic() { }
}
`;

const GOOD = `using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

public sealed class UcpProbeGoodContext : CommandContext
{
    public string Seen;
}

[UcpCommand]
public static class UcpProbeGoodCommand
{
    public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<EchoParams>(
        "ucp_probe_good", CommandHostScope.Editor, "test probe: echoes through its own context");

    public static object Execute(EchoParams p, UcpProbeGoodContext ctx)
    {
        ctx.Seen = p.Message;
        ctx.Message = "probe ran";
        return new Dictionary<string, object> { ["seen"] = ctx.Seen, ["id"] = ctx.CommandId };
    }

    public static void Undo(EchoParams p, UcpProbeGoodContext ctx) { }
}

public static class UcpProbeFunctions
{
    [UcpFunction("ucp_probe_add", Description = "adds two numbers", Scope = CommandHostScope.Editor)]
    public static object Add(int a, int b = 2) => new Dictionary<string, object> { ["sum"] = a + b };

    [UcpFunction("ucp_probe_context", Scope = CommandHostScope.Editor)]
    public static object Context(string label, CommandContext ctx)
    {
        ctx.Message = "context " + label;
        return ctx.CommandId != null;
    }

    [UcpFunction("ucp_probe_throws", Scope = CommandHostScope.Editor)]
    public static void Throws() => throw new System.InvalidOperationException("probe failed on purpose");

    [UcpFunction("ucp_probe_void", Scope = CommandHostScope.Editor)]
    public static void Nothing() { }

    [UcpFunction("ucp_probe_anywhere", Description = "listed on both hosts")]
    public static object Anywhere() => "here";
}
`;

function projectFile(assetPath: string): string {
  return path.join(gameDemoProjectPath(), ...assetPath.split("/"));
}

async function loadErrors(): Promise<string[]> {
  return ((await ok("help")).load_errors as string[]) ?? [];
}

async function functions(sessionType: "editor" | "runtime" = "editor"): Promise<string[]> {
  const result = await runUcpCommand("call", { sessionType });
  expect(result.ok, String(result.message)).toBe(true);
  return ((result.data as { functions: Array<{ name: string }> }).functions ?? []).map((f) => f.name);
}

describe.skipIf(!runEditorTests)("static commands and functions (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
    expect(await loadErrors()).toEqual([]);
    fs.mkdirSync(projectFile(`${FOLDER}/Editor`), { recursive: true });
    fs.writeFileSync(projectFile(`${FOLDER}/Editor/UcpProbeBroken.cs`), BROKEN);
    fs.writeFileSync(projectFile(`${FOLDER}/UcpProbeGood.cs`), GOOD);
    await ok("compile", {}, COMPILE_TIMEOUT);
  }, COMPILE_TIMEOUT + 30_000);

  afterAll(async () => {
    await ensureEditMode();
    await run("asset_delete", { path: FOLDER });
    await run("compile", {}, COMPILE_TIMEOUT);
  }, COMPILE_TIMEOUT + 60_000);

  it("reports every malformed command and function, and skips only those", async () => {
    const errors = await loadErrors();
    const expected: Array<[string, string]> = [
      ["UcpProbeReturnsString", "Execute returns object"],
      ["UcpProbeKeepsState", "static field 'Count' is not readonly"],
      ["UcpProbeNotStatic", "a command is a static class"],
      ["UcpProbeEditsWithPlainContext", "Execute takes an EditTargetContext"],
      ["UcpProbeDeferredEdit", "cannot be deferred"],
      ["UcpProbeLiveContextCommand", "UcpProbeLiveContext.Node"],
      ["UcpProbeUndoMismatch", "Undo returns void and takes the same parameters as Execute"],
      ["NotStatic", "a function is a public static method"],
      ["ucp_probe_dup", "none of them is listed"],
    ];
    for (const [who, why] of expected) {
      expect(
        errors.some((e) => e.includes(who) && e.includes(why)),
        `${who}: ${why}\n${errors.join("\n")}`,
      ).toBe(true);
    }
    expect(errors).toHaveLength(expected.length);

    const help = await ok("help");
    const names = (help.commands as Array<{ name: string }>).map((c) => c.name);
    expect(names).toContain("ucp_probe_good");
    for (const skipped of ["ucp_probe_returns_string", "ucp_probe_keeps_state", "ucp_probe_deferred_edit"]) {
      expect(names).not.toContain(skipped);
    }
    // The other commands still run.
    expect((await ok("echo", { message: "still here" })).message).toBe("still here");
  });

  it("runs a well-formed static command with its own context type", async () => {
    const result = await run("ucp_probe_good", { message: "hi" });
    expect(result.ok, String(result.message)).toBe(true);
    expect(result.message).toBe("probe ran");
    const data = result.data as Record<string, unknown>;
    expect(data.seen).toBe("hi");
    expect(String(data.id)).toMatch(/^cmd-/);
  });

  it("call lists the functions on this host", async () => {
    const result = await ok("call");
    const listed = result.functions as Array<{ name: string; description: string; params: string[] }>;
    const add = listed.find((f) => f.name === "ucp_probe_add");
    expect(add?.description).toBe("adds two numbers");
    expect(add?.params).toEqual(["a <int>", "b <int> (optional)"]);
    expect(listed.find((f) => f.name === "ucp_probe_context")?.params).toEqual(["label <string>"]);
    expect(listed.map((f) => f.name)).not.toContain("ucp_probe_dup");
  });

  it("call binds parameters by name, fills defaults, and injects the context", async () => {
    expect((await ok("call", { name: "ucp_probe_add", args: { a: 3, b: 4 } })).sum).toBe(7);
    expect((await ok("call", { name: "ucp_probe_add", args: { A: 3 } })).sum).toBe(5);

    const context = await run("call", { name: "ucp_probe_context", args: { label: "x" } });
    expect(context.ok).toBe(true);
    expect(context.message).toBe("context x");
    expect((context.data as Record<string, unknown>).value).toBe(true);

    const nothing = await run("call", { name: "ucp_probe_void" });
    expect(nothing.ok).toBe(true);
    expect(nothing.message).toBe("called ucp_probe_void");
  });

  it("call fails with the right code for bad arguments, a throwing function and an unknown name", async () => {
    const wrongType = await rejected("call", { name: "ucp_probe_add", args: { a: "three" } });
    expect(wrongType.error_code).toBe("invalid_argument");
    expect(wrongType.message).toContain("'a' expects int");

    const missing = await rejected("call", { name: "ucp_probe_add", args: {} });
    expect(missing.error_code).toBe("invalid_argument");
    expect(missing.message).toContain("needs 'a'");

    const extra = await rejected("call", { name: "ucp_probe_add", args: { a: 1, c: 2 } });
    expect(extra.error_code).toBe("invalid_argument");
    expect(extra.message).toContain("no parameter 'c'");

    const thrown = await rejected("call", { name: "ucp_probe_throws" });
    expect(thrown.error_code).toBe("command_failed");
    expect(thrown.message).toContain("probe failed on purpose");

    const unknown = await rejected("call", { name: "ucp_probe_nope" });
    expect(unknown.error_code).toBe("unknown_command");
  });

  it("the runtime host lists only the functions that run there", async () => {
    await ok("play", {}, 120_000);
    try {
      const listed = await functions("runtime");
      expect(listed).toContain("ucp_probe_anywhere");
      expect(listed).not.toContain("ucp_probe_add");
      const result = await runUcpCommand("call", { sessionType: "runtime", args: { name: "ucp_probe_add", args: { a: 1 } } });
      expect(result.ok).toBe(false);
      expect(result.error_code).toBe("unknown_command");
    } finally {
      await ensureEditMode();
    }
    expect(await functions("editor")).toEqual(expect.arrayContaining(["ucp_probe_add", "ucp_probe_anywhere"]));
  }, 240_000);
});
