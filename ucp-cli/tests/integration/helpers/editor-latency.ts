import { expect } from "vitest";
import { projectIdFromPath, readJsonFile, sessionFilePath } from "#shared/index.js";
import { ok, run } from "./content-scenario.js";
import { gameDemoProjectPath } from "./editor-commands.js";

/** Shared by the background and foreground tests, so both measure the same way. */

export function sessionFile(): { pid?: number; focused?: boolean } {
  const id = projectIdFromPath(gameDemoProjectPath());
  return (readJsonFile(sessionFilePath(id, process.env.UCP_ROOT)) ?? {}) as {
    pid?: number;
    focused?: boolean;
  };
}

/** Waits for the agent's next heartbeat (every 2 s) to report `focused`. */
export async function waitForFocused(focused: boolean): Promise<void> {
  const deadline = Date.now() + 10_000;
  while (sessionFile().focused !== focused && Date.now() < deadline) {
    await new Promise((r) => setTimeout(r, 250));
  }
  expect(sessionFile().focused).toBe(focused);
}

/** Editor ticks per second, counted by an update callback over 3 s. */
export async function ticksPerSecond(): Promise<number> {
  const key = "ucp.test.ticks";
  await ok("exec", {
    code: `UnityEditor.SessionState.SetInt("${key}", 0);
var end = UnityEditor.EditorApplication.timeSinceStartup + 3;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () => {
    UnityEditor.SessionState.SetInt("${key}", UnityEditor.SessionState.GetInt("${key}", 0) + 1);
    if (UnityEditor.EditorApplication.timeSinceStartup >= end) UnityEditor.EditorApplication.update -= tick;
};
UnityEditor.EditorApplication.update += tick;
return 0;`,
  });
  await new Promise((r) => setTimeout(r, 4_000));
  const ticks = Number(
    (await ok("exec", { code: `return UnityEditor.SessionState.GetInt("${key}", 0);` })).result,
  );
  return ticks / 3;
}

/**
 * `unity_ms` of `count` pings, sorted: from handing the command to Unity to reading its result.
 * `spreadMs` > 0 waits a random time up to it before each, so pings land anywhere in a tick.
 */
export async function pingTimes(count: number, spreadMs = 0): Promise<number[]> {
  const times: number[] = [];
  for (let i = 0; i < count; i++) {
    if (spreadMs > 0) await new Promise((r) => setTimeout(r, Math.random() * spreadMs));
    const result = await run("ping");
    expect(result.ok).toBe(true);
    times.push(result.timing?.unity_ms ?? Number.POSITIVE_INFINITY);
  }
  return times.sort((a, b) => a - b);
}

export function median(sorted: number[]): number {
  return sorted[Math.floor(sorted.length / 2)];
}
