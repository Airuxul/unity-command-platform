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

  /** Rejects with Error("timeout") after `command.timeout` ms. */
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
      return await waitForFile(layout.outbox, `${command.id}.json`, command.timeout);
    } finally {
      // The agent claims a command by renaming x.json to x.claimed before running it, so this
      // either withdraws a command it has not picked up yet or finds it already claimed: never
      // both. Inbox first anyway, for agents before 0.5, which run any inbox file without an
      // outbox and would run it again if the outbox went first.
      removeFile(inboxPath);
      removeFile(outboxPath);
    }
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

/** Watches for the result file; the poll backs up fs.watch, which can miss or merge events. */
function waitForFile(dir: string, name: string, timeoutMs: number): Promise<UcpResult> {
  const file = path.join(dir, name);
  return new Promise((resolve, reject) => {
    let watcher: fs.FSWatcher | null = null;
    let settled = false;
    const settle = (fn: () => void) => {
      if (settled) return;
      settled = true;
      clearInterval(poll);
      clearTimeout(timer);
      watcher?.close();
      fn();
    };
    const check = () => {
      const data = readJsonFile(file);
      if (data && isUcpResult(data)) settle(() => resolve(data));
    };

    const poll = setInterval(check, OUTBOX_POLL_INTERVAL_MS);
    const timer = setTimeout(() => settle(() => reject(new Error("timeout"))), timeoutMs);
    try {
      watcher = fs.watch(dir, (_, changed) => {
        if (!changed || String(changed) === name) check();
      });
      watcher.on("error", () => {});
    } catch {
      // Poll only.
    }
    check();
  });
}

function removeFile(file: string): void {
  try {
    fs.unlinkSync(file);
  } catch {
    // Not there.
  }
}
