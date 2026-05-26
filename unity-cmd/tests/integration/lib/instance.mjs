import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { requestJson } from '../../../src/client/http.js';

export function getInstancesDir() {
  return path.join(os.homedir(), '.unity-cmd', 'instances');
}

export function listInstances() {
  const dir = getInstancesDir();
  if (!fs.existsSync(dir)) return [];
  return fs
    .readdirSync(dir)
    .filter((f) => f.endsWith('.json'))
    .map((f) => {
      try {
        return JSON.parse(fs.readFileSync(path.join(dir, f), 'utf8').replace(/^\uFEFF/, ''));
      } catch {
        return null;
      }
    })
    .filter(Boolean);
}

export function selectInstance(projectHint) {
  const instances = listInstances();
  if (instances.length === 0) return null;

  if (projectHint) {
    const hint = projectHint.toLowerCase().replace(/\\/g, '/');
    const match = instances.find((i) => {
      const p = (i.project_path ?? '').toLowerCase().replace(/\\/g, '/');
      return p === hint || p.includes(hint) || path.basename(p) === path.basename(hint);
    });
    if (match) return match;
  }

  if (instances.length === 1) return instances[0];
  return null;
}

export async function healthCheck(instance, timeoutMs = 3000) {
  if (!instance?.host || !instance?.port) return false;
  try {
    const { status, data } = await requestJson(
      `http://${instance.host}:${instance.port}/health`,
      { timeoutMs },
    );
    return status === 200 && data?.ok === true;
  } catch {
    return false;
  }
}

export async function waitForInstanceAsync({ projectHint, timeoutMs }) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const inst = selectInstance(projectHint);
    if (inst?.host && inst?.port && (await healthCheck(inst, 2000))) {
      return inst;
    }
    await new Promise((r) => setTimeout(r, 200));
  }

  const inst = selectInstance(projectHint);
  if (inst?.host && inst?.port && (await healthCheck(inst, 2000))) {
    return inst;
  }
  return null;
}

export function readManifestEditorState(instance) {
  return instance?.editor_state ?? {};
}
