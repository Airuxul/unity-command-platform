import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { Logger } from "#host/logger/logger.js";
import { QueueService } from "#host/queue/queue-service.js";
import { UcpHostServer, isLocalClientRequest } from "#host/server/ucp-host-server.js";
import { SessionService } from "#host/session/session-service.js";

const PORT = 46610;

/** Raw request so the test controls Host and Origin exactly, as a browser would send them. */
function request(
  method: string,
  pathname: string,
  headers: Record<string, string>,
  body?: string,
): Promise<{ status: number; body: Record<string, unknown> }> {
  return new Promise((resolve, reject) => {
    const req = http.request(
      { host: "127.0.0.1", port: PORT, method, path: pathname, headers },
      (res) => {
        const chunks: Buffer[] = [];
        res.on("data", (c) => chunks.push(c));
        res.on("end", () =>
          resolve({
            status: res.statusCode ?? 0,
            body: JSON.parse(Buffer.concat(chunks).toString("utf8")),
          }),
        );
      },
    );
    req.on("error", reject);
    if (body) req.write(body);
    req.end();
  });
}

describe("isLocalClientRequest", () => {
  it("accepts ucp-cli's own requests", () => {
    expect(isLocalClientRequest({ host: `127.0.0.1:${PORT}` }, PORT)).toBe(true);
    expect(isLocalClientRequest({ host: `localhost:${PORT}` }, PORT)).toBe(true);
  });

  it("refuses anything a browser could send", () => {
    expect(
      isLocalClientRequest({ host: `127.0.0.1:${PORT}`, origin: "https://evil.example" }, PORT),
    ).toBe(false);
    // `null` is what a sandboxed iframe or a file:// page sends.
    expect(isLocalClientRequest({ host: `127.0.0.1:${PORT}`, origin: "null" }, PORT)).toBe(false);
    // DNS rebinding: same IP, attacker's host name.
    expect(isLocalClientRequest({ host: `evil.example:${PORT}` }, PORT)).toBe(false);
    expect(isLocalClientRequest({}, PORT)).toBe(false);
  });
});

describe("UcpHostServer origin guard", () => {
  let server: UcpHostServer;
  let root: string;

  beforeAll(async () => {
    root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-origin-"));
    const logger = new Logger({ level: "error" });
    server = new UcpHostServer({
      queue: new QueueService(logger),
      sessions: new SessionService(logger),
      logger,
      port: PORT,
      ucpRoot: root,
    });
    await server.start();
  });

  afterAll(async () => {
    await server.stop();
    fs.rmSync(root, { recursive: true, force: true });
  });

  it("serves a local client", async () => {
    const res = await request("GET", "/health", { host: `127.0.0.1:${PORT}` });
    expect(res.status).toBe(200);
    expect(res.body.ok).toBe(true);
  });

  it("refuses a cross-site exec before reading the body", async () => {
    const res = await request(
      "POST",
      "/commands",
      { host: `127.0.0.1:${PORT}`, origin: "https://evil.example", "content-type": "text/plain" },
      JSON.stringify({ type: "exec", args: { code: 'System.IO.File.Delete("x");' } }),
    );
    expect(res.status).toBe(403);
    expect(res.body.error).toBe("forbidden_origin");
  });
});
