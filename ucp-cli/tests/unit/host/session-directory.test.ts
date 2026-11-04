import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { Logger } from "#host/logger/logger.js";
import { STALE_MS, SessionDirectory, readEditorSession } from "#host/sessions/session-directory.js";
import { sessionFilePath, writeJsonAtomic } from "#shared/index.js";

const logger = new Logger({ level: "error" });

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

  it("is offline when late and written by an agent that records no pid", () => {
    expect(read({ updatedAt: stale }).status).toBe("offline");
  });

  it("is offline when Unity said so on quit", () => {
    expect(read({ pid: process.pid, status: "offline" }).status).toBe("offline");
  });
});
