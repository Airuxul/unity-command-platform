import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  isEditorInstanceBusy,
  isEditorInstanceReady,
  normalizeHealthState,
  readPlayMode,
  readCommandsReady,
  readConnectorState,
} from '../../src/client/connector-readiness.js';
import { cacheMatchesInstance } from '../../src/client/editor-http-cache.js';
import {
  CONNECTOR_BUSY_STATES,
  CONNECTOR_STATE,
  CONNECTOR_FIELD,
  PLAY_MODE,
  DEFAULT_EDITOR_PORT,
} from '../../src/constants.js';

// ---------- helpers ----------------------------------------------------------

/** Build a minimal health/instance snapshot. */
function inst(overrides = {}) {
  return {
    [CONNECTOR_FIELD.ConnectorState]: CONNECTOR_STATE.Ready,
    [CONNECTOR_FIELD.CommandsReady]: true,
    [CONNECTOR_FIELD.ListenerRunning]: true,
    [CONNECTOR_FIELD.PlayMode]: PLAY_MODE.Edit,
    port: DEFAULT_EDITOR_PORT,
    timestamp: Date.now(),
    ...overrides,
  };
}

// ---------- CONNECTOR_BUSY_STATES --------------------------------------------

test('CONNECTOR_BUSY_STATES includes all blocking states', () => {
  assert.equal(CONNECTOR_BUSY_STATES.has(CONNECTOR_STATE.Compiling), true);
  assert.equal(CONNECTOR_BUSY_STATES.has(CONNECTOR_STATE.Reloading), true);
  assert.equal(CONNECTOR_BUSY_STATES.has(CONNECTOR_STATE.Refreshing), true);
  assert.equal(CONNECTOR_BUSY_STATES.has(CONNECTOR_STATE.EnteringPlayMode), true);
  assert.equal(CONNECTOR_BUSY_STATES.has(CONNECTOR_STATE.Stopped), true);
});

test('CONNECTOR_BUSY_STATES does not include play-mode states', () => {
  assert.equal(CONNECTOR_BUSY_STATES.has(PLAY_MODE.Playing), false);
  assert.equal(CONNECTOR_BUSY_STATES.has(PLAY_MODE.Paused), false);
  assert.equal(CONNECTOR_BUSY_STATES.has(CONNECTOR_STATE.Ready), false);
});

// ---------- readConnectorState -----------------------------------------------

test('readConnectorState returns connector_state field', () => {
  assert.equal(readConnectorState(inst()), CONNECTOR_STATE.Ready);
  assert.equal(readConnectorState(inst({ connector_state: CONNECTOR_STATE.Compiling })), CONNECTOR_STATE.Compiling);
});

test('readConnectorState returns null when instance is null', () => {
  assert.equal(readConnectorState(null), null);
});

// ---------- readPlayMode -----------------------------------------------------

test('readPlayMode returns play_mode field', () => {
  assert.equal(readPlayMode(inst({ play_mode: PLAY_MODE.Playing })), PLAY_MODE.Playing);
  assert.equal(readPlayMode(inst({ play_mode: PLAY_MODE.Paused })), PLAY_MODE.Paused);
  assert.equal(readPlayMode(inst({ play_mode: PLAY_MODE.Edit })), PLAY_MODE.Edit);
});

test('readPlayMode defaults to edit when instance is null', () => {
  assert.equal(readPlayMode(null), PLAY_MODE.Edit);
});

// ---------- readCommandsReady ------------------------------------------------

test('readCommandsReady returns true only when commands_ready=true', () => {
  assert.equal(readCommandsReady(inst()), true);
  assert.equal(readCommandsReady(inst({ commands_ready: false })), false);
  assert.equal(readCommandsReady(inst({ commands_ready: undefined })), false);
});

test('normalizeHealthState maps health payload to unified state shape', () => {
  const state = normalizeHealthState({
    [CONNECTOR_FIELD.ConnectorState]: CONNECTOR_STATE.Compiling,
    [CONNECTOR_FIELD.PlayMode]: PLAY_MODE.Playing,
    [CONNECTOR_FIELD.CommandsReady]: false,
    [CONNECTOR_FIELD.ListenerRunning]: true,
    [CONNECTOR_FIELD.BlockingReasons]: ['compiling'],
    [CONNECTOR_FIELD.SessionId]: 'session-1',
    [CONNECTOR_FIELD.Generation]: 42,
  });
  assert.equal(state.connector_state, CONNECTOR_STATE.Compiling);
  assert.equal(state.play_mode, PLAY_MODE.Playing);
  assert.equal(state.commands_ready, false);
  assert.equal(state.listener_running, true);
  assert.deepEqual(state.blocking_reasons, ['compiling']);
  assert.equal(state.session_id, 'session-1');
  assert.equal(state.generation, 42);
});

// ---------- isEditorInstanceBusy — normal states ----------------------------

test('ready instance is not busy', () => {
  assert.equal(isEditorInstanceBusy(inst()), false);
});

test('null instance is busy', () => {
  assert.equal(isEditorInstanceBusy(null), true);
});

test('listener_running=false is busy regardless of connector_state', () => {
  assert.equal(
    isEditorInstanceBusy(inst({ listener_running: false })),
    true,
  );
  assert.equal(
    isEditorInstanceBusy(inst({ connector_state: CONNECTOR_STATE.Ready, listener_running: false })),
    true,
  );
});

// ---------- isEditorInstanceBusy — compile-fail / reloading -----------------

test('compiling state is busy', () => {
  assert.equal(
    isEditorInstanceBusy(inst({
      connector_state: CONNECTOR_STATE.Compiling,
      commands_ready: false,
    })),
    true,
  );
});

test('reloading state is busy (domain-reload after compile error)', () => {
  assert.equal(
    isEditorInstanceBusy(inst({
      connector_state: CONNECTOR_STATE.Reloading,
      commands_ready: false,
    })),
    true,
  );
});

test('refreshing state is busy', () => {
  assert.equal(
    isEditorInstanceBusy(inst({
      connector_state: CONNECTOR_STATE.Refreshing,
      commands_ready: false,
    })),
    true,
  );
});

test('stopped state is busy', () => {
  assert.equal(
    isEditorInstanceBusy(inst({
      connector_state: CONNECTOR_STATE.Stopped,
      commands_ready: false,
      listener_running: false,
    })),
    true,
  );
});

test('entering_playmode is busy', () => {
  assert.equal(
    isEditorInstanceBusy(inst({
      connector_state: CONNECTOR_STATE.EnteringPlayMode,
      commands_ready: false,
    })),
    true,
  );
});

// ---------- isEditorInstanceBusy — recovery sequence ------------------------

test('state machine: compile-fail then recover to ready', () => {
  // Phase 1: compile starts — connector moves to compiling
  const compiling = inst({
    connector_state: CONNECTOR_STATE.Compiling,
    commands_ready: false,
  });
  assert.equal(isEditorInstanceBusy(compiling), true);
  assert.equal(isEditorInstanceReady(compiling), false);

  // Phase 2: compile fails, domain reload begins
  const reloading = inst({
    connector_state: CONNECTOR_STATE.Reloading,
    commands_ready: false,
  });
  assert.equal(isEditorInstanceBusy(reloading), true);
  assert.equal(isEditorInstanceReady(reloading), false);

  // Phase 3: user fixes code, second compile runs
  const recompiling = inst({
    connector_state: CONNECTOR_STATE.Compiling,
    commands_ready: false,
  });
  assert.equal(isEditorInstanceBusy(recompiling), true);
  assert.equal(isEditorInstanceReady(recompiling), false);

  // Phase 4: compile succeeds, connector is ready
  const recovered = inst({
    connector_state: CONNECTOR_STATE.Ready,
    commands_ready: true,
    listener_running: true,
  });
  assert.equal(isEditorInstanceBusy(recovered), false);
  assert.equal(isEditorInstanceReady(recovered), true);
});

// ---------- isEditorInstanceBusy — play mode does not block ------------------

test('playing while ready is NOT busy (play mode orthogonal to connector state)', () => {
  const playing = inst({
    connector_state: CONNECTOR_STATE.Ready,
    play_mode: PLAY_MODE.Playing,
    commands_ready: true,
    listener_running: true,
  });
  assert.equal(isEditorInstanceBusy(playing), false);
  assert.equal(isEditorInstanceReady(playing), true);
});

test('paused while ready is NOT busy', () => {
  const paused = inst({
    connector_state: CONNECTOR_STATE.Ready,
    play_mode: PLAY_MODE.Paused,
    commands_ready: true,
    listener_running: true,
  });
  assert.equal(isEditorInstanceBusy(paused), false);
  assert.equal(isEditorInstanceReady(paused), true);
});

// ---------- commands_ready=false even when connector_state is ready ----------

test('ready state but commands_ready=false is still busy', () => {
  const notReady = inst({
    connector_state: CONNECTOR_STATE.Ready,
    commands_ready: false,
  });
  assert.equal(isEditorInstanceBusy(notReady), true);
  assert.equal(isEditorInstanceReady(notReady), false);
});

// ---------- cacheMatchesInstance --------------------------------------------

test('cacheMatchesInstance: same session and generation passes', () => {
  const readCache = () => ({
    port: DEFAULT_EDITOR_PORT,
    status: 'running',
    session_id: 'sess-1',
    generation: 5,
  });
  assert.equal(
    cacheMatchesInstance({ port: DEFAULT_EDITOR_PORT }, { session_id: 'sess-1', generation: 5 }, readCache),
    true,
  );
});

test('cacheMatchesInstance: different session rejects', () => {
  const readCache = () => ({
    port: DEFAULT_EDITOR_PORT,
    status: 'running',
    session_id: 'old-session',
    generation: 5,
  });
  assert.equal(
    cacheMatchesInstance({ port: DEFAULT_EDITOR_PORT }, { session_id: 'new-session', generation: 5 }, readCache),
    false,
  );
});

test('cacheMatchesInstance: different generation rejects', () => {
  const readCache = () => ({
    port: DEFAULT_EDITOR_PORT,
    status: 'running',
    session_id: 'sess-1',
    generation: 3,
  });
  assert.equal(
    cacheMatchesInstance({ port: DEFAULT_EDITOR_PORT }, { session_id: 'sess-1', generation: 5 }, readCache),
    false,
  );
});

test('cacheMatchesInstance: stopped cache always matches (reloading scenario)', () => {
  const readCache = () => ({
    port: DEFAULT_EDITOR_PORT,
    status: 'stopped',
    session_id: 'old-session',
    generation: 1,
  });
  assert.equal(
    cacheMatchesInstance({ port: DEFAULT_EDITOR_PORT }, { session_id: 'new-session', generation: 99 }, readCache),
    true,
  );
});

test('cacheMatchesInstance: no cache (null) always passes', () => {
  assert.equal(
    cacheMatchesInstance({ port: DEFAULT_EDITOR_PORT }, { session_id: 'any', generation: 1 }, () => null),
    true,
  );
});
