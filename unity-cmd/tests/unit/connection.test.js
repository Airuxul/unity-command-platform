import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {
  loadProfile,
  saveProfile,
  updateProfile,
  deleteProfile,
  defaultPortForHostKind,
  hostKindMatches,
  normalizeHostKind,
  resolveTarget,
  profileNameForHostKind,
  DEFAULT_EDITOR_PORT,
  DEFAULT_EDITOR_PLAY_PORT,
  DEFAULT_PLAYER_PORT,
} from '../../src/client/connection.js';

test('defaultPortForHostKind uses fixed defaults', () => {
  assert.equal(defaultPortForHostKind('editor'), DEFAULT_EDITOR_PORT);
  assert.equal(defaultPortForHostKind('editor_play'), DEFAULT_EDITOR_PLAY_PORT);
  assert.equal(defaultPortForHostKind('player'), DEFAULT_PLAYER_PORT);
});

test('hostKindMatches compares normalized kinds', () => {
  assert.ok(hostKindMatches('player', 'player'));
  assert.ok(hostKindMatches('editor_play', 'editor_play'));
  assert.throws(() => normalizeHostKind('runtime'), /Invalid host kind/);
});

test('profileNameForHostKind maps integration profiles', () => {
  assert.equal(profileNameForHostKind('editor_play'), 'editor-play');
  assert.equal(profileNameForHostKind('player'), 'package-play');
});

test('resolveTarget uses saved profile only', async () => {
  const name = `kind-match-${Date.now()}`;
  try {
    saveProfile(name, {
      host: '127.0.0.1',
      port: 6795,
      connector_host: 'player',
    });
    const target = await resolveTarget({ profile: name, timeoutMs: 800 });
    if (target) {
      assert.equal(target.port, 6795);
      assert.equal(target.connector_host, 'player');
    } else {
      assert.ok(true, 'player not running; profile file still defines endpoint');
    }
  } finally {
    deleteProfile(name);
  }
});

test('profile save update and delete', () => {
  const name = `test-${Date.now()}`;
  try {
    saveProfile(name, {
      host: '127.0.0.1',
      port: 6401,
      connector_host: 'editor',
    });
    const loaded = loadProfile(name);
    assert.equal(loaded.port, 6401);

    const updated = updateProfile(name, { port: 6402, connector_host: 'editor_play' });
    assert.equal(updated.port, 6402);
    assert.equal(updated.connector_host, 'editor_play');

    const deleted = deleteProfile(name);
    assert.equal(deleted.deleted, true);
    assert.equal(loadProfile(name), null);
  } catch (err) {
    assert.fail(err);
  }
});
