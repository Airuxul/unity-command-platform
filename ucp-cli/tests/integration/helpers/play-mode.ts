import { executeCommand, fetchUcpHostStatus } from "#cli/client/ucp-host-client.js";
import { upsertTarget } from "#shared/target-store.js";
import type { CliResult } from "#shared/types.js";
import { gameDemoProjectPath } from "./editor-commands.js";

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function normalizePath(path: string): string {
  return path.replace(/\\/g, "/").toLowerCase();
}

export interface RunUcpCommandOptions {
  sessionType?: "editor" | "runtime";
  args?: Record<string, unknown>;
  timeout?: number;
  project?: string;
}

export async function runUcpCommand(
  type: string,
  options: RunUcpCommandOptions = {},
): Promise<CliResult> {
  return executeCommand(type, {
    project: options.project ?? gameDemoProjectPath(),
    sessionType: options.sessionType,
    timeout: options.timeout ?? 30_000,
    args: options.args ?? {},
  });
}

export const LOCAL_RUNTIME_TARGET_ID = "gamedemo-runtime";

export function ensureLocalRuntimeTarget(project = gameDemoProjectPath(), port = 6620): void {
  upsertTarget(
    {
      id: LOCAL_RUNTIME_TARGET_ID,
      type: "runtime",
      name: "Local Play Runtime",
      host: "127.0.0.1",
      port,
      projectPath: project,
    },
    process.env.UCP_ROOT,
  );
}

async function discoverLocalRuntimePort(host = "127.0.0.1"): Promise<number | null> {
  for (let port = 6620; port <= 6699; port++) {
    try {
      const res = await fetch(`http://${host}:${port}/health`, {
        signal: AbortSignal.timeout(500),
      });
      const body = (await res.json()) as { ok?: boolean };
      if (res.ok && body.ok) return port;
    } catch {
      // try next port
    }
  }
  return null;
}

export async function waitForRuntimeTargetReady(
  project = gameDemoProjectPath(),
  timeoutMs = 60_000,
): Promise<void> {
  const normalized = normalizePath(project);
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const discoveredPort = await discoverLocalRuntimePort();
    if (discoveredPort) {
      ensureLocalRuntimeTarget(project, discoveredPort);
    }

    const status = await fetchUcpHostStatus();
    const targets = (status.targets ?? []) as Array<{
      target?: { type?: string; projectPath?: string; host?: string; port?: number };
      status?: string;
    }>;

    const ready = targets.find((entry) => {
      const target = entry.target;
      if (!target || target.type !== "runtime" || entry.status !== "ready") return false;
      if (target.projectPath && normalizePath(target.projectPath) !== normalized) return false;
      return true;
    });

    if (ready) return;
    await sleep(250);
  }

  throw new Error("Runtime target did not become ready within timeout");
}

export async function waitForRuntimeTargetOffline(
  project = gameDemoProjectPath(),
  timeoutMs = 60_000,
): Promise<void> {
  const normalized = normalizePath(project);
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const status = await fetchUcpHostStatus();
    const targets = (status.targets ?? []) as Array<{
      target?: { type?: string; projectPath?: string };
      status?: string;
    }>;

    const ready = targets.find((entry) => {
      const target = entry.target;
      if (!target || target.type !== "runtime" || entry.status !== "ready") return false;
      if (target.projectPath && normalizePath(target.projectPath) !== normalized) return false;
      return true;
    });

    if (!ready) return;
    await sleep(250);
  }

  throw new Error("Runtime target still ready after timeout");
}

export async function waitForRuntimeSession(
  project = gameDemoProjectPath(),
  timeoutMs = 30_000,
): Promise<{ id: string; runtimePort?: number }> {
  await waitForRuntimeTargetReady(project, timeoutMs);
  return { id: LOCAL_RUNTIME_TARGET_ID, runtimePort: 6620 };
}

export async function waitForRuntimeSessionAbsent(
  project = gameDemoProjectPath(),
  timeoutMs = 30_000,
): Promise<void> {
  await waitForRuntimeTargetOffline(project, timeoutMs);
}

export async function waitForPlayMode(
  expected: "edit" | "playing",
  timeoutMs = 30_000,
  project = gameDemoProjectPath(),
): Promise<void> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const state = await runUcpCommand("state", { sessionType: "editor", project });
    if (!state.ok) {
      await sleep(250);
      continue;
    }

    const data = state.data as Record<string, unknown> | null;
    const playMode = String(data?.play_mode ?? "");
    const isPlaying = data?.is_playing === true;

    if (expected === "playing" && (playMode === "playing" || isPlaying)) return;
    if (expected === "edit" && playMode === "edit" && !isPlaying) return;

    await sleep(250);
  }

  throw new Error(`Play mode did not reach '${expected}' within timeout`);
}

export async function ensureEditMode(project = gameDemoProjectPath()): Promise<void> {
  const state = await runUcpCommand("state", { sessionType: "editor", project });
  const data = state.data as Record<string, unknown> | null;
  if (data?.is_playing === true) {
    const stop = await runUcpCommand("stop", { sessionType: "editor", project, timeout: 60_000 });
    if (!stop.ok) {
      throw new Error(`Failed to stop play mode during setup: ${stop.error_code ?? "unknown"}`);
    }
    await waitForPlayMode("edit", 60_000, project);
    await waitForRuntimeSessionAbsent(project, 60_000);
  }
}
