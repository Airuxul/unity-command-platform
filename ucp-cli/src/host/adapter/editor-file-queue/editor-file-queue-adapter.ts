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

export class EditorFileQueueAdapter {
  constructor(
    readonly logger: Logger,
    readonly ucpRoot?: string,
  ) {}

  async dispatch(session: UcpSession, command: UcpCommand): Promise<UcpResult> {
    const queueId = session.queueId ?? session.id;
    const layout = queueLayout(queueId, this.ucpRoot);
    fs.mkdirSync(layout.inbox, { recursive: true });
    fs.mkdirSync(layout.outbox, { recursive: true });

    const inboxPath = path.join(layout.inbox, `${command.id}.json`);
    const outboxPath = path.join(layout.outbox, `${command.id}.json`);

    if (fs.existsSync(outboxPath)) {
      fs.unlinkSync(outboxPath);
    }

    writeJsonAtomic(inboxPath, command);
    this.logger.debug("editor adapter enqueued", { id: command.id, inbox: inboxPath });

    return this.#waitForOutbox(outboxPath, command.timeout);
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
}
