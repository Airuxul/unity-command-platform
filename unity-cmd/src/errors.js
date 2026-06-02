/** @typedef {'NO_INSTANCE' | 'NO_PROFILE' | 'CONNECTOR_OUTDATED' | 'CONNECTION_FAILED' | 'CATALOG_FETCH_FAILED' | 'SCOPE_MISMATCH' | 'COMMAND_FAILED' | 'DEFERRED_COMMAND_FAILED' | 'COMMAND_STATUS_TIMEOUT' | 'COMMAND_NOT_FOUND' | 'HTTP_TIMEOUT' | 'UNKNOWN'} ErrorCode */

/**
 * @param {string} message
 * @param {ErrorCode} errorCode
 * @param {string} [hint]
 * @param {Record<string, unknown>} [extra]
 */
export function cliError(message, errorCode, hint, extra = {}) {
  return {
    ok: false,
    error: message,
    error_code: errorCode,
    hint: hint ?? null,
    ...extra,
  };
}

/**
 * @param {Record<string, unknown>} res
 * @param {{ command?: string, status?: number }} [context]
 */
export function enrichFailure(res, context = {}) {
  if (res?.ok !== false && res?.ok !== undefined && res.ok) return res;
  if (res?.error_code) return res;

  const error = String(res?.error ?? res?.message ?? 'unknown_error');
  const status = res?.status ?? context.status;

  let error_code = 'COMMAND_FAILED';
  let hint = null;

  if (error.includes('command_status_poll_timeout') || res?.timedOut) {
    error_code = 'COMMAND_STATUS_TIMEOUT';
    hint = 'Increase --timeout or check Unity Console (--profile editor console).';
  } else if (error === 'command_not_found') {
    error_code = 'COMMAND_NOT_FOUND';
    hint =
      'Command status not found. After a domain reload, retry with allow_connection_retry or re-issue the command.';
  } else if (error === 'failed' || error === 'orphaned' || context.deferred) {
    error_code = 'DEFERRED_COMMAND_FAILED';
    hint = 'Run unity-cmd console --type error,warning to inspect Editor errors.';
  } else if (status === 404) {
    error_code = 'COMMAND_FAILED';
    hint = 'Run unity-cmd --profile <name> list --refresh-catalog';
  } else if (status >= 500) {
    hint = 'Check Unity Editor console and try --profile editor refresh --compile true';
  }

  return {
    ...res,
    error,
    error_code,
    hint,
  };
}

/**
 * @param {unknown} err
 * @param {{ command?: string }} [context]
 */
export function enrichThrown(err, context = {}) {
  const message = err instanceof Error ? err.message : String(err);
  let error_code = 'UNKNOWN';
  let hint = null;

  if (err?.name === 'AbortError' || message.includes('aborted')) {
    error_code = 'HTTP_TIMEOUT';
    hint = 'Increase --timeout or UNITY_CMD_TIMEOUT_MS.';
  } else if (message.includes('fetch failed') || message.includes('ECONNREFUSED')) {
    error_code = 'CONNECTION_FAILED';
    hint =
      'Endpoint unreachable. Open Unity, verify profile (unity-cmd profile list), then --profile <name> ping.';
  } else if (message.includes('command catalog')) {
    error_code = 'CATALOG_FETCH_FAILED';
    hint = 'Try --profile <name> ping then list --refresh-catalog';
  }

  return cliError(message, error_code, hint, { command: context.command ?? null });
}
