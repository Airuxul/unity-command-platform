import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { saveProfile, loadProfile, deleteProfile } from '../../src/client/connection.js';
import { formatProfileBlock } from '../../src/profile.js';

test('formatProfileBlock is readable', () => {
  const text = formatProfileBlock({
    name: 'editor',
    host: '127.0.0.1',
    port: 6547,
    connector_host: 'editor',
    updated_at: '2026-01-01T00:00:00.000Z',
  });
  assert.match(text, /host:\s+127\.0\.0\.1/);
  assert.match(text, /kind:\s+editor/);
});

test('profile save and show fields', () => {
  const name = `fmt-${Date.now()}`;
  try {
    saveProfile(name, {
      host: '10.0.0.1',
      port: 6400,
      connector_host: 'editor_play',
    });
    const loaded = loadProfile(name);
    assert.equal(loaded.host, '10.0.0.1');
    assert.equal(loaded.connector_host, 'editor_play');
  } finally {
    deleteProfile(name);
  }
});
