import fs from "node:fs";
import path from "node:path";
import {
  loadTargets,
  projectIdFromPath,
  readJsonFile,
  sessionFilePath,
  ucpLayout,
} from "#shared/index.js";
import { ucpSessionSchema } from "#shared/schema.js";
import type {
  EditorTarget,
  RuntimeTarget,
  SessionType,
  UcpSession,
  UcpTarget,
  UcpTargetStatus,
} from "#shared/types.js";
import type { Logger } from "../logger/logger.js";

/** The agent writes its session file every 2 s; past this the Editor is not ticking. */
export const STALE_MS = 30_000;
const PROBE_TIMEOUT_MS = 3_000;
const WATCH_DEBOUNCE_MS = 100;
/** A replace takes well under a millisecond; a file gone for longer than this is really gone. */
const UNREADABLE_GRACE_MS = 1_000;
const POLL_INTERVAL_MS = 2_000;

export interface RuntimeHealthResponse {
  ok?: boolean;
  type?: string;
  capabilities?: string[];
  name?: string;
  path?: string;
  product?: string;
}

export interface SessionCriteria {
  sessionId?: string;
  projectPath?: string;
  sessionType?: SessionType;
}

/** Whether a process exists. EPERM means it does, owned by someone else. */
export function isPidAlive(pid: number): boolean {
  try {
    process.kill(pid, 0);
    return true;
  } catch (err) {
    return (err as NodeJS.ErrnoException).code === "EPERM";
  }
}

export function discoverEditorTargetsFromSessions(ucpRoot?: string): EditorTarget[] {
  const { sessionsDir } = ucpLayout(ucpRoot);
  if (!fs.existsSync(sessionsDir)) return [];

  const targets: EditorTarget[] = [];
  for (const file of fs.readdirSync(sessionsDir).filter((f) => f.endsWith(".json"))) {
    if (file.includes("-runtime")) continue;

    const data = readJsonFile(path.join(sessionsDir, file));
    const parsed = ucpSessionSchema.safeParse(data);
    if (!parsed.success || parsed.data.type !== "editor") continue;

    const session = parsed.data;
    const queueId = file.replace(/\.json$/, "");
    targets.push({
      id: `editor-${queueId}`,
      type: "editor",
      name: session.name,
      projectPath: session.path,
      queueId,
    });
  }
  return targets;
}

export function mergeTargets(configured: UcpTarget[], ucpRoot?: string): UcpTarget[] {
  const result: UcpTarget[] = [...configured];
  const configuredQueueIds = new Set(
    configured
      .filter((t): t is EditorTarget => t.type === "editor")
      .map((t) => t.queueId ?? projectIdFromPath(t.projectPath)),
  );

  for (const auto of discoverEditorTargetsFromSessions(ucpRoot)) {
    const queueId = auto.queueId ?? projectIdFromPath(auto.projectPath);
    if (!configuredQueueIds.has(queueId)) {
      result.push(auto);
    }
  }

  return result;
}

export function listConfiguredAndDiscoveredTargets(ucpRoot?: string): UcpTarget[] {
  return mergeTargets(loadTargets(ucpRoot), ucpRoot);
}

/**
 * An editor whose heartbeat is late is `busy` while its process lives (importing, compiling, a
 * modal dialog), and `offline` once it is gone. Agents before 0.5 write no pid, so a late one of
 * theirs can only be called offline.
 */
export function readEditorSession(target: EditorTarget, ucpRoot?: string): UcpSession {
  return tryReadEditorSession(target, ucpRoot) ?? { ...editorBase(target), status: "offline" };
}

function editorBase(target: EditorTarget) {
  return {
    id: target.id,
    name: target.name ?? path.basename(target.projectPath),
    path: target.projectPath,
    type: "editor" as const,
    queueId: target.queueId ?? projectIdFromPath(target.projectPath),
    capabilities: [] as string[],
    updatedAt: new Date().toISOString(),
  };
}

/**
 * Null when the file cannot be read right now. The agent replaces it every 2 s, and a read that
 * lands on the replace finds it missing or cannot open it (about 0.1% of reads on Windows).
 */
function tryReadEditorSession(target: EditorTarget, ucpRoot?: string): UcpSession | null {
  const base = editorBase(target);
  const parsed = ucpSessionSchema.safeParse(readJsonFile(sessionFilePath(base.queueId, ucpRoot)));
  if (!parsed.success) return null;

  const file = parsed.data;
  const updatedAt = Date.parse(file.updatedAt || "");
  const fresh = Number.isFinite(updatedAt) && Date.now() - updatedAt <= STALE_MS;
  const alive = file.pid ? isPidAlive(file.pid) : fresh;
  const status =
    file.status === "offline" || !alive
      ? "offline"
      : fresh && file.status === "ready"
        ? "ready"
        : "busy";

  return {
    ...base,
    name: target.name ?? file.name,
    path: target.projectPath,
    status,
    capabilities: file.capabilities ?? [],
    updatedAt: file.updatedAt,
    pid: file.pid ?? undefined,
    focused: file.focused ?? undefined,
    defaultTimeouts: file.defaultTimeouts ?? undefined,
    readOnlyCommands: file.readOnlyCommands ?? undefined,
  };
}

export async function probeRuntimeTarget(target: RuntimeTarget): Promise<UcpTargetStatus> {
  const now = new Date().toISOString();
  const url = `http://${target.host}:${target.port}/health`;

  try {
    const res = await fetch(url, { signal: AbortSignal.timeout(PROBE_TIMEOUT_MS) });
    const body = (await res.json()) as RuntimeHealthResponse;
    const ready = res.ok && body?.ok === true;

    return {
      target,
      status: ready ? "ready" : "offline",
      reachable: res.ok,
      capabilities: Array.isArray(body?.capabilities) ? body.capabilities : [],
      probedAt: now,
      error: ready ? undefined : String(body?.type ?? "runtime_not_ready"),
    };
  } catch (err) {
    return {
      target,
      status: "offline",
      reachable: false,
      capabilities: [],
      probedAt: now,
      error: err instanceof Error ? err.message : String(err),
    };
  }
}

export async function probeTarget(target: UcpTarget, ucpRoot?: string): Promise<UcpTargetStatus> {
  if (target.type === "runtime") return probeRuntimeTarget(target);

  const session = readEditorSession(target, ucpRoot);
  return {
    target,
    status: session.status,
    reachable: session.status !== "offline",
    capabilities: session.capabilities,
    probedAt: new Date().toISOString(),
    error: session.status === "ready" ? undefined : `editor_${session.status}`,
  };
}

export async function probeAllTargets(ucpRoot?: string): Promise<UcpTargetStatus[]> {
  const targets = listConfiguredAndDiscoveredTargets(ucpRoot);
  return Promise.all(targets.map((target) => probeTarget(target, ucpRoot)));
}

async function readSession(target: UcpTarget, ucpRoot?: string): Promise<UcpSession> {
  if (target.type === "editor") return readEditorSession(target, ucpRoot);

  const status = await probeRuntimeTarget(target);
  return {
    id: target.id,
    name: target.name ?? `${target.host}:${target.port}`,
    path: target.projectPath ?? "",
    type: "runtime",
    status: status.status,
    capabilities: status.capabilities,
    updatedAt: status.probedAt,
    host: target.host,
    runtimePort: target.port,
  };
}

function normalizePath(p: string): string {
  return p.replace(/\\/g, "/").toLowerCase();
}

/**
 * Every editor and runtime the host can reach, rescanned when a session file or targets.json
 * changes (debounced) and every 2 s in case fs.watch misses an event.
 *
 * Editors and runtimes are read apart. An editor is a local file read; a runtime is a /health
 * call that can take PROBE_TIMEOUT_MS when the device does not answer, and an editor turning
 * ready must not wait for that.
 */
export class SessionDirectory {
  #editors = new Map<string, UcpSession>();
  #runtimes = new Map<string, UcpSession>();
  #unreadableSince = new Map<string, number>();
  #listeners = new Set<() => void>();
  #watchers: fs.FSWatcher[] = [];
  #pollTimer: ReturnType<typeof setInterval> | null = null;
  #debounce: ReturnType<typeof setTimeout> | null = null;
  #probing: Promise<void> | null = null;
  #reprobe = false;

  constructor(
    readonly logger: Logger,
    readonly ucpRoot?: string,
  ) {}

  start(): void {
    const { root, sessionsDir } = ucpLayout(this.ucpRoot);
    fs.mkdirSync(sessionsDir, { recursive: true });
    void this.scan();

    const watch = (dir: string, accept: (name: string) => boolean, onChange: () => void) => {
      try {
        const watcher = fs.watch(dir, (_, name) => {
          if (!name || accept(String(name))) this.#debounced(onChange);
        });
        watcher.on("error", () => {});
        this.#watchers.push(watcher);
      } catch {
        this.logger.warn("fs.watch unavailable; relying on the poll", { dir });
      }
    };
    watch(
      sessionsDir,
      (name) => name.endsWith(".json"),
      () => this.#readEditors(),
    );
    // A new or removed runtime target needs a fresh probe round, not the one already running.
    watch(
      root,
      (name) => name === "targets.json",
      () => {
        this.#readEditors();
        void this.#probeRuntimes(true);
      },
    );

    this.#pollTimer = setInterval(() => void this.scan(), POLL_INTERVAL_MS);
  }

  stop(): void {
    for (const watcher of this.#watchers) watcher.close();
    this.#watchers = [];
    if (this.#pollTimer) clearInterval(this.#pollTimer);
    if (this.#debounce) clearTimeout(this.#debounce);
  }

  /** Rereads the editors now; resolves once the runtimes have been probed too. */
  scan(): Promise<void> {
    this.#readEditors();
    return this.#probeRuntimes(false);
  }

  /**
   * An editor whose file cannot be read keeps its last state for UNREADABLE_GRACE_MS: the read
   * most likely landed on the agent replacing the file. Calling it offline would fail a submit
   * with no_ready_session, or a queued command with editor_not_running.
   */
  #readEditors(): void {
    try {
      const now = Date.now();
      const editors = listConfiguredAndDiscoveredTargets(this.ucpRoot).filter(
        (t): t is EditorTarget => t.type === "editor",
      );
      const next = new Map<string, UcpSession>();
      for (const target of editors) {
        const session = tryReadEditorSession(target, this.ucpRoot);
        if (session) {
          next.set(target.id, session);
          this.#unreadableSince.delete(target.id);
        } else {
          next.set(
            target.id,
            this.#lastKnown(target.id, now) ?? readEditorSession(target, this.ucpRoot),
          );
        }
      }
      // Gone from the listing: mid-replace for the old agent, which deletes before it moves.
      for (const id of this.#editors.keys()) {
        if (next.has(id)) continue;
        const kept = this.#lastKnown(id, now);
        if (kept) next.set(id, kept);
      }
      this.#editors = next;
      this.#notify();
    } catch (err) {
      this.logger.warn("editor session scan failed", { error: String(err) });
    }
  }

  /** The editor's last good state, while it has been unreadable for less than the grace. */
  #lastKnown(id: string, now: number): UcpSession | null {
    const previous = this.#editors.get(id);
    if (!previous || previous.status === "offline") return null;
    const since = this.#unreadableSince.get(id) ?? now;
    this.#unreadableSince.set(id, since);
    if (now - since > UNREADABLE_GRACE_MS) {
      this.#unreadableSince.delete(id);
      return null;
    }
    return previous;
  }

  /** Probes every runtime target now, after any round already running. */
  refreshRuntimes(): Promise<void> {
    return this.#probeRuntimes(true);
  }

  /** One round at a time. `force`: run another round after the current one instead of joining it. */
  #probeRuntimes(force: boolean): Promise<void> {
    if (this.#probing) {
      if (force) this.#reprobe = true;
      return this.#probing;
    }
    this.#probing = (async () => {
      try {
        do {
          this.#reprobe = false;
          const runtimes = listConfiguredAndDiscoveredTargets(this.ucpRoot).filter(
            (t): t is RuntimeTarget => t.type === "runtime",
          );
          const sessions = await Promise.all(runtimes.map((t) => readSession(t, this.ucpRoot)));
          this.#runtimes = new Map(sessions.map((s) => [s.id, s]));
          this.#notify();
        } while (this.#reprobe);
      } catch (err) {
        this.logger.warn("runtime probe failed", { error: String(err) });
      } finally {
        this.#probing = null;
      }
    })();
    return this.#probing;
  }

  #notify(): void {
    for (const listener of this.#listeners) listener();
  }

  #debounced(run: () => void): void {
    if (this.#debounce) clearTimeout(this.#debounce);
    this.#debounce = setTimeout(() => {
      this.#debounce = null;
      run();
    }, WATCH_DEBOUNCE_MS);
  }

  /** Called after every scan. Returns the unsubscribe. */
  onChange(listener: () => void): () => void {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  /**
   * Rereads one session now. A runtime has no session file to watch, so between polls its cached
   * state can be up to 2 s old, e.g. still "ready" just after play mode stopped.
   */
  async refresh(id: string): Promise<UcpSession | null> {
    const target = listConfiguredAndDiscoveredTargets(this.ucpRoot).find((t) => t.id === id);
    if (!target) return this.get(id);
    const session = await readSession(target, this.ucpRoot);
    (session.type === "editor" ? this.#editors : this.#runtimes).set(id, session);
    this.#notify();
    return session;
  }

  /** Test hook: replaces what the last scan found. */
  set(sessions: UcpSession[]): void {
    this.#editors = new Map(sessions.filter((s) => s.type === "editor").map((s) => [s.id, s]));
    this.#runtimes = new Map(sessions.filter((s) => s.type !== "editor").map((s) => [s.id, s]));
    this.#notify();
  }

  list(): UcpSession[] {
    return [...this.#editors.values(), ...this.#runtimes.values()];
  }

  get(id: string): UcpSession | null {
    return this.#editors.get(id) ?? this.#runtimes.get(id) ?? null;
  }

  /**
   * Picks the session a submit is meant for among those `accept` lets through. With a project and
   * no session type the editor wins; otherwise the choice must be unambiguous.
   */
  resolve(criteria: SessionCriteria, accept: (s: UcpSession) => boolean): UcpSession | null {
    if (criteria.sessionId) {
      const found = this.get(criteria.sessionId);
      return found && accept(found) ? found : null;
    }

    const candidates = this.list().filter(accept);

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
}
