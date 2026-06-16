import fs from "node:fs";
import http from "node:http";
import {
  DEFAULT_COMMAND_TIMEOUT_MS,
  DEFAULT_HOST_PORT,
  createCommandId,
  ucpLayout,
  writeJsonAtomic,
} from "#shared/index.js";
import { submitCommandSchema, ucpSessionSchema } from "#shared/schema.js";
import type { Logger } from "../logger/logger.js";
import type { QueueService } from "../queue/queue-service.js";
import type { SessionService } from "../session/session-service.js";
import { probeAllTargets } from "../targets/target-probe.js";

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

function sendJson(res: http.ServerResponse, status: number, body: unknown): void {
  const payload = JSON.stringify(body);
  res.writeHead(status, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(payload),
  });
  res.end(payload);
}

export interface UcpHostServerOptions {
  queue: QueueService;
  sessions: SessionService;
  logger: Logger;
  port?: number;
  ucpRoot?: string;
}

export class UcpHostServer {
  queue: QueueService;
  sessions: SessionService;
  logger: Logger;
  port: number;
  ucpRoot?: string;
  server: http.Server | null = null;

  constructor({
    queue,
    sessions,
    logger,
    port = DEFAULT_HOST_PORT,
    ucpRoot,
  }: UcpHostServerOptions) {
    this.queue = queue;
    this.sessions = sessions;
    this.logger = logger;
    this.port = port;
    this.ucpRoot = ucpRoot;
  }

  start(): Promise<void> {
    this.server = http.createServer((req, res) => {
      this.#handle(req, res).catch((err) => {
        this.logger.error("request error", { error: String(err) });
        sendJson(res, 500, { ok: false, error: String(err) });
      });
    });

    return new Promise((resolve, reject) => {
      this.server?.listen(this.port, "127.0.0.1", () => {
        const layout = ucpLayout(this.ucpRoot);
        fs.mkdirSync(layout.root, { recursive: true });
        writeJsonAtomic(layout.hostJson, {
          pid: process.pid,
          port: this.port,
          startedAt: new Date().toISOString(),
        });
        this.logger.info("listening", { port: this.port });
        resolve();
      });
      this.server?.on("error", reject);
    });
  }

  async stop(): Promise<void> {
    return new Promise((resolve) => {
      this.server?.close(() => resolve());
    });
  }

  async #handle(req: http.IncomingMessage, res: http.ServerResponse): Promise<void> {
    const url = new URL(req.url ?? "/", `http://127.0.0.1:${this.port}`);
    const method = req.method ?? "GET";

    if (method === "GET" && url.pathname === "/health") {
      sendJson(res, 200, { ok: true, pid: process.pid, port: this.port });
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
      const id = url.pathname.slice("/commands/".length);
      const entry = this.queue.get(id);
      if (!entry) {
        sendJson(res, 404, { ok: false, error: "not_found" });
        return;
      }
      sendJson(res, 200, {
        ok: true,
        id: entry.command.id,
        status: entry.status,
        result: entry.result ?? null,
        error: entry.error ?? null,
      });
      return;
    }

    if (method === "POST" && url.pathname === "/commands") {
      const raw = await readJsonBody(req);
      const parsed = submitCommandSchema.safeParse(raw);
      if (!parsed.success) {
        sendJson(res, 400, { ok: false, error: "invalid_body", details: parsed.error.flatten() });
        return;
      }

      const body = parsed.data;
      const session = this.sessions.resolve({
        sessionId: body.session ?? body.target,
        projectPath: body.project,
        sessionType: body.sessionType,
      });

      if (!session) {
        sendJson(res, 503, {
          ok: false,
          error: "no_ready_session",
          hint: body.sessionType
            ? `No ready ${body.sessionType} session for this project`
            : "Open Unity Editor with com.air.ucp-agent installed",
        });
        return;
      }

      const sessionCheck = ucpSessionSchema.safeParse(session);
      if (!sessionCheck.success) {
        sendJson(res, 503, { ok: false, error: "invalid_session" });
        return;
      }

      const timeout = body.timeout ?? DEFAULT_COMMAND_TIMEOUT_MS;
      const command = {
        id: createCommandId(),
        type: body.type,
        timeout,
        args: body.args ?? {},
      };

      this.queue.enqueue(command, session.id);
      sendJson(res, 202, { ok: true, id: command.id, status: "queued" });
      return;
    }

    sendJson(res, 404, { ok: false, error: "not_found" });
  }
}
