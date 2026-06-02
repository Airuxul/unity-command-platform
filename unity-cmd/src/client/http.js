import { withRetry, isTransientError } from './connection.js';
import { DEFAULT_TIMEOUT_MS } from '../constants.js';

function buildHeaders(body) {
  const headers = {};
  if (body) headers['Content-Type'] = 'application/json';
  const token = process.env.UNITY_CMD_TOKEN;
  if (token) headers['X-Unity-Cmd-Token'] = token;
  return Object.keys(headers).length > 0 ? headers : undefined;
}

export async function requestJson(url, options = {}) {
  const { method = 'GET', body, timeoutMs = DEFAULT_TIMEOUT_MS, retryOnDisconnect = false } = options;

  const doRequest = async (remainingMs) => {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), remainingMs);
    try {
      const res = await fetch(url, {
        method,
        headers: buildHeaders(body),
        body: body ? JSON.stringify(body) : undefined,
        signal: controller.signal,
      });
      const text = await res.text();
      let data = null;
      if (text) {
        try {
          data = JSON.parse(text);
        } catch {
          data = { raw: text };
        }
      }
      return { status: res.status, data };
    } finally {
      clearTimeout(timer);
    }
  };

  if (retryOnDisconnect) {
    return withRetry((remaining) => doRequest(Math.min(timeoutMs, remaining)), {
      timeoutMs,
      intervalMs: 400,
    });
  }

  try {
    return await doRequest(timeoutMs);
  } catch (err) {
    if (isTransientError(err)) {
      const e = new Error(err.message ?? String(err));
      e.cause = err;
      e.code = 'CONNECTION_FAILED';
      throw e;
    }
    throw err;
  }
}
