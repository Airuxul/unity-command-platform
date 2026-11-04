import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";
import {
  EditorFileQueueAdapter,
  STALE_QUEUE_FILE_MS,
} from "#host/adapter/editor-file-queue/editor-file-queue-adapter.js";
import { Logger } from "#host/logger/logger.js";
import { queueLayout } from "#shared/index.js";

const session = {
  id: "proj1",
  name: "demo",
  path: "/demo",
  type: "editor" as const,
  status: "ready" as const,
  capabilities: ["ping"],
  updatedAt: new Date().toISOString(),
};

function setup() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-fq-"));
  const adapter = new EditorFileQueueAdapter(new Logger({ level: "error" }), root);
  return { root, adapter, layout: queueLayout("proj1", root) };
}

describe("EditorFileQueueAdapter", () => {
  it("returns the outbox result and leaves no queue files behind", async () => {
    const { adapter, layout } = setup();
    const command = { id: "cmd-test-1", type: "ping", timeout: 3000, args: {} };

    setTimeout(() => {
      fs.writeFileSync(
        path.join(layout.outbox, "cmd-test-1.json"),
        JSON.stringify({ id: "cmd-test-1", success: true, duration: 2, message: "pong" }),
      );
    }, 200);

    const result = await adapter.dispatch(session, command);
    expect(result.success).toBe(true);
    expect(result.message).toBe("pong");
    expect(fs.readdirSync(layout.inbox)).toEqual([]);
    expect(fs.readdirSync(layout.outbox)).toEqual([]);
  });

  it("withdraws the inbox file on timeout so the agent cannot run it late", async () => {
    const { adapter, layout } = setup();
    const command = { id: "cmd-test-2", type: "ping", timeout: 300, args: {} };

    await expect(adapter.dispatch(session, command)).rejects.toThrow("timeout");
    expect(fs.readdirSync(layout.inbox)).toEqual([]);
  });

  it("sweeps queue files older than the stale limit, keeps recent ones", async () => {
    const { adapter, layout } = setup();
    fs.mkdirSync(layout.outbox, { recursive: true });
    const old = path.join(layout.outbox, "late-result.json");
    const recent = path.join(layout.outbox, "recent-result.json");
    fs.writeFileSync(old, "{}");
    fs.writeFileSync(recent, "{}");
    const past = (Date.now() - STALE_QUEUE_FILE_MS - 60_000) / 1000;
    fs.utimesSync(old, past, past);

    setTimeout(() => {
      fs.writeFileSync(
        path.join(layout.outbox, "cmd-test-3.json"),
        JSON.stringify({ id: "cmd-test-3", success: true, duration: 1 }),
      );
    }, 100);
    await adapter.dispatch(session, { id: "cmd-test-3", type: "ping", timeout: 3000, args: {} });

    expect(fs.existsSync(old)).toBe(false);
    expect(fs.existsSync(recent)).toBe(true);
  });
});
