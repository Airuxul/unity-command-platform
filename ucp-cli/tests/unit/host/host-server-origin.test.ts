import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { CommandRunner } from "#host/commands/command-runner.js";
import { Logger } from "#host/logger/logger.js";
import { UcpHostServer, isLocalClientRequest } from "#host/server/ucp-host-server.js";
import { SessionDirectory } from "#host/sessions/session-directory.js";

const PORT = 46610;
const TOKEN = "test-token";

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

const local = { host: `127.0.0.1:${PORT}` };
const authed = { ...local, "x-ucp-token": TOKEN };

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

describe("UcpHostServer", () => {
  let server: UcpHostServer;
  let runner: CommandRunner;
  let root: string;

  beforeAll(async () => {
    root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-origin-"));
    const logger = new Logger({ level: "error" });
    const sessions = new SessionDirectory(logger, root);
    sessions.set([
      {
        id: "editor-1",
        name: "demo",
        path: "C:/Project/Demo",
        type: "editor",
        status: "ready",
        capabilities: ["ping"],
        updatedAt: new Date().toISOString(),
      },
    ]);
    runner = new CommandRunner(
      sessions,
      async (_, command) => {
        await new Promise((r) => setTimeout(r, 200));
        return { id: command.id, success: true, duration: 200, message: "pong" };
      },
      logger,
    );
    server = new UcpHostServer({
      runner,
      sessions,
      logger,
      port: PORT,
      ucpRoot: root,
      build: 7,
      token: TOKEN,
    });
    await server.start();
  });

  afterAll(async () => {
    await server.stop();
    fs.rmSync(root, { recursive: true, force: true });
  });

  it("serves /health without a token, with the build id", async () => {
    const res = await request("GET", "/health", local);
    expect(res.status).toBe(200);
    expect(res.body).toMatchObject({ ok: true, build: 7, busy: 0 });
  });

  it("writes the token and build to host.json", () => {
    const info = JSON.parse(fs.readFileSync(path.join(root, "host.json"), "utf8"));
    expect(info).toMatchObject({ port: PORT, build: 7, token: TOKEN });
  });

  it("refuses a cross-site exec before reading the body", async () => {
    const res = await request(
      "POST",
      "/commands",
      { ...authed, origin: "https://evil.example", "content-type": "text/plain" },
      JSON.stringify({ type: "exec", args: { code: 'System.IO.File.Delete("x");' } }),
    );
    expect(res.status).toBe(403);
    expect(res.body.error).toBe("forbidden_origin");
  });

  it("refuses a missing or wrong token", async () => {
    expect((await request("GET", "/sessions", local)).status).toBe(401);
    expect((await request("GET", "/sessions", { ...local, "x-ucp-token": "nope" })).status).toBe(
      401,
    );
    const post = await request("POST", "/commands", local, JSON.stringify({ type: "ping" }));
    expect(post.status).toBe(401);
  });

  it("holds GET ?wait= until the command finishes", async () => {
    const submit = await request("POST", "/commands", authed, JSON.stringify({ type: "ping" }));
    expect(submit.status).toBe(202);

    const started = Date.now();
    const res = await request("GET", `/commands/${submit.body.id}?wait=10000`, authed);
    expect(res.body.status).toBe("completed");
    expect(Date.now() - started).toBeLessThan(2_000);
    expect((res.body.result as { message: string }).message).toBe("pong");
    expect(res.body.timing).toBeTruthy();
  });

  it("answers a GET without wait at once", async () => {
    const submit = await request("POST", "/commands", authed, JSON.stringify({ type: "ping" }));
    const res = await request("GET", `/commands/${submit.body.id}`, authed);
    expect(res.body.status).not.toBe("completed");
    await runner.wait(String(submit.body.id), 5_000);
  });
});
