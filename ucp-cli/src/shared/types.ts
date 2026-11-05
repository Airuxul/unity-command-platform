export type CommandStatus = "queued" | "running" | "completed" | "failed" | "cancelled" | "timeout";

export const TERMINAL_STATUSES: readonly CommandStatus[] = [
  "completed",
  "failed",
  "cancelled",
  "timeout",
];

/** `busy`: an editor whose heartbeat stopped (importing, compiling, a modal dialog). */
export type SessionStatus = "offline" | "ready" | "busy";

export type SessionType = "editor" | "runtime";

export interface UcpCommand {
  id: string;
  type: string;
  /** What is left of the caller's timeout when the command is sent, in ms. */
  timeout: number;
  /**
   * When the host gives up, Unix ms. The agent does not start a command past it, and drops a
   * result that comes in later; agents before 0.5 ignore it.
   */
  deadline?: number;
  args?: Record<string, unknown>;
}

export interface UcpResult {
  id: string;
  success: boolean;
  duration: number;
  message?: string;
  data?: Record<string, unknown>;
  /** A code such as node_not_found; agents before 0.5 put the message text here. */
  error?: string;
  hint?: string | null;
}

export interface UcpSession {
  id: string;
  name: string;
  path: string;
  type: SessionType;
  status: SessionStatus;
  capabilities: string[];
  updatedAt: string;
  host?: string;
  runtimePort?: number;
  queueId?: string;
  /** Editor process id; agents before 0.5 do not write it. */
  pid?: number;
  focused?: boolean;
  /** Commands whose descriptor declares a default timeout, by name and alias. */
  defaultTimeouts?: Record<string, number>;
  /**
   * Commands that change nothing (ping, state, console, ...). They do not wait behind a command
   * still running on the same editor. Agents before 0.5 send none, so everything waits.
   */
  readOnlyCommands?: string[];
}

export interface EditorTarget {
  id: string;
  type: "editor";
  name?: string;
  projectPath: string;
  queueId?: string;
}

export interface RuntimeTarget {
  id: string;
  type: "runtime";
  name?: string;
  host: string;
  port: number;
  projectPath?: string;
}

export type UcpTarget = EditorTarget | RuntimeTarget;

export interface UcpTargetStatus {
  target: UcpTarget;
  status: SessionStatus;
  reachable: boolean;
  capabilities: string[];
  probedAt: string;
  error?: string;
}

export interface QueuedCommand {
  command: UcpCommand;
  status: CommandStatus;
  sessionId: string;
  result?: UcpResult;
  /** Host-level failure code (timeout, unknown_command, ...); the agent's own goes in result. */
  error?: string;
  /** What went wrong, when the host failed the command itself. */
  message?: string;
  hint?: string;
  createdAt: number;
  /** createdAt + timeout: time spent queued counts against the caller's timeout. */
  deadline: number;
  /** When it was handed to Unity. */
  startedAt?: number;
  completedAt?: number;
  waitedForSession?: boolean;
  timing?: CommandTiming;
}

/** Where a command's time went; see CommandRunner. */
export interface CommandTiming {
  total_ms: number;
  queued_ms: number;
  waited_for_session: boolean;
  unity_ms?: number;
  run_ms?: number;
}

export interface CliResult {
  ok: boolean;
  data?: unknown;
  message?: string;
  error_code?: string;
  hint?: string;
  timing?: CommandTiming;
}

export type LogLevel = "debug" | "info" | "warn" | "error";
