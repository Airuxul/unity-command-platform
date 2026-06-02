import { requestJson } from './http.js';
import { sleep } from './connection.js';
import { POST_COMMAND_CAP_MS, SEND_COMMAND_RETRY_INTERVAL_MS, CONNECTION_RETRY_MIN_MS } from '../constants.js';

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
      const remainingMs = Math.max(CONNECTION_RETRY_MIN_MS, deadline - Date.now());
      const { status, data } = await requestJson(`${baseUrl}/commands/${commandId}`, {
        timeoutMs: Math.min(POST_COMMAND_CAP_MS, remainingMs),
        retryOnDisconnect: allowConnectionRetry,
      });

      if (status === 404) {
        if (allowConnectionRetry && Date.now() < deadline) {
          await sleep(SEND_COMMAND_RETRY_INTERVAL_MS);
          continue;
        }
        return {
          ok: false,
          error: 'command_not_found',
          error_code: 'COMMAND_NOT_FOUND',
          hint: allowConnectionRetry
            ? 'Command status not found yet (Editor may still be reloading). Retrying.'
            : 'Command status not found. If the Editor domain reloaded, retry with connection retry enabled or re-issue the command.',
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
