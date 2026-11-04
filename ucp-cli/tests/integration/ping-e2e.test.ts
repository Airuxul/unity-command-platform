import { spawn } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";
import {
  ensureUcpHost,
  fetchUcpHostHealth,
  hostFetch,
  waitForCommand,
} from "#cli/client/ucp-host-client.js";
import {
  OUTBOX_POLL_INTERVAL_MS,
  queueLayout,
  readJsonFile,
  resolveHostBin,
  sessionFilePath,
  writeJsonAtomic,
} from "#shared/index.js";

function startHost(ucpRoot: string, port: number) {
  return spawn(process.execPath, [resolveHostBin()], {
    env: { ...process.env, UCP_ROOT: ucpRoot, UCP_HOST_PORT: String(port), UCP_LOG_LEVEL: "error" },
    stdio: "ignore",
  });
}

async function waitHealth(port: number) {
  const deadline = Date.now() + 10_000;
  while (Date.now() < deadline) {
    const health = await fetchUcpHostHealth(port);
    if (health?.ok) return health;
    await new Promise((r) => setTimeout(r, 100));
  }
  throw new Error("host not ready");
}

/** Claims each command the way the 0.5 agent does: rename to .claimed, run, delete. */
function startFakeAgent(projectId: string, ucpRoot: string) {
  const layout = queueLayout(projectId, ucpRoot);
  fs.mkdirSync(layout.inbox, { recursive: true });
  fs.mkdirSync(layout.outbox, { recursive: true });

  const heartbeat = () =>
    writeJsonAtomic(sessionFilePath(projectId, ucpRoot), {
      id: projectId,
      name: "TestProject",
      path: process.cwd().replace(/\\/g, "/"),
      type: "editor",
      status: "ready",
      capabilities: ["ping", "build"],
      updatedAt: new Date().toISOString(),
      pid: process.pid,
      focused: true,
    });
  heartbeat();

  const timer = setInterval(() => {
    heartbeat();
    for (const file of fs.readdirSync(layout.inbox).filter((f) => f.endsWith(".json"))) {
      const inbox = path.join(layout.inbox, file);
      const claimed = inbox.replace(/\.json$/, ".claimed");
      try {
        fs.renameSync(inbox, claimed);
      } catch {
        continue;
      }
      const cmd = readJsonFile(claimed) as { id?: string } | null;
      if (cmd?.id) {
        writeJsonAtomic(path.join(layout.outbox, `${cmd.id}.json`), {
          id: cmd.id,
          success: true,
          duration: 1,
          message: "pong",
          data: { echo: "pong" },
        });
      }
      fs.rmSync(claimed, { force: true });
    }
  }, OUTBOX_POLL_INTERVAL_MS);
  return { timer, layout };
}

describe("ping e2e", () => {
  it("completes via file queue and leaves nothing behind", async () => {
    const ucpRoot = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-int-"));
    const port = 16610 + Math.floor(Math.random() * 1000);
    const projectId = "testproject00001";

    const host = startHost(ucpRoot, port);
    const agent = startFakeAgent(projectId, ucpRoot);

    try {
      await waitHealth(port);

      const unauthorized = await fetch(`http://127.0.0.1:${port}/sessions`);
      expect(unauthorized.status).toBe(401);

      const submit = await hostFetch(
        "/commands",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ type: "ping", timeout: 5000, session: `editor-${projectId}` }),
        },
        ucpRoot,
      ).then((r) => r.json());

      expect(submit.ok).toBe(true);
      expect(submit.id).toBeTruthy();

      const final = await waitForCommand(String(submit.id), ucpRoot);
      expect(final.status).toBe("completed");
      expect((final.result as { success: boolean }).success).toBe(true);
      expect((final.result as { message: string }).message).toBe("pong");

      expect(fs.readdirSync(agent.layout.inbox)).toEqual([]);
      expect(fs.readdirSync(agent.layout.outbox)).toEqual([]);
    } finally {
      clearInterval(agent.timer);
      host.kill();
    }
  });

  it("replaces an idle host older than the CLI", async () => {
    const ucpRoot = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-int-"));
    const port = 17610 + Math.floor(Math.random() * 1000);

    const host = startHost(ucpRoot, port);
    let replacement: number | undefined;
    try {
      const before = await waitHealth(port);

      const result = await ensureUcpHost({ port, ucpRoot, build: (before.build ?? 0) + 1 });
      expect(result.spawned).toBe(true);

      const after = await waitHealth(port);
      replacement = after.pid;
      expect(after.pid).not.toBe(before.pid);
      expect(fs.existsSync(path.join(ucpRoot, "host.lock"))).toBe(false);
    } finally {
      host.kill();
      if (replacement) process.kill(replacement);
    }
  });
});
