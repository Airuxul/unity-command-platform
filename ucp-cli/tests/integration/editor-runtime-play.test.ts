import { beforeAll, describe, expect, it } from "vitest";
import { hasReadyEditorSession } from "./helpers/editor-commands.js";
import {
  LOCAL_RUNTIME_TARGET_ID,
  ensureEditMode,
  ensureLocalRuntimeTarget,
  runUcpCommand,
  waitForPlayMode,
  waitForRuntimeSession,
  waitForRuntimeSessionAbsent,
} from "./helpers/play-mode.js";

const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

describe.skipIf(!runEditorTests)("editor + runtime play workflow (Unity required)", () => {
  beforeAll(async () => {
    const ready = await hasReadyEditorSession();
    if (!ready) {
      throw new Error(
        "No ready Unity Editor session. Open GameDemo in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
    ensureLocalRuntimeTarget();
  }, 120_000);

  it("enters play, runs editor+runtime commands, stops play, runtime fails afterward", async () => {
    const play = await runUcpCommand("play", { sessionType: "editor", timeout: 60_000 });
    expect(play.ok).toBe(true);

    await waitForPlayMode("playing", 60_000);
    const runtimeSession = await waitForRuntimeSession(undefined, 60_000);
    expect(runtimeSession.runtimePort).toBeGreaterThan(0);

    const editorEcho = await runUcpCommand("echo", {
      sessionType: "editor",
      args: { message: "editor-in-play" },
    });
    expect(editorEcho.ok).toBe(true);
    expect((editorEcho.data as Record<string, unknown>)?.channel).toBe("editor");
    expect((editorEcho.data as Record<string, unknown>)?.message).toBe("editor-in-play");

    const runtimeEcho = await runUcpCommand("echo", {
      sessionType: "runtime",
      target: LOCAL_RUNTIME_TARGET_ID,
      args: { message: "runtime-in-play" },
    });
    expect(runtimeEcho.ok).toBe(true);
    expect((runtimeEcho.data as Record<string, unknown>)?.channel).toBe("runtime");
    expect((runtimeEcho.data as Record<string, unknown>)?.message).toBe("runtime-in-play");

    const stop = await runUcpCommand("stop", { sessionType: "editor", timeout: 60_000 });
    expect(stop.ok).toBe(true);

    await waitForPlayMode("edit", 60_000);
    await waitForRuntimeSessionAbsent(undefined, 60_000);

    const editorAfter = await runUcpCommand("echo", {
      sessionType: "editor",
      args: { message: "editor-after-stop" },
    });
    expect(editorAfter.ok).toBe(true);
    expect((editorAfter.data as Record<string, unknown>)?.channel).toBe("editor");

    const runtimeAfter = await runUcpCommand("echo", {
      sessionType: "runtime",
      args: { message: "runtime-after-stop" },
    });
    expect(runtimeAfter.ok).toBe(false);
    expect(runtimeAfter.error_code).toBe("no_ready_session");
  }, 180_000);
});
