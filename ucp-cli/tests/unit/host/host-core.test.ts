import { describe, expect, it } from "vitest";
import { CapabilityRegistry } from "#host/capability/capability-registry.js";
import { Logger } from "#host/logger/logger.js";
import { QueueService } from "#host/queue/queue-service.js";
import { TransportRouter } from "#host/router/transport-router.js";

describe("host core", () => {
  it("QueueService lifecycle", () => {
    const queue = new QueueService(new Logger({ level: "error" }));
    const cmd = { id: "cmd-1", type: "ping", timeout: 1000, args: {} };
    queue.enqueue(cmd, "session-1");
    expect(queue.get("cmd-1")?.status).toBe("queued");
    queue.updateStatus("cmd-1", "completed", {
      result: { id: "cmd-1", success: true, duration: 1, message: "pong" },
    });
    expect(queue.get("cmd-1")?.status).toBe("completed");
  });

  it("CapabilityRegistry supports command types", () => {
    const reg = new CapabilityRegistry();
    const session = {
      id: "s1",
      name: "demo",
      path: "/p",
      type: "editor" as const,
      status: "ready" as const,
      capabilities: ["ping", "build"],
      updatedAt: new Date().toISOString(),
    };
    expect(reg.supports(session, "ping")).toBe(true);
    expect(reg.supports(session, "unknown")).toBe(false);
  });

  it("TransportRouter rejects unsupported commands", async () => {
    const logger = new Logger({ level: "error" });
    const router = new TransportRouter({
      capabilities: new CapabilityRegistry(),
      editorAdapter: { dispatch: async () => ({ id: "x", success: true, duration: 0 }) },
      runtimeAdapter: { dispatch: async () => ({ id: "x", success: true, duration: 0 }) },
      logger,
    });
    const session = {
      id: "s1",
      name: "demo",
      path: "/p",
      type: "editor" as const,
      status: "ready" as const,
      capabilities: ["ping"],
      updatedAt: new Date().toISOString(),
    };
    await expect(router.route(session, { id: "c1", type: "build", timeout: 1000 })).rejects.toThrow(
      /does not support/,
    );
  });
});
