import { spawn } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";
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
    try {
      const res = await fetch(`http://127.0.0.1:${port}/health`);
      if (res.ok) return;
    } catch {
      // retry
    }
    await new Promise((r) => setTimeout(r, 100));
  }
  throw new Error("host not ready");
}

function startFakeAgent(projectId: string, ucpRoot: string) {
  const layout = queueLayout(projectId, ucpRoot);
  fs.mkdirSync(layout.inbox, { recursive: true });
  fs.mkdirSync(layout.outbox, { recursive: true });

  writeJsonAtomic(sessionFilePath(projectId, ucpRoot), {
    id: projectId,
    name: "TestProject",
    path: process.cwd().replace(/\\/g, "/"),
    type: "editor",
    status: "ready",
    capabilities: ["ping", "build"],
    updatedAt: new Date().toISOString(),
  });

  return setInterval(() => {
    const files = fs.readdirSync(layout.inbox).filter((f) => f.endsWith(".json"));
    for (const file of files) {
      const cmd = readJsonFile(path.join(layout.inbox, file)) as { id?: string } | null;
      if (!cmd?.id) continue;
      const out = path.join(layout.outbox, `${cmd.id}.json`);
      if (fs.existsSync(out)) continue;
      writeJsonAtomic(out, {
        id: cmd.id,
        success: true,
        duration: 1,
        message: "pong",
        data: { echo: "pong" },
      });
    }
  }, OUTBOX_POLL_INTERVAL_MS);
}

describe("ping e2e", () => {
  it("completes via file queue", async () => {
    const ucpRoot = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-int-"));
    const port = 16610 + Math.floor(Math.random() * 1000);
    const projectId = "testproject00001";

    const host = startHost(ucpRoot, port);
    const agent = startFakeAgent(projectId, ucpRoot);

    try {
      await waitHealth(port);

      const submit = await fetch(`http://127.0.0.1:${port}/commands`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ type: "ping", timeout: 5000, session: `editor-${projectId}` }),
      }).then((r) => r.json());

      expect(submit.ok).toBe(true);
      expect(submit.id).toBeTruthy();

      const deadline = Date.now() + 8000;
      let final: Record<string, unknown> = {};
      while (Date.now() < deadline) {
        final = await fetch(`http://127.0.0.1:${port}/commands/${submit.id}`).then((r) => r.json());
        if (final.status === "completed") break;
        await new Promise((r) => setTimeout(r, 100));
      }

      expect(final.status).toBe("completed");
      expect((final.result as { success: boolean }).success).toBe(true);
      expect((final.result as { message: string }).message).toBe("pong");
    } finally {
      clearInterval(agent);
      host.kill();
    }
  });
});
