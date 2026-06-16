import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { EditorFileQueueAdapter } from "#host/adapter/editor-file-queue/editor-file-queue-adapter.js";
import { Logger } from "#host/logger/logger.js";
import { readJsonFile } from "#shared/index.js";

describe("EditorFileQueueAdapter", () => {
  it("roundtrip", async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-fq-"));
    const adapter = new EditorFileQueueAdapter(new Logger({ level: "error" }), root);
    const session = {
      id: "proj1",
      name: "demo",
      path: "/demo",
      type: "editor" as const,
      status: "ready" as const,
      capabilities: ["ping"],
      updatedAt: new Date().toISOString(),
    };
    const command = { id: "cmd-test-1", type: "ping", timeout: 3000, args: {} };

    setTimeout(() => {
      const outbox = path.join(root, "queues", "proj1", "outbox", "cmd-test-1.json");
      fs.mkdirSync(path.dirname(outbox), { recursive: true });
      fs.writeFileSync(
        outbox,
        JSON.stringify({ id: "cmd-test-1", success: true, duration: 2, message: "pong" }),
      );
    }, 200);

    const result = await adapter.dispatch(session, command);
    expect(result.success).toBe(true);
    expect(result.message).toBe("pong");
    const inbox = path.join(root, "queues", "proj1", "inbox", "cmd-test-1.json");
    expect(readJsonFile(inbox)).toBeTruthy();
  });
});
