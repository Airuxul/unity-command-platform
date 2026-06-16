export type CommandStatus =
  | "created"
  | "queued"
  | "dispatching"
  | "running"
  | "completed"
  | "failed"
  | "cancelled"
  | "timeout";

export type SessionStatus = "offline" | "ready" | "busy";

export type SessionType = "editor" | "runtime";

export interface UcpCommand {
  id: string;
  type: string;
  timeout: number;
  args?: Record<string, unknown>;
}

export interface UcpResult {
  id: string;
  success: boolean;
  duration: number;
  message?: string;
  data?: Record<string, unknown>;
  error?: string;
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
  error?: string;
  createdAt: number;
  completedAt?: number;
  attempts: number;
}

export interface CliResult {
  ok: boolean;
  data?: unknown;
  message?: string;
  error_code?: string;
  hint?: string;
}

export type LogLevel = "debug" | "info" | "warn" | "error";
