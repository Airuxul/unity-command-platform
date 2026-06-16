import type { Logger } from "../logger/logger.js";
import type { QueueService } from "../queue/queue-service.js";
import type { RetryPolicy } from "../retry/retry-policy.js";
import type { TransportRouter } from "../router/transport-router.js";
import type { SessionService } from "../session/session-service.js";

export interface SchedulerServiceDeps {
  queue: QueueService;
  router: TransportRouter;
  sessions: SessionService;
  retry: RetryPolicy;
  logger: Logger;
}

export class SchedulerService {
  readonly queue: QueueService;
  readonly router: TransportRouter;
  readonly sessions: SessionService;
  readonly retry: RetryPolicy;
  readonly logger: Logger;
  running = false;

  constructor({ queue, router, sessions, retry, logger }: SchedulerServiceDeps) {
    this.queue = queue;
    this.router = router;
    this.sessions = sessions;
    this.retry = retry;
    this.logger = logger;
  }

  start(): void {
    this.running = true;
    void this.#drain();
  }

  stop(): void {
    this.running = false;
  }

  async #drain(): Promise<void> {
    while (this.running) {
      const pending = this.queue.listQueued();
      if (pending.length === 0) {
        await new Promise((r) => setTimeout(r, 50));
        continue;
      }
      const entry = pending[0];
      await this.#execute(entry.command.id);
    }
  }

  async #execute(commandId: string): Promise<void> {
    const entry = this.queue.get(commandId);
    if (!entry || entry.status !== "queued") return;

    const session = this.sessions.get(entry.sessionId);
    if (!session || session.status !== "ready") {
      this.queue.updateStatus(commandId, "failed", { error: "session unavailable" });
      return;
    }

    this.queue.updateStatus(commandId, "dispatching");
    entry.attempts += 1;

    try {
      this.queue.updateStatus(commandId, "running");
      const result = await this.retry.executeWithTimeout(entry.command.timeout, () =>
        this.router.route(session, entry.command),
      );
      this.queue.updateStatus(commandId, "completed", { result });
      this.logger.info("command completed", { id: commandId, type: entry.command.type });
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      if (message === "timeout" && this.retry.shouldRetry(entry.attempts)) {
        this.logger.warn("command timeout, retrying", { id: commandId, attempts: entry.attempts });
        await this.retry.waitBeforeRetry(entry.attempts);
        this.queue.updateStatus(commandId, "queued");
        return;
      }
      const status = message === "timeout" ? "timeout" : "failed";
      this.queue.updateStatus(commandId, status, { error: message });
      this.logger.error("command failed", { id: commandId, error: message });
    }
  }
}
