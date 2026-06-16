import type { SessionType, UcpSession } from "#shared/types.js";
import type { Logger } from "../logger/logger.js";

function normalizePath(path: string): string {
  return path.replace(/\\/g, "/").toLowerCase();
}

export class SessionService {
  #sessions = new Map<string, UcpSession>();

  constructor(readonly logger: Logger) {}

  upsert(session: UcpSession): void {
    this.#sessions.set(session.id, session);
    this.logger.debug("session upsert", { id: session.id, status: session.status });
  }

  remove(id: string): void {
    this.#sessions.delete(id);
  }

  list(): UcpSession[] {
    return [...this.#sessions.values()];
  }

  get(id: string): UcpSession | null {
    return this.#sessions.get(id) ?? null;
  }

  resolve(criteria: {
    sessionId?: string;
    projectPath?: string;
    sessionType?: SessionType;
  }): UcpSession | null {
    const ready = this.list().filter((s) => s.status === "ready");
    if (criteria.sessionId) {
      const found = this.get(criteria.sessionId);
      return found?.status === "ready" ? found : null;
    }

    if (criteria.projectPath) {
      const normalized = normalizePath(criteria.projectPath);
      const matches = ready.filter((s) => normalizePath(s.path) === normalized);
      if (criteria.sessionType) {
        return matches.find((s) => s.type === criteria.sessionType) ?? null;
      }
      const editor = matches.find((s) => s.type === "editor");
      if (editor) return editor;
      if (matches.length === 1) return matches[0];
      return null;
    }

    if (criteria.sessionType) {
      const typed = ready.filter((s) => s.type === criteria.sessionType);
      if (typed.length === 1) return typed[0];
      return null;
    }

    if (ready.length === 1) return ready[0];
    return null;
  }
}
