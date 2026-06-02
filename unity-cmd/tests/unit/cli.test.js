import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseArgs } from '../../src/cli.js';
import { DEFAULT_TIMEOUT_MS } from '../../src/constants.js';

test('parseArgs splits positional and flags', () => {
  const { positional, flags, timeoutMs } = parseArgs([
    '--profile',
    'editor',
    'compile',
    '--message',
    'hi',
  ]);
  assert.deepEqual(positional, ['compile']);
  assert.equal(flags.profile, 'editor');
  assert.equal(flags.message, 'hi');
  assert.equal(timeoutMs, DEFAULT_TIMEOUT_MS);
});

test('parseArgs supports --key=value', () => {
  const { flags, timeoutMs } = parseArgs(['--profile=editor', 'ping', '--timeout=30000']);
  assert.equal(flags.profile, 'editor');
  assert.equal(timeoutMs, 30_000);
});

test('parseArgs treats bare --flag as true', () => {
  const { flags, positional } = parseArgs(['list', '--refresh-catalog']);
  assert.equal(flags['refresh-catalog'], true);
  assert.deepEqual(positional, ['list']);
});

test('parseArgs supports --help flag', () => {
  const { flags } = parseArgs(['--help']);
  assert.equal(flags.help, true);
});
