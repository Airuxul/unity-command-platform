import { requestJson } from './http.js';
import { sleep } from './connection.js';

export async function pollCommandStatus(
  baseUrl,
  commandId,
  timeoutMs,
  { allowConnectionRetry = false } = {},
) {
  const deadline = Date.now() + timeoutMs;
  let lastError = null;

  while (Date.now() < deadline) {
    try {
      const { status, data } = await requestJson(`${baseUrl}/commands/${commandId}`, {
        timeoutMs: Math.min(5_000, timeoutMs),
      });

      if (status === 404) {
        return {
          ok: false,
          error: 'command_not_found',
          error_code: 'COMMAND_NOT_FOUND',
          hint: 'Retry after domain reload; command status was lost.',
          data,
        };
      }

      const state = data?.status;
      if (state === 'succeeded') return { ok: true, data };
      if (state === 'failed' || state === 'orphaned') {
        return {
          ok: false,
          error: data?.error ?? state,
          error_code: 'DEFERRED_COMMAND_FAILED',
          hint: 'Run unity-cmd console --type error,warning for details.',
          data,
        };
      }
    } catch (err) {
      lastError = err;
      if (!allowConnectionRetry) throw err;
    }

    await sleep(200);
  }

  return {
    ok: false,
    error: lastError?.message ?? 'command_status_poll_timeout',
    error_code: 'COMMAND_STATUS_TIMEOUT',
    hint: 'Increase --timeout or inspect Unity with unity-cmd console.',
    timedOut: true,
  };
}
