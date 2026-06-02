import { test } from 'node:test';
import assert from 'node:assert/strict';
import { HOST_KIND } from '../../src/constants.js';
import {
  DEFAULT_PORT_BY_HOST_KIND,
  formatDefaultPortsLine,
  formatProfileCreateExample,
} from '../../src/constants.js';

test('formatDefaultPortsLine uses canonical ports', () => {
  assert.match(formatDefaultPortsLine(), /editor 6547, editor_play 6794, player 6795/);
});

test('formatProfileCreateExample builds create command', () => {
  assert.equal(
    formatProfileCreateExample('editor'),
    'unity-cmd profile create editor --host 127.0.0.1 --port 6547 --host-kind editor',
  );
  assert.equal(
    formatProfileCreateExample('package-play', HOST_KIND.Player),
    'unity-cmd profile create package-play --host 127.0.0.1 --port 6795 --host-kind player',
  );
});

test('DEFAULT_PORT_BY_HOST_KIND matches host kinds', () => {
  assert.equal(DEFAULT_PORT_BY_HOST_KIND[HOST_KIND.Editor], 6547);
  assert.equal(DEFAULT_PORT_BY_HOST_KIND[HOST_KIND.EditorPlay], 6794);
  assert.equal(DEFAULT_PORT_BY_HOST_KIND[HOST_KIND.Player], 6795);
});
