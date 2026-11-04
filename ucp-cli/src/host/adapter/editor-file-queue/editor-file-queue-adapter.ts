import fs from "node:fs";
import path from "node:path";
import {
  OUTBOX_POLL_INTERVAL_MS,
  isUcpResult,
  queueLayout,
  readJsonFile,
  writeJsonAtomic,
} from "#shared/index.js";
import type { UcpCommand, UcpResult, UcpSession } from "#shared/types.js";
import type { Logger } from "../../logger/logger.js";

/** Queue files older than this belong to a caller that has given up (or a host that crashed). */
export const STALE_QUEUE_FILE_MS = 60 * 60_000;
const SWEEP_INTERVAL_MS = 60_000;

export class EditorFileQueueAdapter {
  readonly #lastSweep = new Map<string, number>();

  constructor(
    readonly logger: Logger,
    readonly ucpRoot?: string,
  ) {}

  async dispatch(session: UcpSession, command: UcpCommand): Promise<UcpResult> {
    const queueId = session.queueId ?? session.id;
    const layout = queueLayout(queueId, this.ucpRoot);
    fs.mkdirSync(layout.inbox, { recursive: true });
    fs.mkdirSync(layout.outbox, { recursive: true });
    this.#sweepStale(queueId, layout);

    const inboxPath = path.join(layout.inbox, `${command.id}.json`);
    const outboxPath = path.join(layout.outbox, `${command.id}.json`);

    removeFile(outboxPath);
    writeJsonAtomic(inboxPath, command);
    this.logger.debug("editor adapter enqueued", { id: command.id, inbox: inboxPath });

    try {
      return await this.#waitForOutbox(outboxPath, command.timeout);
    } finally {
      // Inbox first: the agent runs any inbox file that has no outbox, so removing the outbox
      // first would let it run the command again. On timeout this also withdraws a command the
      // agent has not picked up yet, so it cannot run after the caller gave up.
      removeFile(inboxPath);
      removeFile(outboxPath);
    }
  }

  #waitForOutbox(outboxPath: string, timeoutMs: number): Promise<UcpResult> {
    const started = Date.now();
    return new Promise((resolve, reject) => {
      const tick = () => {
        const elapsed = Date.now() - started;
        if (elapsed >= timeoutMs) {
          reject(new Error("timeout"));
          return;
        }
        const data = readJsonFile(outboxPath);
        if (data && isUcpResult(data)) {
          resolve(data);
          return;
        }
        setTimeout(tick, OUTBOX_POLL_INTERVAL_MS);
      };
      tick();
    });
  }

  /** Removes results that arrived after their caller timed out, and inbox files nobody claimed. */
  #sweepStale(queueId: string, layout: { inbox: string; outbox: string }): void {
    const now = Date.now();
    if (now - (this.#lastSweep.get(queueId) ?? 0) < SWEEP_INTERVAL_MS) return;
    this.#lastSweep.set(queueId, now);

    for (const dir of [layout.inbox, layout.outbox]) {
      for (const name of fs.readdirSync(dir)) {
        const file = path.join(dir, name);
        try {
          if (now - fs.statSync(file).mtimeMs > STALE_QUEUE_FILE_MS) fs.unlinkSync(file);
        } catch {
          // Already gone, or still being written; the next sweep gets it.
        }
      }
    }
  }
}

function removeFile(file: string): void {
  try {
    fs.unlinkSync(file);
  } catch {
    // Not there.
  }
}
