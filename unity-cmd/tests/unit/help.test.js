import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatHelp } from '../../src/help.js';

test('formatHelp lists catalog commands', () => {
  const text = formatHelp({
    catalog_version: 'abc',
    connector_build: 6,
    commands: [
      { name: 'compile', aliases: ['recompile'], is_job: true, description: 'Compile' },
      { name: 'ping', aliases: [], is_job: false, description: 'Ping' },
    ],
    commands_by_name: {},
    alias_to_command: {},
  });
  assert.match(text, /catalog_version: abc/);
  assert.match(text, /compile/);
  assert.match(text, /recompile/);
});

test('formatHelp without catalog', () => {
  const text = formatHelp(null);
  assert.match(text, /No live catalog/);
});
