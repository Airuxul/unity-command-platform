import { beforeAll, describe, expect, it } from "vitest";
import { run } from "./helpers/content-scenario.js";
import { hasReadyEditorSession } from "./helpers/editor-commands.js";

/**
 * The in-editor command line: parsing, the caret, and hints. Execution of a runtime line is
 * covered with the runtime command cases; here the line only has to reach that executor.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

const PROBE = `
var parsed = ShellLineParser.Parse("? --command ping");
var quoted = ShellCompletionService.GetTokenAtCaret("echo --message \\"ab cd\\"", 20);
var hints = string.Join("\\n", ShellHintService.BuildHints("echo --m"));
var empty = ShellCommandExecutor.ExecuteLine("   ");
var alias = ShellCommandExecutor.ExecuteLine("?");
return new Dictionary<string, object> {
    ["command"] = parsed.CommandName,
    ["arg"] = parsed.Args["command"],
    ["token"] = quoted.value,
    ["resolved"] = ShellLineParser.ResolveRuntimeCommand("?"),
    ["hints"] = hints,
    ["empty_error"] = empty.error ?? "",
    ["empty_message"] = empty.result != null ? empty.result.message ?? "" : "",
    ["alias_error"] = alias.error ?? "",
    ["alias_code"] = alias.result != null ? alias.result.error ?? "" : "",
};
`;

describe.skipIf(!runEditorTests)("editor command line (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
  });

  it("keeps aliases and quotes, and completes parameters from the binder", async () => {
    const result = await run("exec", {
      usings: ["Air.UcpAgent.Runtime.Shell"],
      code: PROBE,
    });
    expect(result.ok, JSON.stringify(result)).toBe(true);
    const data = (result.data as { result?: Record<string, string> } | null)?.result ?? {};
    expect(data.command).toBe("?");
    expect(data.arg).toBe("ping");
    expect(data.token).toBe("ab cd");
    expect(data.resolved).toBe("help");
    expect(data.hints).toContain("message");
    expect(data.empty_error).toBe("");
    expect(data.empty_message).toBe("Enter a command.");
    expect(data.alias_error).toBe("");
    expect(data.alias_code).toBe("play_mode_required");
  });
});
