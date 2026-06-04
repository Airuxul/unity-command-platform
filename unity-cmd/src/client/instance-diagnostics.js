import fs from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import {
  CONNECTOR_FIELD,
  CONNECTOR_STATE,
  EDITOR_HTTP_CACHE_STATUS,
  INSTANCES_DIR,
} from '../constants.js';
import { findInstanceByPort, readInstanceFile } from './connector-readiness.js';
import { readEditorHttpCache } from './editor-http-cache.js';
import { ping } from './command.js';

export const HEALTH_PROBE_MS = 1_500;
export const PORT_PROBE_MS = 400;

export function normalizeProjectPath(projectPath) {
  return String(projectPath ?? '')
    .replace(/\\/g, '/')
    .replace(/\/+$/, '')
    .toLowerCase();
}

export function findInstanceByProject(projectPath) {
  const want = normalizeProjectPath(projectPath);
  if (!want || !fs.existsSync(INSTANCES_DIR)) return null;

  let best = null;
  let bestTs = 0;

  for (const name of fs.readdirSync(INSTANCES_DIR)) {
    if (!name.endsWith('.json')) continue;
    const inst = readInstanceFile(path.join(INSTANCES_DIR, name));
    if (!inst?.projectPath) continue;
    if (normalizeProjectPath(inst.projectPath) !== want) continue;
    const ts = Number(inst.timestamp) || 0;
    if (ts >= bestTs) {
      bestTs = ts;
      best = inst;
    }
  }

  return best;
}

export function isPortOpen(host, port, timeoutMs = PORT_PROBE_MS) {
  return new Promise((resolve) => {
    const socket = new net.Socket();
    let settled = false;
    const done = (value) => {
      if (settled) return;
      settled = true;
      try {
        socket.destroy();
      } catch {
        // ignored
      }
      resolve(value);
    };

    socket.setTimeout(timeoutMs);
    socket.once('error', () => done(false));
    socket.once('timeout', () => done(false));
    socket.connect(port, host, () => done(true));
  });
}

/**
 * @param {{ profile: string, host: string, port: number, connector_host?: string, projectPath?: string }} input
 */
export async function buildReachabilityDiagnostics(input) {
  const { profile, host, port, connector_host: connectorHost, projectPath } = input;
  const inst =
    findInstanceByPort(port) ??
    (projectPath ? findInstanceByProject(projectPath) : null);
  const cache = readEditorHttpCache();
  const portOpen = await isPortOpen(host, port);

  let health = null;
  if (portOpen) {
    try {
      health = await ping(
        { host, port },
        { timeoutMs: HEALTH_PROBE_MS, retryOnDisconnect: false, maxAttempts: 1 },
      );
    } catch {
      health = { ok: false };
    }
  }

  const connectorState = inst?.[CONNECTOR_FIELD.ConnectorState] ?? null;
  const listenerRunning = inst?.[CONNECTOR_FIELD.ListenerRunning];
  const compileErrors = inst?.compile_errors === true;
  const cacheStatus = cache?.port === port ? cache.status : null;

  let reason = 'unreachable';
  let hint =
    'Open this project in Unity Editor and wait for Console: "Editor HTTP server started (port …)".';

  if (!portOpen) {
    reason = 'port_closed';
    hint =
      'Unity Editor HTTP is not listening. Open the project in Unity or check UNITY_CMD_PORT.';
  } else if (!health?.ok) {
    if (cacheStatus === EDITOR_HTTP_CACHE_STATUS.Stopped || listenerRunning === false) {
      reason = 'listener_restarting';
      hint =
        'Editor HTTP is restarting (domain reload / compile). Run: unity-cmd --profile ' +
        `${profile} wait --timeout=120000`;
    } else if (connectorState === CONNECTOR_STATE.Compiling || inst?.[CONNECTOR_FIELD.IsCompiling]) {
      reason = 'editor_compiling';
      hint = 'Unity is compiling. Wait for compilation to finish, then run unity-cmd wait or ping.';
    } else if (compileErrors) {
      reason = 'compile_errors';
      hint =
        'Unity reported compile errors. Fix Console errors, let scripts recompile, then unity-cmd wait.';
    } else if (health?.data?.host && connectorHost && health.data.host !== connectorHost) {
      reason = 'host_mismatch';
      hint = `Port ${port} is another connector host (${health.data.host}). Close other Unity instances or use a different UNITY_CMD_PORT.`;
    } else {
      reason = 'health_timeout';
      hint =
        'Port is open but /health did not respond. Focus the Editor window or restart Unity; stale listeners clear after Stop().';
    }
  } else if (connectorHost && health.data?.host !== connectorHost) {
    reason = 'host_mismatch';
    hint = `Expected host "${connectorHost}", got "${health.data?.host}". Check profile port and running Unity instance.`;
  } else if (health.data?.[CONNECTOR_FIELD.CommandsReady] !== true) {
    reason = 'not_ready';
    hint = 'Connector is up but not ready for commands. Run: unity-cmd wait';
  } else {
    reason = 'ok';
    hint = null;
  }

  return {
    reason,
    hint,
    port_open: portOpen,
    health_ok: health?.ok === true,
    health_host: health?.data?.host ?? null,
    connector_state: connectorState,
    listener_running: listenerRunning,
    commands_ready: health?.data?.[CONNECTOR_FIELD.CommandsReady],
    compile_errors: compileErrors,
    cache_status: cacheStatus,
    project_path: inst?.projectPath ?? null,
    unity_pid: inst?.pid ?? cache?.pid ?? null,
    instance_generation: inst?.[CONNECTOR_FIELD.Generation] ?? null,
  };
}
