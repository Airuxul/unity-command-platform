import { requestJson } from './http.js';
import { sleep } from './target.js';

export async function pollJob(baseUrl, jobId, timeoutMs, { allowConnectionRetry = false } = {}) {
  const deadline = Date.now() + timeoutMs;
  let lastError = null;

  while (Date.now() < deadline) {
    try {
      const { status, data } = await requestJson(`${baseUrl}/jobs/${jobId}`, {
        timeoutMs: Math.min(5_000, timeoutMs),
      });

      if (status === 404) {
        return {
          ok: false,
          error: 'job_not_found',
          error_code: 'JOB_NOT_FOUND',
          hint: 'Retry after domain reload; job state was lost.',
          data,
        };
      }

      const jobStatus = data?.status;
      if (jobStatus === 'succeeded') {
        return { ok: true, data };
      }
      if (jobStatus === 'failed' || jobStatus === 'orphaned') {
        return {
          ok: false,
          error: data?.error ?? jobStatus,
          error_code: 'JOB_FAILED',
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
    error: lastError?.message ?? 'job_poll_timeout',
    error_code: 'JOB_TIMEOUT',
    hint: 'Increase --timeout or inspect Unity with unity-cmd console.',
    timedOut: true,
  };
}
