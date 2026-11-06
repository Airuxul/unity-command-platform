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
import { HostDispatchError, HostError } from "../errors.js";
import type { Logger } from "../logger/logger.js";
import type { SessionCriteria, SessionDirectory } from "../sessions/session-directory.js";
import { CommandChainError, expandCommandChain } from "./command-chain.js";

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
  | { ok: false; error: string; message: string; hint: string };

/** A failure the host decides itself; the agent's own failures arrive in the result. */
interface Failure {
  error: string;
  message: string;
  hint?: string;
}

interface Entry extends QueuedCommand {
  waiters: Set<() => void>;
  /** Open long polls. */
  attached: number;
  lastSeen: number;
}

/**
 * Accepts commands and runs them one at a time per session, in submission order; different
 * sessions (an editor and its play-mode runtime) run side by side. A command the session lists
 * as read-only skips the line, so `state` answers while a test run holds the editor. A command
 * that finds its editor busy waits for it, but never past its deadline, which counts from
 * submission.
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
    } else if (!session && mayMeanRuntime(criteria, this.sessions)) {
      // Or predate a start: the probe runs every 2 s, Play Mode may have begun since.
      await this.sessions.refreshRuntimes();
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
    if (runsWhileBusy(session, body.type)) {
      // Listed in the session file: it may run while another command holds the lane.
      void this.#run(entry);
    } else if (lane) {
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
        error: HostError.editorNotRunning,
        message: `Unity (pid ${editor.pid}) for ${editor.path} has exited.`,
        hint: "Start it and wait for the agent to compile.",
      };
    }
    return {
      ok: false,
      error: HostError.noReadySession,
      message: sessionType
        ? `No ready ${sessionType} session for this project.`
        : "No Unity Editor with com.air.ucp-agent is open.",
      hint:
        sessionType === "runtime"
          ? "A runtime session exists only in Play Mode: run play first."
          : "Open the project in Unity, or pass --project when several are open.",
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

    if (!supports(session, entry.command.type)) {
      this.#finish(entry, "failed", {
        error: HostError.unknownCommand,
        message: `'${entry.command.type}' is not a command of ${session.name} (${session.type}).`,
        hint: "Run ucp-cli help for its commands. A command added since the last compile appears after ucp-cli run compile.",
      });
      return;
    }

    const startedAt = Date.now();
    entry.status = "running";
    entry.startedAt = startedAt;

    let steps;
    try {
      steps = expandCommandChain(entry.command.type, session.commandChains);
    } catch (err) {
      if (err instanceof CommandChainError) {
        this.#finish(entry, "failed", {
          error: HostError.commandFailed,
          message: err.message,
          hint: "Before and after name other commands. A command cannot appear again inside its own chain.",
        });
        return;
      }
      throw err;
    }

    let main: UcpResult | undefined;
    for (const step of steps) {
      const now = Date.now();
      if (now >= entry.deadline) {
        this.#finish(entry, "timeout", {
          error: HostError.timeout,
          message: `No result within ${Math.round(entry.command.timeout / 1000)} s.`,
          hint: timeoutHint(session),
        });
        return;
      }

      const current = this.sessions.get(entry.sessionId) ?? session;
      if (!supports(current, step.type)) {
        this.#finish(entry, "failed", {
          error: HostError.unknownCommand,
          message: `'${step.type}' is not a command of ${current.name} (${current.type}).`,
          hint: "Run ucp-cli help for its commands. A command added since the last compile appears after ucp-cli run compile.",
        });
        return;
      }

      const remaining = Math.max(1, entry.deadline - now);
      const command: UcpCommand = {
        id: step.role === "main" ? entry.command.id : createCommandId(),
        type: step.type,
        args: step.role === "main" ? entry.command.args : {},
        timeout: remaining,
        deadline: entry.deadline,
      };

      try {
        const result = await withTimeout(remaining, this.dispatch(current, command));
        const reported = { ...result, id: entry.command.id };
        if (!result.success) {
          this.#finish(entry, "completed", {
            result:
              step.role === "main"
                ? reported
                : {
                    ...reported,
                    message: result.message
                      ? `${step.type}: ${result.message}`
                      : `${step.type} failed`,
                  },
          });
          return;
        }
        if (step.role === "main") main = reported;
      } catch (err) {
        if (err instanceof HostDispatchError) {
          this.#finish(entry, "failed", {
            error: err.code,
            message: err.message,
            hint: err.hint,
          });
          this.logger.error("command failed", { id: command.id, error: err.message });
          return;
        }
        const message = err instanceof Error ? err.message : String(err);
        if (message === "timeout") {
          this.#finish(entry, "timeout", {
            error: HostError.timeout,
            message: `No result within ${Math.round(entry.command.timeout / 1000)} s.`,
            hint: timeoutHint(session),
          });
        } else {
          this.#finish(entry, "failed", {
            error: HostError.commandFailed,
            message,
            hint: `ucp-host could not deliver the command to ${session.name} (${session.type}).`,
          });
        }
        this.logger.error("command failed", { id: command.id, error: message });
        return;
      }
    }

    this.#finish(entry, "completed", { result: main });
    this.logger.info("command completed", {
      id: entry.command.id,
      type: entry.command.type,
      ...timing(entry),
    });
  }

  /** The session once it is ready; null after finishing the entry if it never will be in time. */
  async #awaitReady(entry: Entry): Promise<UcpSession | null> {
    for (;;) {
      if (entry.status !== "queued") return null;
      if (Date.now() >= entry.deadline) {
        this.#expire(entry);
        return null;
      }

      const session = this.sessions.get(entry.sessionId);
      if (session?.status === "ready") return session;
      if (!session || session.status === "offline") {
        const editor = session?.type === "editor";
        this.#finish(entry, "failed", {
          error: editor ? HostError.editorNotRunning : HostError.sessionUnavailable,
          message: `${entry.sessionId} went offline before the command was sent.`,
          hint: editor
            ? "Unity quit or crashed; start it and wait for the agent."
            : "Play Mode ended; run play again.",
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

  /** A command whose deadline passed before it could be sent; it never reaches Unity. */
  #expire(entry: Entry): void {
    this.#finish(entry, "timeout", {
      error: HostError.timeout,
      message: `Timed out after ${Math.round(entry.command.timeout / 1000)} s before reaching Unity.`,
      hint: entry.waitedForSession
        ? "The editor stayed busy (importing, compiling or a modal dialog). Pass a longer --timeout."
        : "Commands ahead of it on the same editor were still running. Pass a longer --timeout.",
    });
  }

  #finish(
    entry: Entry,
    status: CommandStatus,
    extra: { result?: UcpResult } & Partial<Failure>,
  ): void {
    entry.status = status;
    entry.result = extra.result;
    entry.error = extra.error;
    entry.message = extra.message;
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
      if (entry.status !== "queued") continue;

      // Behind a long command it would otherwise only notice when its turn came.
      if (now >= entry.deadline) {
        this.#expire(entry);
      } else if (entry.attached === 0 && now - entry.lastSeen > this.detachedCancelMs) {
        this.#finish(entry, "cancelled", {
          error: HostError.callerDisconnected,
          message: "Nobody was waiting for the result any more.",
          hint: "Dropped before it reached Unity; nothing ran.",
        });
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

/** A submit nothing took that a runtime could, once probed: asked for by type, or by its id. */
function mayMeanRuntime(criteria: SessionCriteria, sessions: SessionDirectory): boolean {
  if (criteria.sessionType) return criteria.sessionType === "runtime";
  if (criteria.sessionId) return sessions.get(criteria.sessionId)?.type === "runtime";
  return false;
}

/** True when the session file lists this command in `runsWhileBusy`. It does not mean the command is read-only. */
function runsWhileBusy(session: UcpSession, commandType: string): boolean {
  const type = commandType.toLowerCase();
  return (session.runsWhileBusy ?? []).some((c) => c.toLowerCase() === type);
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
 * the agent noticing the inbox file (once per editor tick, about 10 a second in the background)
 * and writing the outbox.
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

function timeoutHint(session: UcpSession): string {
  if (session.type === "editor" && session.focused === false) {
    return "Unity is in the background, where it ticks about 10 times a second and only imports on focus. Bring it to the front, or pass a longer --timeout.";
  }
  return "Unity did not answer in time: it may be blocked by a modal dialog or a long import, or the command itself is stuck. The command may still finish in Unity.";
}
