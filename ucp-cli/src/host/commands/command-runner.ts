import { DEFAULT_COMMAND_TIMEOUT_MS, createCommandId } from "#shared/index.js";
import type { SubmitCommandInput } from "#shared/schema.js";
import type {
  CommandStatus,
  CommandTiming,
  QueuedCommand,
  UcpCommand,
  UcpResult,
  UcpSession,
} from "#shared/types.js";
import { TERMINAL_STATUSES } from "#shared/types.js";
import type { Logger } from "../logger/logger.js";
import type { SessionCriteria, SessionDirectory } from "../sessions/session-directory.js";

/** How long a finished command stays readable by GET /commands/:id. */
export const FINISHED_RETENTION_MS = 10 * 60_000;
/**
 * A queued command nobody has asked about for this long belongs to a caller that went away
 * (Ctrl-C, crash). It is dropped before it reaches Unity. A command already running is not.
 */
export const DETACHED_CANCEL_MS = 10_000;
const SWEEP_INTERVAL_MS = 1_000;
const SESSION_RECHECK_MS = 250;

export type Dispatch = (session: UcpSession, command: UcpCommand) => Promise<UcpResult>;

export type SubmitOutcome =
  | { ok: true; entry: QueuedCommand }
  | { ok: false; error: string; hint: string };

interface Entry extends QueuedCommand {
  waiters: Set<() => void>;
  /** Open long polls. */
  attached: number;
  lastSeen: number;
}

/**
 * Accepts commands and runs them one at a time per session, in submission order; different
 * sessions (an editor and its play-mode runtime) run side by side. A command that finds its
 * editor busy waits for it, but never past its deadline, which counts from submission.
 */
export class CommandRunner {
  #entries = new Map<string, Entry>();
  #lanes = new Map<string, Entry[]>();
  #sweep: ReturnType<typeof setInterval> | null = null;

  constructor(
    readonly sessions: SessionDirectory,
    readonly dispatch: Dispatch,
    readonly logger: Logger,
    readonly detachedCancelMs = DETACHED_CANCEL_MS,
  ) {}

  start(): void {
    this.#sweep = setInterval(() => this.#sweepEntries(Date.now()), SWEEP_INTERVAL_MS);
    this.#sweep.unref?.();
  }

  stop(): void {
    if (this.#sweep) clearInterval(this.#sweep);
  }

  async submit(body: SubmitCommandInput): Promise<SubmitOutcome> {
    const criteria: SessionCriteria = {
      sessionId: body.session ?? body.target,
      projectPath: body.project,
      sessionType: body.sessionType,
    };
    // A busy editor still takes commands: they wait for it. A runtime has no busy state, and an
    // offline session (Unity quitting or gone) takes nothing.
    const accept = (s: UcpSession) => s.status !== "offline";
    let session = this.sessions.resolve(criteria, accept);
    if (session?.type === "runtime") {
      // Its cached state may predate a stop; one local /health call settles it.
      await this.sessions.refresh(session.id);
      session = this.sessions.resolve(criteria, accept);
    }
    if (!session) return this.#rejection(criteria, body.sessionType);

    const now = Date.now();
    const timeout = body.timeout ?? defaultTimeout(session, body.type);
    const entry: Entry = {
      command: { id: createCommandId(), type: body.type, timeout, args: body.args ?? {} },
      status: "queued",
      sessionId: session.id,
      createdAt: now,
      deadline: now + timeout,
      waiters: new Set(),
      attached: 0,
      lastSeen: now,
    };
    this.#entries.set(entry.command.id, entry);

    const lane = this.#lanes.get(session.id);
    if (lane) {
      lane.push(entry);
    } else {
      this.#lanes.set(session.id, [entry]);
      void this.#drain(session.id);
    }
    this.logger.debug("command queued", { id: entry.command.id, type: body.type });
    return { ok: true, entry };
  }

  get(id: string): QueuedCommand | null {
    const entry = this.#entries.get(id);
    if (entry) entry.lastSeen = Date.now();
    return entry ?? null;
  }

  /** Commands not finished yet; the CLI only restarts a host that has none. */
  busyCount(): number {
    let count = 0;
    for (const entry of this.#entries.values()) if (!isTerminal(entry.status)) count++;
    return count;
  }

  /**
   * Resolves once the command finishes or `waitMs` passes, whichever is first; null if unknown.
   * `onDetach` receives the function to call when the caller hangs up early.
   */
  wait(
    id: string,
    waitMs: number,
    onDetach?: (detach: () => void) => void,
  ): Promise<QueuedCommand | null> {
    const entry = this.#entries.get(id);
    if (!entry) return Promise.resolve(null);
    entry.lastSeen = Date.now();
    if (isTerminal(entry.status) || waitMs <= 0) return Promise.resolve(entry);

    return new Promise((resolve) => {
      entry.attached += 1;
      const done = () => {
        clearTimeout(timer);
        if (!entry.waiters.delete(done)) return;
        entry.attached -= 1;
        entry.lastSeen = Date.now();
        resolve(entry);
      };
      const timer = setTimeout(done, waitMs);
      entry.waiters.add(done);
      onDetach?.(done);
    });
  }

  #rejection(criteria: SessionCriteria, sessionType?: string): SubmitOutcome {
    const editor = this.sessions.resolve(criteria, (s) => s.type === "editor");
    if (editor?.pid && sessionType !== "runtime") {
      return {
        ok: false,
        error: "editor_not_running",
        hint: `Unity (pid ${editor.pid}) for ${editor.path} has exited. Start it and wait for the agent.`,
      };
    }
    return {
      ok: false,
      error: "no_ready_session",
      hint: sessionType
        ? `No ready ${sessionType} session for this project`
        : "Open Unity Editor with com.air.ucp-agent installed",
    };
  }

  async #drain(sessionId: string): Promise<void> {
    const lane = this.#lanes.get(sessionId);
    try {
      for (let entry = lane?.shift(); entry; entry = lane?.shift()) {
        await this.#run(entry);
      }
    } finally {
      this.#lanes.delete(sessionId);
    }
  }

  async #run(entry: Entry): Promise<void> {
    if (entry.status !== "queued") return;

    const session = await this.#awaitReady(entry);
    if (!session) return;

    const startedAt = Date.now();
    entry.status = "running";
    entry.startedAt = startedAt;
    // Unity is handed what is left of the caller's timeout, so its own job deadline matches ours.
    const command = { ...entry.command, timeout: Math.max(1, entry.deadline - startedAt) };

    try {
      if (!supports(session, command.type)) {
        throw new Error(`session ${session.id} does not support command type: ${command.type}`);
      }
      // The adapters time out on their own (and withdraw the inbox file); this only guarantees a
      // lane is never stuck behind a dispatch that does not.
      const result = await withTimeout(command.timeout, this.dispatch(session, command));
      this.#finish(entry, "completed", { result });
      this.logger.info("command completed", {
        id: command.id,
        type: command.type,
        ...timing(entry),
      });
    } catch (err) {
      // Never re-dispatched: a timed-out command may still be running in Unity, and a second
      // dispatch would run a non-idempotent command (gameobject_create, asset_delete, exec) twice.
      const message = err instanceof Error ? err.message : String(err);
      if (message === "timeout") {
        this.#finish(entry, "timeout", { error: "timeout", hint: timeoutHint(session, entry) });
      } else {
        this.#finish(entry, "failed", { error: "failed", hint: message });
      }
      this.logger.error("command failed", { id: command.id, error: message });
    }
  }

  /** The session once it is ready; null after finishing the entry if it never will be in time. */
  async #awaitReady(entry: Entry): Promise<UcpSession | null> {
    for (;;) {
      if (entry.status !== "queued") return null;
      if (Date.now() >= entry.deadline) {
        this.#finish(entry, "timeout", {
          error: "timeout",
          hint: "Timed out before reaching Unity: the session was busy or other commands ran first.",
        });
        return null;
      }

      const session = this.sessions.get(entry.sessionId);
      if (session?.status === "ready") return session;
      if (!session || session.status === "offline") {
        this.#finish(entry, "failed", {
          error: session?.type === "editor" ? "editor_not_running" : "session_unavailable",
          hint: `Session ${entry.sessionId} went offline before the command was sent.`,
        });
        return null;
      }

      entry.waitedForSession = true;
      await this.#sessionChange(Math.min(SESSION_RECHECK_MS, entry.deadline - Date.now()));
    }
  }

  #sessionChange(maxMs: number): Promise<void> {
    return new Promise((resolve) => {
      const done = () => {
        clearTimeout(timer);
        unsubscribe();
        resolve();
      };
      const timer = setTimeout(done, Math.max(0, maxMs));
      const unsubscribe = this.sessions.onChange(done);
    });
  }

  #finish(
    entry: Entry,
    status: CommandStatus,
    extra: { result?: UcpResult; error?: string; hint?: string },
  ): void {
    entry.status = status;
    entry.result = extra.result;
    entry.error = extra.error;
    entry.hint = extra.hint;
    entry.completedAt = Date.now();
    entry.timing = timing(entry);
    for (const waiter of [...entry.waiters]) waiter();
  }

  #sweepEntries(now: number): void {
    for (const [id, entry] of this.#entries) {
      if (entry.completedAt !== undefined) {
        if (now - entry.completedAt > FINISHED_RETENTION_MS) this.#entries.delete(id);
        continue;
      }
      if (entry.status === "queued" && entry.attached === 0) {
        if (now - entry.lastSeen > this.detachedCancelMs) {
          this.#finish(entry, "cancelled", {
            error: "caller_disconnected",
            hint: "Nobody was waiting for the result any more; dropped before it reached Unity.",
          });
        }
      }
    }
  }
}

async function withTimeout<T>(ms: number, work: Promise<T>): Promise<T> {
  let timer: ReturnType<typeof setTimeout> | undefined;
  try {
    return await Promise.race([
      work,
      new Promise<never>((_, reject) => {
        timer = setTimeout(() => reject(new Error("timeout")), ms);
      }),
    ]);
  } finally {
    clearTimeout(timer);
  }
}

export function isTerminal(status: CommandStatus): boolean {
  return TERMINAL_STATUSES.includes(status);
}

function supports(session: UcpSession, commandType: string): boolean {
  const type = commandType.toLowerCase();
  return session.capabilities.some((c) => c.toLowerCase() === type);
}

/** The descriptor's own default (compile, play, ...) when the caller did not pass --timeout. */
function defaultTimeout(session: UcpSession, type: string): number {
  const table = session.defaultTimeouts;
  const declared = table?.[type] ?? table?.[type.toLowerCase()];
  return declared && declared > 0 ? declared : DEFAULT_COMMAND_TIMEOUT_MS;
}

/**
 * Where the time went. `queued_ms` covers other commands ahead in the lane and waiting for a
 * busy editor; `unity_ms` is from handing the command over to reading its result, of which
 * `run_ms` is the agent's own measure of the command itself. The rest of `unity_ms` is delivery:
 * the agent noticing the inbox file (it polls every 250 ms) and writing the outbox.
 */
function timing(entry: QueuedCommand): CommandTiming {
  const end = entry.completedAt ?? Date.now();
  const started = entry.startedAt;
  return {
    total_ms: end - entry.createdAt,
    queued_ms: (started ?? end) - entry.createdAt,
    waited_for_session: entry.waitedForSession === true,
    unity_ms: started === undefined ? undefined : end - started,
    run_ms: entry.result?.duration,
  };
}

function timeoutHint(session: UcpSession, entry: QueuedCommand): string {
  const waited = `Waited ${Math.round((Date.now() - entry.createdAt) / 1000)}s of ${Math.round(entry.command.timeout / 1000)}s.`;
  if (session.type === "editor" && session.focused === false) {
    return `${waited} Unity is in the background, where it ticks about 10 times a second and only imports on focus. Bring it to the front, or pass a longer --timeout.`;
  }
  return `${waited} Unity did not answer in time: it may be blocked by a modal dialog or a long import, or the command itself is stuck.`;
}
