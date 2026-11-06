import type { SessionType, UcpSession } from "#shared/types.js";

export interface SessionCriteria {
  sessionId?: string;
  projectPath?: string;
  sessionType?: SessionType;
}

function normalizePath(p: string): string {
  return p.replace(/\\/g, "/").toLowerCase();
}

/**
 * Picks the session a submit is meant for among those `accept` lets through. With a project and
 * no session type the editor wins; otherwise the choice must be unambiguous.
 */
export function resolveSession(
  sessions: readonly UcpSession[],
  criteria: SessionCriteria,
  get: (id: string) => UcpSession | null,
  accept: (session: UcpSession) => boolean,
): UcpSession | null {
  if (criteria.sessionId) {
    const found = get(criteria.sessionId);
    return found && accept(found) ? found : null;
  }

  const candidates = sessions.filter(accept);

  if (criteria.projectPath) {
    const normalized = normalizePath(criteria.projectPath);
    const matches = candidates.filter((s) => normalizePath(s.path) === normalized);
    if (criteria.sessionType) {
      return matches.find((s) => s.type === criteria.sessionType) ?? null;
    }
    const editor = matches.find((s) => s.type === "editor");
    if (editor) return editor;
    return matches.length === 1 ? matches[0] : null;
  }

  if (criteria.sessionType) {
    const typed = candidates.filter((s) => s.type === criteria.sessionType);
    return typed.length === 1 ? typed[0] : null;
  }

  return candidates.length === 1 ? candidates[0] : null;
}
