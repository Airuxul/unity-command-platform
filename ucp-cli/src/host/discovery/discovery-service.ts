import fs from "node:fs";
import { ucpLayout } from "#shared/index.js";
import type { Logger } from "../logger/logger.js";
import type { SessionService } from "../session/session-service.js";
import { probeAllTargets, targetStatusToSession } from "../targets/target-probe.js";

export class DiscoveryService {
  watcher: fs.FSWatcher | null = null;
  pollTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    readonly sessions: SessionService,
    readonly logger: Logger,
    readonly ucpRoot?: string,
  ) {}

  start(): void {
    const { root, sessionsDir } = ucpLayout(this.ucpRoot);
    fs.mkdirSync(root, { recursive: true });
    fs.mkdirSync(sessionsDir, { recursive: true });
    void this.scan();

    try {
      this.watcher = fs.watch(root, { recursive: true }, () => {
        void this.scan();
      });
    } catch {
      this.logger.warn("fs.watch unavailable; using poll fallback");
    }

    this.pollTimer = setInterval(() => {
      void this.scan();
    }, 2000);
  }

  stop(): void {
    this.watcher?.close();
    if (this.pollTimer) clearInterval(this.pollTimer);
  }

  async scan(): Promise<void> {
    const statuses = await probeAllTargets(this.ucpRoot);
    const seen = new Set<string>();

    for (const status of statuses) {
      seen.add(status.target.id);
      const session = targetStatusToSession(status);
      if (session) {
        this.sessions.upsert(session);
      } else {
        this.sessions.remove(status.target.id);
      }
    }

    for (const session of this.sessions.list()) {
      if (!seen.has(session.id)) {
        this.sessions.remove(session.id);
      }
    }
  }

  async listStatuses() {
    return probeAllTargets(this.ucpRoot);
  }
}
