import { spawn, spawnSync } from "node:child_process";
import fs from "node:fs";
import http from "node:http";
import type { AddressInfo } from "node:net";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { Logger } from "#host/logger/logger.js";
import { STALE_MS, SessionDirectory, readEditorSession } from "#host/sessions/session-directory.js";
import { sessionFilePath, writeJsonAtomic } from "#shared/index.js";

const logger = new Logger({ level: "error" });
const REWRITER = path.join(
  path.dirname(fileURLToPath(import.meta.url)),
  "fixtures",
  "rewrite-session.mjs",
);

describe("SessionDirectory.resolve", () => {
  const base = {
    name: "GameDemo",
    path: "C:/Project/GameDemo",
    status: "ready" as const,
    capabilities: ["echo"],
    updatedAt: new Date().toISOString(),
  };
  const any = () => true;

  it("prefers editor session when project has both editor and runtime", () => {
    const sessions = new SessionDirectory(logger);
    sessions.set([
      { ...base, id: "editor-1", type: "editor" },
      { ...base, id: "runtime-1", type: "runtime", runtimePort: 6620 },
    ]);
    expect(sessions.resolve({ projectPath: "C:/Project/GameDemo" }, any)?.id).toBe("editor-1");
  });

  it("resolves runtime session by sessionType", () => {
    const sessions = new SessionDirectory(logger);
    sessions.set([
      { ...base, id: "editor-1", type: "editor" },
      { ...base, id: "runtime-1", type: "runtime", runtimePort: 6620 },
    ]);
    const resolved = sessions.resolve(
      { projectPath: "C:/Project/GameDemo", sessionType: "runtime" },
      any,
    );
    expect(resolved?.id).toBe("runtime-1");
  });
});

describe("SessionDirectory scanning", () => {
  it("sees an editor become ready while a runtime target's probe hangs", async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-scan-"));
    // A device that accepts the connection and never answers, like one behind a dead route.
    const device = http.createServer(() => {});
    await new Promise<void>((r) => device.listen(0, "127.0.0.1", r));
    const port = (device.address() as AddressInfo).port;
    writeJsonAtomic(path.join(root, "targets.json"), {
      targets: [{ id: "device", type: "runtime", host: "127.0.0.1", port }],
    });
    const writeSession = (updatedAt: string) =>
      writeJsonAtomic(sessionFilePath("p1", root), {
        id: "p1",
        name: "P1",
        path: "C:/P1",
        type: "editor",
        status: "ready",
        capabilities: ["ping"],
        updatedAt,
        pid: process.pid,
      });
    writeSession(new Date(Date.now() - STALE_MS - 5_000).toISOString());

    const until = async (status: string) => {
      const started = Date.now();
      while (sessions.get("editor-p1")?.status !== status && Date.now() - started < 10_000) {
        await new Promise((r) => setTimeout(r, 20));
      }
      expect(sessions.get("editor-p1")?.status).toBe(status);
      return Date.now() - started;
    };

    const sessions = new SessionDirectory(logger, root);
    sessions.start();
    try {
      const seen = await until("busy");
      writeSession(new Date().toISOString());
      const ready = await until("ready");
      // The probe gives up after 3 s; neither may wait for it.
      expect(
        seen < 1_000 && ready < 1_000,
        `busy seen after ${seen} ms, ready after ${ready} ms`,
      ).toBe(true);
    } finally {
      sessions.stop();
      device.closeAllConnections();
      device.close();
    }
  }, 20_000);
});

describe("readEditorSession", () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-sessions-"));
  const target = { id: "editor-p1", type: "editor" as const, projectPath: "C:/P1", queueId: "p1" };
  const deadPid = spawnSync(process.execPath, ["-e", ""]).pid;
  const stale = new Date(Date.now() - STALE_MS - 5_000).toISOString();

  function read(file: Record<string, unknown>) {
    writeJsonAtomic(sessionFilePath("p1", root), {
      id: "p1",
      name: "P1",
      path: "C:/P1",
      type: "editor",
      status: "ready",
      capabilities: ["ping"],
      updatedAt: new Date().toISOString(),
      ...file,
    });
    return readEditorSession(target, root);
  }

  it("is ready while the heartbeat is fresh", () => {
    const s = read({ pid: process.pid, focused: false, defaultTimeouts: { compile: 1 } });
    expect(s.status).toBe("ready");
    expect(s.focused).toBe(false);
    expect(s.defaultTimeouts).toEqual({ compile: 1 });
  });

  it("is busy when the heartbeat is late but Unity is still running", () => {
    expect(read({ pid: process.pid, updatedAt: stale }).status).toBe("busy");
  });

  it("is offline once Unity's process is gone, fresh file or not", () => {
    expect(read({ pid: deadPid, updatedAt: stale }).status).toBe("offline");
    expect(read({ pid: deadPid }).status).toBe("offline");
  });

  it("is offline when the session file has no pid", () => {
    expect(read({ updatedAt: stale }).status).toBe("offline");
    expect(read({}).status).toBe("offline");
  });

  it("is offline when Unity said so on quit", () => {
    expect(read({ pid: process.pid, status: "offline" }).status).toBe("offline");
  });

  it("lets an editor go once its session file has been gone for longer than a replace takes", async () => {
    const sessionRoot = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-gone-"));
    const file = sessionFilePath("p1", sessionRoot);
    writeJsonAtomic(file, {
      id: "p1",
      name: "P1",
      path: "C:/P1",
      type: "editor",
      status: "ready",
      capabilities: ["ping"],
      updatedAt: new Date().toISOString(),
      pid: process.pid,
    });
    const sessions = new SessionDirectory(logger, sessionRoot);
    await sessions.scan();
    expect(sessions.get("editor-p1")?.status).toBe("ready");

    fs.rmSync(file);
    await sessions.scan();
    expect(sessions.get("editor-p1")?.status).toBe("ready");
    await new Promise((r) => setTimeout(r, 1_200));
    await sessions.scan();
    await sessions.scan();
    expect(sessions.get("editor-p1")).toBeNull();
  });

  // The agent rewrites its session file every 2 s from another process; here as fast as a child
  // process can, so reads land on the replace itself.
  it.each([
    ["the agent's write (Unity: delete, then move)", "delete-then-move"],
    ["writeJsonAtomic (fake agents, host.json)", "write-json-atomic"],
  ])(
    "never loses an editor while %s replaces its session file",
    async (_, writer) => {
      const sessionRoot = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-rewrite-"));
      const file = sessionFilePath("p1", sessionRoot);
      const body = {
        id: "p1",
        name: "P1",
        path: "C:/P1",
        type: "editor",
        status: "ready",
        capabilities: ["ping"],
        pid: process.pid,
      };
      writeJsonAtomic(file, { ...body, updatedAt: new Date().toISOString() });

      const sessions = new SessionDirectory(logger, sessionRoot);
      await sessions.scan();
      const child = spawn(process.execPath, [REWRITER, writer, file, JSON.stringify(body)], {
        stdio: "ignore",
      });
      const seen: Record<string, number> = {};
      try {
        await new Promise((r) => setTimeout(r, 200));
        const end = Date.now() + 3_000;
        while (Date.now() < end) {
          await sessions.scan();
          const status = sessions.get("editor-p1")?.status ?? "missing";
          seen[status] = (seen[status] ?? 0) + 1;
        }
      } finally {
        child.kill();
      }
      expect(seen.ready).toBeGreaterThan(100);
      expect(Object.keys(seen)).toEqual(["ready"]);
    },
    20_000,
  );
});
