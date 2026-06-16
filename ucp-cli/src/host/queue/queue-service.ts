import type { CommandStatus, QueuedCommand, UcpCommand, UcpResult } from "#shared/types.js";
import type { Logger } from "../logger/logger.js";

export class QueueService {
  #commands = new Map<string, QueuedCommand>();

  constructor(readonly logger: Logger) {}

  enqueue(command: UcpCommand, sessionId: string): QueuedCommand {
    const entry: QueuedCommand = {
      command,
      status: "queued",
      sessionId,
      createdAt: Date.now(),
      attempts: 0,
    };
    this.#commands.set(command.id, entry);
    this.logger.debug("command queued", { id: command.id, type: command.type });
    return entry;
  }

  get(id: string): QueuedCommand | null {
    return this.#commands.get(id) ?? null;
  }

  listQueued(): QueuedCommand[] {
    return [...this.#commands.values()].filter((c) => c.status === "queued");
  }

  updateStatus(
    id: string,
    status: CommandStatus,
    extra: { result?: UcpResult; error?: string } = {},
  ): QueuedCommand | null {
    const entry = this.#commands.get(id);
    if (!entry) return null;
    entry.status = status;
    if (extra.result) entry.result = extra.result;
    if (extra.error) entry.error = extra.error;
    if (
      status === "completed" ||
      status === "failed" ||
      status === "timeout" ||
      status === "cancelled"
    ) {
      entry.completedAt = Date.now();
    }
    return entry;
  }

  incrementAttempts(id: string): QueuedCommand | null {
    const entry = this.#commands.get(id);
    if (entry) entry.attempts += 1;
    return entry ?? null;
  }
}

export type { QueuedCommand };
