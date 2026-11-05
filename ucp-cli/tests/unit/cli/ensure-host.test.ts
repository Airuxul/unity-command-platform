import { spawn } from "node:child_process";
import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { ensureUcpHost, executeCommand } from "#cli/client/ucp-host-client.js";

const PORT = 46611;

describe("ensureUcpHost", () => {
  let server: http.Server;

  beforeAll(async () => {
    // Stands in for an older host that is still running a command.
    server = http.createServer((_, res) => {
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ ok: true, pid: 999_999, port: PORT, build: 1, busy: 1 }));
    });
    await new Promise<void>((r) => server.listen(PORT, "127.0.0.1", r));
  });

  afterAll(async () => {
    await new Promise((r) => server.close(r));
  });

  it("leaves an older host alone while it has commands in flight", async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-ensure-"));
    const result = await ensureUcpHost({ port: PORT, ucpRoot: root, build: 2 });
    expect(result).toEqual({ port: PORT, spawned: false });
  });

  it("leaves a newer host alone", async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-ensure-"));
    expect(await ensureUcpHost({ port: PORT, ucpRoot: root, build: 0 })).toEqual({
      port: PORT,
      spawned: false,
    });
  });
});

describe("ensureUcpHost with a host from before /shutdown", () => {
  it("stops it by its pid when it does not know POST /shutdown", async () => {
    const port = PORT + 2;
    // A real process stands in for the old host's pid, so the kill can be observed.
    const old = spawn(process.execPath, ["-e", "setInterval(() => {}, 1000)"], { stdio: "ignore" });
    const exited = new Promise<void>((r) => old.on("exit", () => r()));
    const host = http.createServer((req, res) => {
      res.writeHead(req.url === "/health" ? 200 : 404, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ ok: req.url === "/health", pid: old.pid, port, build: 1, busy: 0 }));
    });
    await new Promise<void>((r) => host.listen(port, "127.0.0.1", r));
    exited.then(() => {
      host.closeAllConnections();
      host.close();
    });

    const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-oldhost-"));
    let replacement: number | undefined;
    try {
      const result = await ensureUcpHost({ port, ucpRoot: root, build: 2 });
      await exited;
      expect(result.spawned).toBe(true);
      replacement = (
        JSON.parse(fs.readFileSync(path.join(root, "host.json"), "utf8")) as { pid: number }
      ).pid;
      expect(replacement).not.toBe(old.pid);
    } finally {
      old.kill();
      if (replacement) process.kill(replacement);
    }
  }, 20_000);
});

describe("executeCommand", () => {
  it("sends a command again when the host could not be reached at all", async () => {
    const port = PORT + 3;
    let submits = 0;
    // Drops the connection on the first submit, as a host exiting right after /health answered.
    const host = http.createServer((req, res) => {
      const reply = (body: unknown) => {
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify(body));
      };
      if (req.url === "/health")
        return reply({ ok: true, pid: process.pid, build: Number.MAX_SAFE_INTEGER, busy: 0 });
      if (req.method === "POST" && req.url === "/commands") {
        submits += 1;
        if (submits === 1) return req.socket.destroy();
        res.writeHead(202, { "Content-Type": "application/json" });
        return res.end(JSON.stringify({ ok: true, id: "cmd-2", status: "queued" }));
      }
      reply({
        ok: true,
        id: "cmd-2",
        status: "completed",
        result: { id: "cmd-2", success: true, duration: 1, message: "pong" },
      });
    });
    await new Promise<void>((r) => host.listen(port, "127.0.0.1", r));

    const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-unreach-"));
    fs.writeFileSync(path.join(root, "host.json"), JSON.stringify({ port, token: "t" }));
    const previous = process.env.UCP_ROOT;
    process.env.UCP_ROOT = root;
    try {
      expect(await executeCommand("ping")).toMatchObject({ ok: true, message: "pong" });
      expect(submits).toBe(2);
    } finally {
      if (previous === undefined) Reflect.deleteProperty(process.env, "UCP_ROOT");
      else process.env.UCP_ROOT = previous;
      host.closeAllConnections();
      await new Promise((r) => host.close(r));
    }
  });

  it("reports host_lost when the host forgets a command it accepted", async () => {
    const port = PORT + 4;
    const host = http.createServer((req, res) => {
      const ok = req.url === "/health" || req.method === "POST";
      res.writeHead(ok ? (req.method === "POST" ? 202 : 200) : 404, {
        "Content-Type": "application/json",
      });
      res.end(
        JSON.stringify(
          req.url === "/health"
            ? { ok: true, pid: process.pid, build: Number.MAX_SAFE_INTEGER, busy: 0 }
            : req.method === "POST"
              ? { ok: true, id: "cmd-3", status: "queued" }
              : { ok: false, error: "not_found" },
        ),
      );
    });
    await new Promise<void>((r) => host.listen(port, "127.0.0.1", r));
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-forgot-"));
    fs.writeFileSync(path.join(root, "host.json"), JSON.stringify({ port, token: "t" }));
    const previous = process.env.UCP_ROOT;
    process.env.UCP_ROOT = root;
    try {
      const result = await executeCommand("ping");
      expect(result).toMatchObject({ ok: false, error_code: "host_lost" });
      expect(result.message).toContain("not_found");
    } finally {
      if (previous === undefined) Reflect.deleteProperty(process.env, "UCP_ROOT");
      else process.env.UCP_ROOT = previous;
      await new Promise((r) => host.close(r));
    }
  });

  it("sends a command again when the host turned it away to make room for a newer one", async () => {
    const port = PORT + 1;
    const submits: number[] = [];
    // Answers the first submit as a host that has agreed to shut down, then like its successor.
    const host = http.createServer((req, res) => {
      const reply = (status: number, body: unknown) => {
        res.writeHead(status, { "Content-Type": "application/json" });
        res.end(JSON.stringify(body));
      };
      if (req.url === "/health")
        return reply(200, { ok: true, pid: process.pid, build: Number.MAX_SAFE_INTEGER, busy: 0 });
      if (req.method === "POST" && req.url === "/commands") {
        submits.push(Date.now());
        return submits.length === 1
          ? reply(503, { ok: false, error: "host_restarting", message: "shutting down" })
          : reply(202, { ok: true, id: "cmd-1", status: "queued" });
      }
      return reply(200, {
        ok: true,
        id: "cmd-1",
        status: "completed",
        result: { id: "cmd-1", success: true, duration: 1, message: "pong" },
      });
    });
    await new Promise<void>((r) => host.listen(port, "127.0.0.1", r));

    const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-resend-"));
    fs.writeFileSync(path.join(root, "host.json"), JSON.stringify({ port, token: "t" }));
    const previous = process.env.UCP_ROOT;
    process.env.UCP_ROOT = root;
    try {
      const result = await executeCommand("ping");
      expect(result).toMatchObject({ ok: true, message: "pong" });
      expect(submits).toHaveLength(2);
    } finally {
      if (previous === undefined) Reflect.deleteProperty(process.env, "UCP_ROOT");
      else process.env.UCP_ROOT = previous;
      await new Promise((r) => host.close(r));
    }
  });
});
