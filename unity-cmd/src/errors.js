/** @typedef {'NO_INSTANCE' | 'CONNECTION_FAILED' | 'CATALOG_FETCH_FAILED' | 'COMMAND_FAILED' | 'JOB_FAILED' | 'JOB_TIMEOUT' | 'JOB_NOT_FOUND' | 'HTTP_TIMEOUT' | 'UNKNOWN'} ErrorCode */

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

  if (error.includes('job_poll_timeout') || res?.timedOut) {
    error_code = 'JOB_TIMEOUT';
    hint = 'Increase --timeout or check Unity Console (unity-cmd console).';
  } else if (error === 'job_not_found') {
    error_code = 'JOB_NOT_FOUND';
    hint = 'Domain reload may have cleared the job. Retry the command.';
  } else if (error === 'failed' || error === 'orphaned' || context.job) {
    error_code = 'JOB_FAILED';
    hint = 'Run unity-cmd console --type error,warning to inspect Editor errors.';
  } else if (status === 404) {
    error_code = 'COMMAND_FAILED';
    hint = 'Run unity-cmd list to refresh the command catalog.';
  } else if (status >= 500) {
    hint = 'Check Unity Editor console and try unity-cmd refresh --compile true.';
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
      'Unity Editor HTTP is not reachable. Open the project, wait for compile, then unity-cmd ping.';
  } else if (message.includes('command catalog')) {
    error_code = 'CATALOG_FETCH_FAILED';
    hint = 'Try unity-cmd ping then unity-cmd list --refresh-catalog';
  }

  return cliError(message, error_code, hint, { command: context.command ?? null });
}
