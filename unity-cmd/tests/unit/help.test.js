import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatLocalHelp, formatProfileHelp } from '../../src/help.js';

test('formatLocalHelp documents commands', () => {
  const text = formatLocalHelp();
  assert.match(text, /profile list.*list saved profile names/);
  assert.match(text, /profile set <name>.*change --host/);
  assert.doesNotMatch(text, /defaults:.*6547/);
});

test('formatLocalHelp lists saved profile names only', () => {
  const text = formatLocalHelp({
    profiles: [{ name: 'editor', host: '127.0.0.1', port: 6547, connector_host: 'editor' }],
  });
  assert.match(text, /Saved profile names:\n  editor/);
  assert.doesNotMatch(text, /6547/);
});

test('formatProfileHelp lists catalog when connected', () => {
  const text = formatProfileHelp(
    { name: 'editor', host: '127.0.0.1', port: 6547, connector_host: 'editor' },
    {
      connected: true,
      catalog: {
        catalog_version: 'abc',
        connector_build: 14,
        host_kind: 'editor',
        commands: [
          { name: 'compile', aliases: ['recompile'], description: 'Compile' },
        ],
      },
    },
  );
  assert.match(text, /compile/);
  assert.match(text, /--profile editor compile/);
  assert.doesNotMatch(text, /profile list/);
});

test('formatProfileHelp indents command params', () => {
  const text = formatProfileHelp(
    { name: 'editor', host: '127.0.0.1', port: 6547, connector_host: 'editor' },
    {
      connected: true,
      catalog: {
        commands: [
          {
            name: 'console',
            description: 'Read logs',
            params: ['--lines <int> max entries'],
          },
        ],
      },
    },
  );
  assert.match(text, /console — Read logs/);
  assert.match(text, /--lines <int> max entries/);
});

test('formatProfileHelp offline is compact', () => {
  const text = formatProfileHelp(
    { name: 'editor-play', host: '127.0.0.1', port: 6794, connector_host: 'editor_play' },
    { connected: false, reason: 'unreachable' },
  );
  assert.match(text, /offline/i);
  assert.match(text, /Typical: echo, ping/);
});
