import { sendCommand, ping, fetchCatalog } from './client/command.js';
import { selectInstance } from './client/target.js';
import { resolveTimeoutMs } from './timeout.js';
import { loadCatalog, resolveRemoteCommand, LOCAL_META } from './catalog.js';
import { coerceParameters } from './params.js';
import { cliError, enrichFailure, enrichThrown } from './errors.js';
import { formatHelp } from './help.js';

export function parseArgs(argv) {
  const args = [...argv];
  let timeoutMs;
  const flags = {};
  const positional = [];

  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a === '--timeout' && args[i + 1]) {
      timeoutMs = Number.parseInt(args[++i], 10);
      continue;
    }
    if (a.startsWith('--timeout=')) {
      timeoutMs = Number.parseInt(a.split('=')[1], 10);
      continue;
    }
    if (a.startsWith('--')) {
      const key = a.slice(2);
      const next = args[i + 1];
      if (next && !next.startsWith('--')) {
        flags[key] = next;
        i++;
      } else {
        flags[key] = true;
      }
      continue;
    }
    positional.push(a);
  }

  return { positional, flags, timeoutMs: resolveTimeoutMs(timeoutMs) };
}

export async function runCommand(command, flags, timeoutMs) {
  const projectHint = process.env.UNITY_CMD_PROJECT ?? flags.project;
  const target = selectInstance(projectHint);
  if (!target) {
    const err = cliError(
      'No Unity Editor instance found in heartbeat registry.',
      'NO_INSTANCE',
      'Open your project with unity-connector installed. Set UNITY_CMD_PROJECT when multiple Editors run.',
    );
    printJson(err);
    process.exit(1);
  }

  if (command === 'ping') {
    const res = await ping(target, { timeoutMs });
    printJson(res.ok ? res : enrichFailure(res));
    process.exit(res.ok ? 0 : 1);
  }

  if (command === 'list') {
    try {
      const forceRefresh = flags['refresh-catalog'] === true || flags.refresh_catalog === true;
      const catalog = await loadCatalog(target, { timeoutMs, forceRefresh });
      printJson({
        ok: true,
        catalog_version: catalog.catalog_version,
        connector_build: catalog.connector_build,
        commands: catalog.commands,
        alias_to_command: catalog.alias_to_command,
      });
      process.exit(0);
    } catch (err) {
      printJson(enrichThrown(err));
      process.exit(1);
    }
  }

  if (command === 'help') {
    try {
      const catalog = await loadCatalog(target, { timeoutMs });
      console.log(formatHelp(catalog));
    } catch {
      console.log(formatHelp(null));
    }
    process.exit(0);
  }

  const forceCatalogRefresh =
    flags['refresh-catalog'] === true || flags.refresh_catalog === true;

  const parameters = coerceParameters({ ...flags });
  delete parameters.project;
  delete parameters['refresh-catalog'];
  delete parameters.refresh_catalog;

  let catalog;
  try {
    catalog = await loadCatalog(target, {
      timeoutMs,
      forceRefresh: forceCatalogRefresh,
    });
  } catch (err) {
    printJson(enrichThrown(err, { command }));
    process.exit(1);
  }

  const resolved = resolveRemoteCommand(command, parameters, catalog);
  const effectiveTimeout =
    resolved.minTimeoutMs != null
      ? Math.max(timeoutMs, resolved.minTimeoutMs)
      : timeoutMs;

  try {
    const res = await sendCommand(target, resolved.command, parameters, {
      timeoutMs: effectiveTimeout,
      allowConnectionRetry: resolved.allowConnectionRetry,
    });
    const out = res.ok
      ? res
      : enrichFailure(res, {
          command: resolved.command,
          status: res.status,
          job: Boolean(res.job_id),
        });
    printJson(out);
    process.exit(res.ok ? 0 : 1);
  } catch (err) {
    printJson(enrichThrown(err, { command: resolved.command }));
    process.exit(1);
  }
}

function printJson(obj) {
  console.log(JSON.stringify(obj, null, 2));
}
