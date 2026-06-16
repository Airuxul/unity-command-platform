import type { UcpCommand, UcpResult } from "./types.js";

export const DEFAULT_HOST_PORT = 6610;
export const DEFAULT_COMMAND_TIMEOUT_MS = 30_000;
export const HOST_START_TIMEOUT_MS = 15_000;
export const OUTBOX_POLL_INTERVAL_MS = 100;

export function createCommandId(prefix = "cmd"): string {
  const rand = Math.random().toString(36).slice(2, 10);
  return `${prefix}-${Date.now()}-${rand}`;
}

export function isUcpResult(value: unknown): value is UcpResult {
  if (!value || typeof value !== "object") return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v.id === "string" && typeof v.success === "boolean" && typeof v.duration === "number"
  );
}

export type {
  UcpCommand,
  UcpResult,
  UcpSession,
  CommandStatus,
  SessionStatus,
  SessionType,
} from "./types.js";
