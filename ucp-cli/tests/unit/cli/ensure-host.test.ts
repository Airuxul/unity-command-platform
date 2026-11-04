import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { ensureUcpHost } from "#cli/client/ucp-host-client.js";

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
