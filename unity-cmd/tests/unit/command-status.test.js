import { test } from 'node:test';
import assert from 'node:assert/strict';
import { pollCommandStatus } from '../../src/client/command-status.js';

const originalFetch = global.fetch;

test('pollCommandStatus succeeds when command completes', async () => {
  let calls = 0;
  global.fetch = async () => {
    calls++;
    const body = calls < 2 ? { status: 'running' } : { status: 'succeeded', result: { ok: true } };
    return {
      ok: true,
      status: 200,
      text: async () => JSON.stringify(body),
    };
  };

  const res = await pollCommandStatus('http://127.0.0.1:6400', 'cmd1', 5000);
  assert.equal(res.ok, true);
  global.fetch = originalFetch;
});

test('pollCommandStatus times out', async () => {
  global.fetch = async () => ({
    ok: true,
    status: 200,
    text: async () => JSON.stringify({ status: 'running' }),
  });

  const res = await pollCommandStatus('http://127.0.0.1:6400', 'cmd1', 300);
  assert.equal(res.ok, false);
  assert.equal(res.timedOut, true);
  global.fetch = originalFetch;
});
