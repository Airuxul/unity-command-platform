import { test } from 'node:test';
import assert from 'node:assert/strict';
import { sendCommand } from '../../src/client/command.js';

const originalFetch = global.fetch;

test('sendCommand uses full command timeout for deferred poll, not only POST budget', async () => {
  const started = [];
  global.fetch = async (url, init) => {
    const isPost = init?.method === 'POST';
    started.push({ url: String(url), isPost, at: Date.now() });
    if (isPost) {
      return {
        ok: true,
        status: 202,
        text: async () =>
          JSON.stringify({ ok: true, command_id: 'cmd-deferred', request_id: 'r1' }),
      };
    }
    return {
      ok: true,
      status: 200,
      text: async () => JSON.stringify({ status: 'succeeded', result: { compiled: true } }),
    };
  };

  const res = await sendCommand(
    { host: '127.0.0.1', port: 6547 },
    'compile',
    {},
    { timeoutMs: 20_000, allowConnectionRetry: false },
  );

  assert.equal(res.ok, true);
  const post = started.find((x) => x.isPost);
  const poll = started.find((x) => !x.isPost);
  assert.ok(post);
  assert.ok(poll);
  global.fetch = originalFetch;
});

test('sendCommand retries POST 503 reloading until accepted', async () => {
  let postAttempts = 0;
  global.fetch = async (url, init) => {
    const isPost = init?.method === 'POST';
    if (isPost) {
      postAttempts += 1;
      if (postAttempts < 3) {
        return {
          ok: true,
          status: 503,
          text: async () => JSON.stringify({ ok: false, error: 'reloading' }),
        };
      }
      return {
        ok: true,
        status: 202,
        text: async () =>
          JSON.stringify({ ok: true, command_id: 'cmd-after-reload', request_id: 'r1' }),
      };
    }
    return {
      ok: true,
      status: 200,
      text: async () => JSON.stringify({ status: 'succeeded', result: { compiled: true } }),
    };
  };

  const res = await sendCommand(
    { host: '127.0.0.1', port: 6547 },
    'compile',
    {},
    { timeoutMs: 10_000, allowConnectionRetry: true },
  );

  assert.equal(res.ok, true);
  assert.equal(postAttempts, 3);
  global.fetch = originalFetch;
});
