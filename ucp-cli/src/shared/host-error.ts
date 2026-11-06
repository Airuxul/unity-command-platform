/**
 * Failures ucp-host decides before, or instead of, a Unity result.
 * `host_busy`, `host_timeout`, `host_unknown_command` and `host_command_failed`
 * are not the Unity agent's `busy`, `timeout`, `unknown_command` and `command_failed`.
 */
export const HostError = {
  noReadySession: "no_ready_session",
  editorNotRunning: "editor_not_running",
  unknownCommand: "host_unknown_command",
  timeout: "host_timeout",
  commandFailed: "host_command_failed",
  busy: "host_busy",
  hostLost: "host_lost",
  unauthorized: "unauthorized",
  sessionUnavailable: "session_unavailable",
  callerDisconnected: "caller_disconnected",
  unknownSessionType: "unknown_session_type",
  restarting: "host_restarting",
  unreachable: "host_unreachable",
  forbiddenOrigin: "forbidden_origin",
  invalidBody: "invalid_body",
  notFound: "not_found",
} as const;

export type HostErrorCode = (typeof HostError)[keyof typeof HostError];

/** The CLI sends the command again. Nothing was queued. */
export function isRetriableHostError(code: unknown): boolean {
  return code === HostError.restarting || code === HostError.unreachable;
}

/** A dispatch that never reached Unity. The runner records it as failed, not as a Unity result. */
export class HostDispatchError extends Error {
  readonly code: HostErrorCode;
  readonly hint?: string;

  constructor(code: HostErrorCode, message: string, hint?: string) {
    super(message);
    this.name = "HostDispatchError";
    this.code = code;
    this.hint = hint;
  }
}
