import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  resolveTimeoutMs,
  createCommandBudget,
  checkMinConnectorBuild,
} from '../../src/runtime.js';
import {
  DEFAULT_TIMEOUT_MS,
  MIN_CONNECTOR_BUILD,
  COMMAND_BUDGET_FLOOR_MS,
} from '../../src/constants.js';

test('resolveTimeoutMs uses default', () => {
  const prev = process.env.UNITY_CMD_TIMEOUT_MS;
  delete process.env.UNITY_CMD_TIMEOUT_MS;
  assert.equal(resolveTimeoutMs(), DEFAULT_TIMEOUT_MS);
  if (prev) process.env.UNITY_CMD_TIMEOUT_MS = prev;
});

test('resolveTimeoutMs respects override', () => {
  assert.equal(resolveTimeoutMs(5000), 5000);
});

test('createCommandBudget remaining decreases over time', async () => {
  const budget = createCommandBudget(5_000);
  const first = budget.remaining();
  await new Promise((r) => setTimeout(r, 50));
  assert.ok(budget.remaining() < first);
  assert.ok(budget.remaining() >= COMMAND_BUDGET_FLOOR_MS);
});

test('createCommandBudget enforces floor', () => {
  const budget = createCommandBudget(500);
  assert.equal(budget.remaining(), COMMAND_BUDGET_FLOOR_MS);
});

test('checkMinConnectorBuild passes current and newer builds', () => {
  assert.equal(checkMinConnectorBuild(MIN_CONNECTOR_BUILD), null);
  assert.equal(checkMinConnectorBuild(MIN_CONNECTOR_BUILD + 1), null);
  assert.equal(checkMinConnectorBuild(null), null);
});

test('checkMinConnectorBuild rejects outdated build', () => {
  const err = checkMinConnectorBuild(MIN_CONNECTOR_BUILD - 1);
  assert.equal(err?.ok, false);
  assert.equal(err?.error_code, 'CONNECTOR_OUTDATED');
});
