import crypto from "node:crypto";
import fs from "node:fs";
import http from "node:http";
import {
  DEFAULT_HOST_PORT,
  HOST_TOKEN_HEADER,
  LONG_POLL_MAX_MS,
  ucpLayout,
  writeJsonAtomic,
} from "#shared/index.js";
import { submitCommandSchema } from "#shared/schema.js";
import type { QueuedCommand } from "#shared/types.js";
import type { CommandRunner } from "../commands/command-runner.js";
import type { Logger } from "../logger/logger.js";
import { type SessionDirectory, probeAllTargets } from "../sessions/session-directory.js";

function readJsonBody(req: http.IncomingMessage): Promise<unknown> {
  return new Promise((resolve, reject) => {
    const chunks: Buffer[] = [];
    req.on("data", (c) => chunks.push(c));
    req.on("end", () => {
      try {
        const raw = Buffer.concat(chunks).toString("utf8");
        resolve(raw ? JSON.parse(raw) : {});
      } catch (err) {
        reject(err);
      }
    });
    req.on("error", reject);
  });
}

/**
 * Refuses requests a web browser could have sent. Binding to 127.0.0.1 does not stop a page the
 * user has open from POSTing here (a text/plain fetch needs no preflight), and `exec` runs any C#
 * in the Editor. Browsers always send `Origin` on such a POST, and a DNS-rebinding page arrives
 * with its own host name instead of 127.0.0.1. ucp-cli sends neither.
 */
export function isLocalClientRequest(headers: http.IncomingHttpHeaders, port: number): boolean {
  if (headers.origin !== undefined) return false;
  const host = headers.host ?? "";
  return host === `127.0.0.1:${port}` || host === `localhost:${port}`;
}

/** Constant-time, so the token cannot be guessed a character at a time. */
function tokenMatches(sent: string | string[] | undefined, token: string): boolean {
  if (typeof sent !== "string") return false;
  const a = Buffer.from(sent);
  const b = Buffer.from(token);
  return a.length === b.length && crypto.timingSafeEqual(a, b);
}

function sendJson(res: http.ServerResponse, status: number, body: unknown): void {
  if (res.writableEnded || res.destroyed) return;
  const payload = JSON.stringify(body);
  res.writeHead(status, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(payload),
  });
  res.end(payload);
}

function commandBody(entry: QueuedCommand) {
  return {
    ok: true,
    id: entry.command.id,
    status: entry.status,
    result: entry.result ?? null,
    error: entry.error ?? null,
    message: entry.message ?? null,
    hint: entry.hint ?? null,
    timing: entry.timing ?? null,
  };
}

export interface UcpHostServerOptions {
  runner: CommandRunner;
  sessions: SessionDirectory;
  logger: Logger;
  port?: number;
  ucpRoot?: string;
  /** dist/build.json of the code this host runs; see readBuildId. */
  build?: number;
  /** Fixed in tests; random otherwise. */
  token?: string;
  /** Called once POST /shutdown is accepted; the process entry point exits from it. */
  onShutdown?: () => void;
}

export class UcpHostServer {
  readonly runner: CommandRunner;
  readonly sessions: SessionDirectory;
  readonly logger: Logger;
  readonly port: number;
  readonly ucpRoot?: string;
  readonly build: number;
  /**
   * Any local process can reach 127.0.0.1, not only this user's. host.json lives in the user's
   * home directory, so only the user (and their own processes) can read the token from it.
   */
  readonly token: string;
  readonly onShutdown?: () => void;
  server: http.Server | null = null;
  /** Set once a shutdown is accepted: from then on no command is taken, so none can be lost. */
  #closing = false;

  constructor(options: UcpHostServerOptions) {
    this.onShutdown = options.onShutdown;
    this.runner = options.runner;
    this.sessions = options.sessions;
    this.logger = options.logger;
    this.port = options.port ?? DEFAULT_HOST_PORT;
    this.ucpRoot = options.ucpRoot;
    this.build = options.build ?? 0;
    this.token = options.token ?? crypto.randomBytes(24).toString("hex");
  }

  start(): Promise<void> {
    this.server = http.createServer((req, res) => {
      this.#handle(req, res).catch((err) => {
        this.logger.error("request error", { error: String(err) });
        sendJson(res, 500, { ok: false, error: String(err) });
      });
    });
    // Long polls hold a request for LONG_POLL_MAX_MS; keep Node from cutting them off first.
    this.server.requestTimeout = 0;
    this.server.headersTimeout = 60_000;

    return new Promise((resolve, reject) => {
      this.server?.listen(this.port, "127.0.0.1", () => {
        const layout = ucpLayout(this.ucpRoot);
        fs.mkdirSync(layout.root, { recursive: true });
        writeJsonAtomic(layout.hostJson, {
          pid: process.pid,
          port: this.port,
          build: this.build,
          token: this.token,
          startedAt: new Date().toISOString(),
        });
        this.logger.info("listening", { port: this.port, build: this.build });
        resolve();
      });
      this.server?.on("error", reject);
    });
  }

  async stop(): Promise<void> {
    return new Promise((resolve) => {
      this.server?.closeAllConnections?.();
      this.server?.close(() => resolve());
    });
  }

  async #handle(req: http.IncomingMessage, res: http.ServerResponse): Promise<void> {
    const url = new URL(req.url ?? "/", `http://127.0.0.1:${this.port}`);
    const method = req.method ?? "GET";

    if (!isLocalClientRequest(req.headers, this.port)) {
      this.logger.warn("rejected non-local request", {
        origin: req.headers.origin,
        host: req.headers.host,
      });
      sendJson(res, 403, { ok: false, error: "forbidden_origin" });
      return;
    }

    // Open, so a CLI can tell a stale host from a live one before it has read host.json.
    if (method === "GET" && url.pathname === "/health") {
      sendJson(res, 200, {
        ok: true,
        pid: process.pid,
        port: this.port,
        build: this.build,
        busy: this.runner.busyCount(),
      });
      return;
    }

    if (!tokenMatches(req.headers[HOST_TOKEN_HEADER], this.token)) {
      sendJson(res, 401, {
        ok: false,
        error: "unauthorized",
        message: `Missing or wrong ${HOST_TOKEN_HEADER} header.`,
        hint: "ucp-cli older than 0.3 does not send it: update every ucp-cli on this machine. Other clients read the token from ~/.ucp/host.json.",
      });
      return;
    }

    // Answered from the single request loop, so no submit can slip in between the idle check
    // and the host going away.
    if (method === "POST" && url.pathname === "/shutdown") {
      const busy = this.runner.busyCount();
      if (busy > 0) {
        sendJson(res, 409, {
          ok: false,
          error: "busy",
          message: `${busy} command(s) still in flight.`,
        });
        return;
      }
      this.#closing = true;
      sendJson(res, 200, { ok: true });
      this.logger.info("shutting down on request");
      this.onShutdown?.();
      return;
    }

    if (method === "GET" && url.pathname === "/sessions") {
      sendJson(res, 200, { ok: true, sessions: this.sessions.list() });
      return;
    }

    if (method === "GET" && url.pathname === "/targets") {
      const targets = await probeAllTargets(this.ucpRoot);
      sendJson(res, 200, { ok: true, targets });
      return;
    }

    if (method === "GET" && url.pathname.startsWith("/commands/")) {
      const id = decodeURIComponent(url.pathname.slice("/commands/".length));
      const requested = Number.parseInt(url.searchParams.get("wait") ?? "0", 10);
      const waitMs = Number.isFinite(requested)
        ? Math.min(Math.max(requested, 0), LONG_POLL_MAX_MS)
        : 0;

      const entry = await this.runner.wait(id, waitMs, (detach) => res.on("close", detach));
      if (!entry) {
        sendJson(res, 404, { ok: false, error: "not_found" });
        return;
      }
      sendJson(res, 200, commandBody(entry));
      return;
    }

    if (method === "POST" && url.pathname === "/commands") {
      if (this.#closing) {
        sendJson(res, 503, {
          ok: false,
          error: "host_restarting",
          message: "This ucp-host is shutting down to make way for a newer one.",
          hint: "Send the command again; ucp-cli starts the new host.",
        });
        return;
      }
      const raw = await readJsonBody(req);
      const parsed = submitCommandSchema.safeParse(raw);
      if (!parsed.success) {
        sendJson(res, 400, { ok: false, error: "invalid_body", details: parsed.error.flatten() });
        return;
      }

      const outcome = await this.runner.submit(parsed.data);
      if (!outcome.ok) {
        sendJson(res, 503, {
          ok: false,
          error: outcome.error,
          message: outcome.message,
          hint: outcome.hint,
        });
        return;
      }
      sendJson(res, 202, {
        ok: true,
        id: outcome.entry.command.id,
        status: outcome.entry.status,
        timeout: outcome.entry.command.timeout,
      });
      return;
    }

    sendJson(res, 404, { ok: false, error: "not_found" });
  }
}
