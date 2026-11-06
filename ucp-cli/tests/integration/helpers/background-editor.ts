import { afterAll, beforeAll } from "vitest";
import { sessionFile } from "./editor-latency.js";
import { isMinimized, minimize, restore } from "./unity-window.js";

/**
 * Command stability is defined with Unity minimized. The editor will run that way, and a focused
 * window only ticks faster, so a command that finishes in the background also finishes in front.
 * Each integration file starts minimized and the previous window state is put back afterwards.
 * foreground.test.ts pulls the window forward itself, and only when UCP_ALLOW_FOCUS=1.
 */
let restoreWindow: (() => void) | undefined;

beforeAll(() => {
  restoreWindow = pinEditorToBackground();
});

afterAll(() => {
  restoreWindow?.();
  restoreWindow = undefined;
});

function pinEditorToBackground(): (() => void) | undefined {
  if (process.platform !== "win32" || process.env.UCP_EDITOR_INTEGRATION !== "1") return;
  const pid = sessionFile().pid ?? 0;
  if (pid <= 0) return;

  try {
    const wasMinimized = isMinimized(pid);
    if (!wasMinimized) minimize(pid);
    return () => {
      if (!wasMinimized) restore(pid);
    };
  } catch {
    // No live window. The test file reports a missing editor on its own.
    return;
  }
}
