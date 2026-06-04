import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeProjectPath } from '../../src/client/instance-diagnostics.js';

test('normalizeProjectPath lowercases and normalizes slashes', () => {
  assert.equal(normalizeProjectPath('C:\\Project\\GameDemo\\'), 'c:/project/gamedemo');
});
