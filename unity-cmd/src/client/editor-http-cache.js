import fs from 'node:fs';
import { EDITOR_HTTP_CACHE_PATH, EDITOR_HTTP_CACHE_STATUS } from '../constants.js';

/** @returns {{ pid?: number, session_id?: string, generation?: number, port?: number, status?: string, listener_id?: string }|null} */
export function readEditorHttpCache() {
  try {
    if (!fs.existsSync(EDITOR_HTTP_CACHE_PATH)) return null;
    return JSON.parse(fs.readFileSync(EDITOR_HTTP_CACHE_PATH, 'utf8'));
  } catch {
    return null;
  }
}

/**
 * @param {{ port: number }} target
 * @param {{ session_id?: string, generation?: number }} inst
 * @param {() => object|null} [readCache]
 */
export function cacheMatchesInstance(target, inst, readCache = readEditorHttpCache) {
  const cache = readCache();
  if (!cache || cache.port !== target.port) return true;

  if (cache.status === EDITOR_HTTP_CACHE_STATUS.Stopped) return true;

  if (inst?.session_id && cache.session_id && cache.session_id !== inst.session_id) {
    return false;
  }

  if (
    inst?.generation != null &&
    cache.generation != null &&
    cache.generation !== inst.generation
  ) {
    return false;
  }

  return true;
}
