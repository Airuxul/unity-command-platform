import { spawn } from "node:child_process";
import fs from "node:fs";
import {
  DEFAULT_COMMAND_TIMEOUT_MS,
  DEFAULT_HOST_PORT,
  HOST_START_TIMEOUT_MS,
  OUTBOX_POLL_INTERVAL_MS,
  readJsonFile,
  resolveHostBin,
  ucpLayout,
} from "#shared/index.js";
import type { CliResult, UcpResult } from "#shared/types.js";

export async function fetchUcpHostHealth(
  port = DEFAULT_HOST_PORT,
): Promise<{ ok: boolean } | null> {
  try {
    const res = await fetch(`http://127.0.0.1:${port}/health`, {
      signal: AbortSignal.timeout(2000),
    });
    if (!res.ok) return null;
    return (await res.json()) as { ok: boolean };
  } catch {
    return null;
  }
}

export interface EnsureUcpHostOptions {
  port?: number;
  ucpRoot?: string;
}

export async function ensureUcpHost(
  options: EnsureUcpHostOptions = {},
): Promise<{ port: number; spawned: boolean }> {
  const layout = ucpLayout(options.ucpRoot);
  const hostInfo = readJsonFile(layout.hostJson) as { port?: number } | null;
  const port = options.port ?? hostInfo?.port ?? DEFAULT_HOST_PORT;

  const healthy = await fetchUcpHostHealth(port);
  if (healthy?.ok) return { port, spawned: false };

  if (fs.existsSync(layout.hostLock)) {
    const started = Date.now();
    while (Date.now() - started < HOST_START_TIMEOUT_MS) {
      const h = await fetchUcpHostHealth(port);
      if (h?.ok) return { port, spawned: false };
      await sleep(200);
    }
  }

  fs.mkdirSync(layout.root, { recursive: true });
  fs.writeFileSync(layout.hostLock, String(process.pid), "utf8");

  const hostBin = resolveHostBin();
  const child = spawn(process.execPath, [hostBin], {
    detached: true,
    stdio: "ignore",
    env: { ...process.env, UCP_HOST_PORT: String(port), UCP_ROOT: layout.root },
  });
  child.unref();

  const deadline = Date.now() + HOST_START_TIMEOUT_MS;
  while (Date.now() < deadline) {
    const h = await fetchUcpHostHealth(port);
    if (h?.ok) return { port, spawned: true };
    await sleep(200);
  }

  throw new Error("ucp-host failed to start within timeout");
}

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

export async function submitCommand(
  body: Record<string, unknown>,
  port: number,
): Promise<Record<string, unknown>> {
  const res = await fetch(`http://127.0.0.1:${port}/commands`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  return (await res.json()) as Record<string, unknown>;
}

export async function waitForCommand(
  commandId: string,
  port: number,
  timeoutMs: number,
): Promise<Record<string, unknown>> {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    const res = await fetch(`http://127.0.0.1:${port}/commands/${commandId}`);
    const body = (await res.json()) as Record<string, unknown>;
    if (!body.ok) throw new Error(String(body.error ?? "command_not_found"));
    const terminal = ["completed", "failed", "timeout", "cancelled"];
    if (terminal.includes(String(body.status))) return body;
    await sleep(OUTBOX_POLL_INTERVAL_MS);
  }
  throw new Error("wait_timeout");
}

export interface ExecuteCommandOptions {
  project?: string;
  session?: string;
  target?: string;
  sessionType?: "editor" | "runtime";
  timeout?: number;
  args?: Record<string, unknown>;
}

export async function executeCommand(
  type: string,
  options: ExecuteCommandOptions = {},
): Promise<CliResult> {
  const { port } = await ensureUcpHost({ ucpRoot: process.env.UCP_ROOT });
  const timeout = options.timeout ?? DEFAULT_COMMAND_TIMEOUT_MS;
  const submit = await submitCommand(
    {
      type,
      project: options.project,
      session: options.session,
      target: options.target ?? options.session,
      sessionType: options.sessionType,
      timeout,
      args: options.args ?? {},
    },
    port,
  );

  if (!submit.ok || !submit.id) {
    return {
      ok: false,
      error_code: String(submit.error ?? "submit_failed"),
      hint: submit.hint as string | undefined,
      data: submit,
    };
  }

  const final = await waitForCommand(String(submit.id), port, timeout + 5_000);
  if (final.status === "completed" && final.result) {
    const result = final.result as UcpResult;
    return {
      ok: result.success,
      data: result.data ?? null,
      message: result.message,
      error_code: result.success ? undefined : (result.error ?? "command_failed"),
    };
  }

  return {
    ok: false,
    error_code: String(final.status ?? final.error ?? "command_failed"),
    hint: final.error as string | undefined,
  };
}

export async function fetchUcpHostStatus(port = DEFAULT_HOST_PORT) {
  await ensureUcpHost({ ucpRoot: process.env.UCP_ROOT });
  const [health, sessionsBody, targetsBody] = await Promise.all([
    fetchUcpHostHealth(port),
    fetch(`http://127.0.0.1:${port}/sessions`).then(
      (r) => r.json() as Promise<{ sessions?: unknown[] }>,
    ),
    fetch(`http://127.0.0.1:${port}/targets`).then(
      (r) => r.json() as Promise<{ targets?: unknown[] }>,
    ),
  ]);
  return {
    health,
    sessions: sessionsBody.sessions ?? [],
    targets: targetsBody.targets ?? [],
  };
}
