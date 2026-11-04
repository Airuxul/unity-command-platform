import { afterEach, describe, expect, it, vi } from "vitest";
import { CapabilityRegistry } from "#host/capability/capability-registry.js";
import { Logger } from "#host/logger/logger.js";
import { FINISHED_RETENTION_MS, QueueService } from "#host/queue/queue-service.js";
import { TransportRouter } from "#host/router/transport-router.js";
import { SchedulerService } from "#host/scheduler/scheduler-service.js";
import { SessionService } from "#host/session/session-service.js";

describe("host core", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("QueueService drops finished commands after the retention window", () => {
    vi.useFakeTimers();
    const queue = new QueueService(new Logger({ level: "error" }));
    queue.enqueue({ id: "old", type: "ping", timeout: 1000 }, "s1");
    queue.updateStatus("old", "completed");
    queue.enqueue({ id: "running", type: "ping", timeout: 1000 }, "s1");

    vi.advanceTimersByTime(FINISHED_RETENTION_MS + 1);
    queue.enqueue({ id: "new", type: "ping", timeout: 1000 }, "s1");

    expect(queue.get("old")).toBeNull();
    expect(queue.get("running")).not.toBeNull();
    expect(queue.get("new")).not.toBeNull();
  });

  it("SchedulerService marks a timed-out command and never dispatches it again", async () => {
    const logger = new Logger({ level: "error" });
    const queue = new QueueService(logger);
    const sessions = new SessionService(logger);
    const session = {
      id: "s1",
      name: "demo",
      path: "/p",
      type: "editor" as const,
      status: "ready" as const,
      capabilities: ["ping"],
      updatedAt: new Date().toISOString(),
    };
    sessions.upsert(session);

    let dispatches = 0;
    const never = { dispatch: () => new Promise<never>(() => {}) };
    const router = new TransportRouter({
      capabilities: new CapabilityRegistry(),
      editorAdapter: {
        dispatch: () => {
          dispatches += 1;
          return new Promise<never>(() => {});
        },
      },
      runtimeAdapter: never,
      logger,
    } as unknown as ConstructorParameters<typeof TransportRouter>[0]);
    const scheduler = new SchedulerService({ queue, router, sessions, logger });

    queue.enqueue({ id: "c1", type: "ping", timeout: 100 }, "s1");
    scheduler.start();
    // Long enough for the old retry path (500ms backoff) to have re-dispatched.
    await new Promise((r) => setTimeout(r, 1_200));
    scheduler.stop();

    expect(queue.get("c1")?.status).toBe("timeout");
    expect(dispatches).toBe(1);
  });

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
