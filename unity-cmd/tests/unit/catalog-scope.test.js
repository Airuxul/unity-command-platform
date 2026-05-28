import { test } from 'node:test';
import assert from 'node:assert/strict';
import { checkScopeCompatibility } from '../../src/catalog.js';

test('editor host rejects runtime scope', () => {
  assert.match(
    checkScopeCompatibility('echo', 'runtime', 'editor'),
    /editor-play|package-play/,
  );
});

test('play host rejects editor scope', () => {
  assert.match(
    checkScopeCompatibility('compile', 'editor', 'editor_play'),
    /profile editor/,
  );
  assert.match(checkScopeCompatibility('compile', 'editor', 'player'), /profile editor/);
});

test('matching scopes pass', () => {
  assert.equal(checkScopeCompatibility('compile', 'editor', 'editor'), null);
  assert.equal(checkScopeCompatibility('echo', 'runtime', 'editor_play'), null);
});
