import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import {
  DEFAULT_HOST_PORT,
  HOST_START_TIMEOUT_MS,
  HOST_TOKEN_HEADER,
  LONG_POLL_MAX_MS,
  TERMINAL_STATUSES,
  readBuildId,
  readJsonFile,
  resolveHostBin,
  ucpLayout,
} from "#shared/index.js";
import type { CliResult, CommandTiming, UcpResult } from "#shared/types.js";

export interface HostHealth {
  ok: boolean;
  pid?: number;
  port?: number;
  build?: number;
  busy?: number;
}

interface HostInfo {
  port: number;
  token?: string;
}

function readHostInfo(ucpRoot = process.env.UCP_ROOT): HostInfo {
  const info = readJsonFile(ucpLayout(ucpRoot).hostJson) as {
    port?: number;
    token?: string;
  } | null;
  return { port: info?.port ?? DEFAULT_HOST_PORT, token: info?.token };
}

/**
 * Every host call but /health: the port and token come from host.json, read each time because a
 * restarted host has a new token.
 */
export function hostFetch(
  pathname: string,
  init: RequestInit = {},
  ucpRoot = process.env.UCP_ROOT,
): Promise<Response> {
  const { port, token } = readHostInfo(ucpRoot);
  const headers = new Headers(init.headers);
  if (token) headers.set(HOST_TOKEN_HEADER, token);
  return fetch(`http://127.0.0.1:${port}${pathname}`, { ...init, headers });
}

export async function fetchUcpHostHealth(port = DEFAULT_HOST_PORT): Promise<HostHealth | null> {
  try {
    const res = await fetch(`http://127.0.0.1:${port}/health`, {
      signal: AbortSignal.timeout(2000),
    });
    if (!res.ok) return null;
    return (await res.json()) as HostHealth;
  } catch {
    return null;
  }
}

export interface EnsureUcpHostOptions {
  port?: number;
  ucpRoot?: string;
  /** This CLI's build id; defaults to dist/build.json. */
  build?: number;
}

/**
 * A host started before the CLI was rebuilt keeps running the old code, so an idle host with an
 * older build id is replaced. Only older: with two installs side by side, neither keeps killing
 * the other's host.
 */
export async function ensureUcpHost(
  options: EnsureUcpHostOptions = {},
): Promise<{ port: number; spawned: boolean }> {
  const layout = ucpLayout(options.ucpRoot);
  const port = options.port ?? readHostInfo(options.ucpRoot).port;

  const health = await fetchUcpHostHealth(port);
  if (health?.ok) {
    const outdated = (health.build ?? 0) < (options.build ?? readBuildId()) && !health.busy;
    if (!outdated || !health.pid) return { port, spawned: false };
    await stopHost(health.pid, port);
  }

  // Another CLI is starting a host right now; a lock older than the start timeout is left over.
  if (!takeLock(layout.hostLock)) {
    const started = Date.now();
    while (Date.now() - started < HOST_START_TIMEOUT_MS) {
      const h = await fetchUcpHostHealth(port);
      if (h?.ok) return { port, spawned: false };
      await sleep(200);
    }
    fs.writeFileSync(layout.hostLock, String(process.pid), "utf8");
  }

  try {
    const child = spawn(process.execPath, [resolveHostBin()], {
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
  } finally {
    fs.rmSync(layout.hostLock, { force: true });
  }

  throw new Error("ucp-host failed to start within timeout");
}

function takeLock(lockPath: string): boolean {
  try {
    fs.mkdirSync(path.dirname(lockPath), { recursive: true });
    if (fs.existsSync(lockPath)) {
      const age = Date.now() - fs.statSync(lockPath).mtimeMs;
      if (age > HOST_START_TIMEOUT_MS) fs.rmSync(lockPath, { force: true });
    }
    fs.writeFileSync(lockPath, String(process.pid), { encoding: "utf8", flag: "wx" });
    return true;
  } catch {
    return false;
  }
}

async function stopHost(pid: number, port: number): Promise<void> {
  try {
    process.kill(pid);
  } catch {
    // Already gone, or another CLI got there first.
  }
  const deadline = Date.now() + 5_000;
  while (Date.now() < deadline && (await fetchUcpHostHealth(port))?.ok) await sleep(100);
}

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

export async function submitCommand(
  body: Record<string, unknown>,
  ucpRoot = process.env.UCP_ROOT,
): Promise<Record<string, unknown>> {
  const res = await hostFetch(
    "/commands",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    },
    ucpRoot,
  );
  return (await res.json()) as Record<string, unknown>;
}

/**
 * Long-polls until the command finishes. The host enforces the command's deadline, so there is
 * no second clock here; each request is held at most LONG_POLL_MAX_MS.
 */
export async function waitForCommand(
  commandId: string,
  ucpRoot = process.env.UCP_ROOT,
): Promise<Record<string, unknown>> {
  for (;;) {
    const res = await hostFetch(
      `/commands/${encodeURIComponent(commandId)}?wait=${LONG_POLL_MAX_MS}`,
      { signal: AbortSignal.timeout(LONG_POLL_MAX_MS + 15_000) },
      ucpRoot,
    );
    const body = (await res.json()) as Record<string, unknown>;
    // 404: the host restarted and forgot the command.
    if (!body.ok) throw new Error(String(body.error ?? "command_not_found"));
    if (TERMINAL_STATUSES.includes(body.status as never)) return body;
  }
}

export interface ExecuteCommandOptions {
  project?: string;
  session?: string;
  target?: string;
  sessionType?: "editor" | "runtime";
  /** Omitted: the command's own default if it declares one, else 30 s. */
  timeout?: number;
  args?: Record<string, unknown>;
}

export async function executeCommand(
  type: string,
  options: ExecuteCommandOptions = {},
): Promise<CliResult> {
  const ucpRoot = process.env.UCP_ROOT;
  await ensureUcpHost({ ucpRoot });
  const submit = await submitCommand(
    {
      type,
      project: options.project,
      session: options.session,
      target: options.target ?? options.session,
      sessionType: options.sessionType,
      timeout: options.timeout,
      args: options.args ?? {},
    },
    ucpRoot,
  );

  if (!submit.ok || !submit.id) {
    return {
      ok: false,
      error_code: String(submit.error ?? "submit_failed"),
      hint: submit.hint as string | undefined,
      data: submit,
    };
  }

  const final = await waitForCommand(String(submit.id), ucpRoot);
  const timing = (final.timing as CommandTiming | null) ?? undefined;
  if (final.status === "completed" && final.result) {
    const result = final.result as UcpResult;
    return {
      ok: result.success,
      data: result.data ?? null,
      message: result.message,
      error_code: result.success ? undefined : (result.error ?? "command_failed"),
      hint: result.hint ?? undefined,
      timing,
    };
  }

  return {
    ok: false,
    error_code: String(final.error ?? final.status ?? "command_failed"),
    hint: (final.hint as string | null) ?? undefined,
    timing,
  };
}

/**
 * Why a command failed is split over three fields: `error_code` says what kind of failure it was,
 * `message` says what went wrong, and `hint` what to try next. The host fills in only
 * `error_code` and `hint` when it could not reach Unity, so reading one field is not enough.
 */
export function describeFailure(result: CliResult): string {
  return [result.message, result.error_code, result.hint].filter(Boolean).join(" | ");
}

export async function fetchUcpHostStatus() {
  const ucpRoot = process.env.UCP_ROOT;
  const { port } = await ensureUcpHost({ ucpRoot });
  const [health, sessionsBody, targetsBody] = await Promise.all([
    fetchUcpHostHealth(port),
    hostFetch("/sessions", {}, ucpRoot).then((r) => r.json() as Promise<{ sessions?: unknown[] }>),
    hostFetch("/targets", {}, ucpRoot).then((r) => r.json() as Promise<{ targets?: unknown[] }>),
  ]);
  return {
    health,
    sessions: sessionsBody.sessions ?? [],
    targets: targetsBody.targets ?? [],
  };
}
