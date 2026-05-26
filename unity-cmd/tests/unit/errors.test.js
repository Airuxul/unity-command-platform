import { test } from 'node:test';
import assert from 'node:assert/strict';
import { cliError, enrichFailure, enrichThrown } from '../../src/errors.js';

test('cliError includes code and hint', () => {
  const e = cliError('msg', 'NO_INSTANCE', 'open unity');
  assert.equal(e.ok, false);
  assert.equal(e.error_code, 'NO_INSTANCE');
  assert.equal(e.hint, 'open unity');
});

test('enrichFailure adds job timeout hint', () => {
  const e = enrichFailure({ ok: false, error: 'job_poll_timeout', timedOut: true }, { job: true });
  assert.equal(e.error_code, 'JOB_TIMEOUT');
  assert.match(e.hint, /console/i);
});

test('enrichThrown maps connection refused', () => {
  const e = enrichThrown(new TypeError('fetch failed'), { command: 'ping' });
  assert.equal(e.error_code, 'CONNECTION_FAILED');
  assert.match(e.hint, /ping/i);
});
