import { ping } from './client/command.js';
import {
  saveProfile,
  loadProfile,
  updateProfile,
  deleteProfile,
  listProfiles,
  defaultPortForHostKind,
  normalizeHostKind,
  hostKindMatches,
} from './client/connection.js';
import { cliError, enrichFailure } from './errors.js';
import { hostKindDescription } from './host-labels.js';

function parsePort(raw, hostKind) {
  const port = Number.parseInt(raw ?? '', 10);
  return port > 0 ? port : defaultPortForHostKind(hostKind);
}

/** @param {object} profile */
export function formatProfileBlock(profile) {
  const kind = profile.connector_host ?? 'editor';
  return [
    `  host:     ${profile.host}`,
    `  port:     ${profile.port}`,
    `  kind:     ${kind} (${hostKindDescription(kind)})`,
    profile.updated_at ? `  updated:  ${profile.updated_at}` : null,
  ]
    .filter(Boolean)
    .join('\n');
}

export function printProfileUsage() {
  console.log(`Profile commands:

  unity-cmd profile list
  unity-cmd profile show <name>
  unity-cmd profile create <name> [--host <ip>] [--port <n>] [--host-kind <kind>] [--no-verify] [--force]
  unity-cmd profile set <name> [--host <ip>] [--port <n>] [--host-kind <kind>]
  unity-cmd profile delete <name>

  host-kind: editor | editor_play | player
  defaults:  editor 6547, editor_play 6794, player 6795

Examples:
  unity-cmd profile create editor --host 127.0.0.1 --port 6547 --host-kind editor
  unity-cmd profile create editor-play --port 6794 --host-kind editor_play
  unity-cmd profile set editor --port 6548
  unity-cmd profile set editor-play --host 192.168.1.10 --port 6794
  unity-cmd profile show editor
  unity-cmd profile list

  Add --json for machine-readable output.`);
}

function profilePayloadFromFlags(name, flags, existing = null) {
  const hostKind = normalizeHostKind(
    flags['host-kind'] ?? existing?.connector_host ?? 'editor',
  );
  return {
    name,
    host: (flags.host ?? existing?.host ?? '127.0.0.1').trim(),
    port: parsePort(flags.port ?? existing?.port, hostKind),
    connector_host: hostKind,
  };
}

/** Merge only flags the user passed (for profile set). */
function mergeProfilePatch(existing, flags) {
  const hostKind =
    flags['host-kind'] !== undefined
      ? normalizeHostKind(flags['host-kind'])
      : normalizeHostKind(existing.connector_host ?? 'editor');

  const next = {
    name: existing.name,
    host: flags.host !== undefined ? String(flags.host).trim() : existing.host,
    port:
      flags.port !== undefined ? parsePort(flags.port, hostKind) : Number(existing.port),
    connector_host:
      flags['host-kind'] !== undefined ? hostKind : normalizeHostKind(existing.connector_host),
  };
  return next;
}

function wantsJson(flags) {
  return flags.json === true || flags.json === 'true';
}

export async function runProfileCommand(subArgs, flags, timeoutMs) {
  const action = subArgs[0];
  if (!action) {
    printProfileUsage();
    process.exit(1);
  }

  switch (action) {
    case 'list':
      await runProfileList(flags, timeoutMs);
      return;
    case 'show':
      runProfileShow(subArgs[1] ?? flags.name, flags);
      return;
    case 'create':
      await runProfileCreate(subArgs[1] ?? flags.name, flags, timeoutMs);
      return;
    case 'set':
    case 'update':
      runProfileSet(subArgs[1] ?? flags.name, flags);
      return;
    case 'delete':
    case 'remove':
      runProfileDelete(subArgs[1] ?? flags.name, flags);
      return;
    default:
      if (wantsJson(flags)) {
        printJson(cliError(`Unknown profile action: ${action}`, 'INVALID_USAGE'));
      } else {
        console.error(`Unknown profile action: ${action}`);
        printProfileUsage();
      }
      process.exit(1);
  }
}

async function runProfileList(flags, timeoutMs) {
  const profiles = listProfiles().sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''));

  if (wantsJson(flags)) {
    const rows = [];
    for (const p of profiles) {
      let alive = false;
      let health = null;
      try {
        const res = await ping(p, { timeoutMs: Math.min(timeoutMs, 5000), retryOnDisconnect: false });
        alive = res.ok && hostKindMatches(p.connector_host, res.data?.host);
        health = res.data;
      } catch {
        alive = false;
      }
      rows.push({ ...p, alive, health });
    }
    printJson({ ok: true, profiles: rows });
    process.exit(0);
  }

  if (profiles.length === 0) {
    console.log('No profiles. Create one, e.g.:');
    console.log(
      '  unity-cmd profile create editor --host 127.0.0.1 --port 6547 --host-kind editor',
    );
    process.exit(0);
  }

  for (const p of profiles) {
    console.log(p.name);
  }
  process.exit(0);
}

function runProfileShow(name, flags) {
  if (!name) {
    if (wantsJson(flags)) {
      printJson(cliError('profile show requires a name', 'INVALID_USAGE'));
    } else {
      console.error('Usage: unity-cmd profile show <name>');
    }
    process.exit(1);
  }
  const profile = loadProfile(name);
  if (!profile) {
    if (wantsJson(flags)) {
      printJson(cliError(`Profile not found: ${name}`, 'PROFILE_NOT_FOUND'));
    } else {
      console.error(`Profile not found: ${name}`);
    }
    process.exit(1);
  }

  if (wantsJson(flags)) {
    printJson({ ok: true, profile });
    process.exit(0);
  }

  console.log(`Profile: ${profile.name}`);
  console.log(formatProfileBlock(profile));
  process.exit(0);
}

async function runProfileCreate(name, flags, timeoutMs) {
  if (!name) {
    if (wantsJson(flags)) {
      printJson(cliError('profile create requires a name', 'INVALID_USAGE'));
    } else {
      console.error('Usage: unity-cmd profile create <name> [--host ...] [--port ...] [--host-kind ...]');
      console.error('Run: unity-cmd profile  (for examples)');
    }
    process.exit(1);
  }
  if (loadProfile(name) && flags.force !== true && flags.force !== 'true') {
    const msg = `Profile already exists: ${name}. Use "profile set ${name} ..." or --force`;
    if (wantsJson(flags)) {
      printJson(cliError(msg, 'PROFILE_EXISTS'));
    } else {
      console.error(msg);
    }
    process.exit(1);
  }

  const draft = profilePayloadFromFlags(name, flags);
  const verify = flags['no-verify'] !== true;

  if (verify) {
    const res = await ping(draft, { timeoutMs, retryOnDisconnect: true });
    if (!res.ok || !hostKindMatches(draft.connector_host, res.data?.host)) {
      const failure = enrichFailure({
        ok: false,
        error: res.ok
          ? `health host mismatch (expected ${draft.connector_host}, got ${res.data?.host})`
          : 'health failed',
        status: res.status,
      });
      if (wantsJson(flags)) {
        printJson(failure);
      } else {
        console.error(failure.error ?? 'Could not verify endpoint.');
        console.error('Use --no-verify to save without checking, or fix host/port/host-kind.');
      }
      process.exit(1);
    }
    draft.connector_host = res.data?.host ?? draft.connector_host;
  }

  const saved = saveProfile(name, draft);

  if (wantsJson(flags)) {
    printJson({ ok: true, profile: saved, verified: verify });
    process.exit(0);
  }

  console.log(`Created profile "${saved.name}"${verify ? '' : ' (not verified)'}`);
  console.log(formatProfileBlock(saved));
  console.log('');
  console.log(`Use: unity-cmd --profile ${saved.name} ping`);
  process.exit(0);
}

function runProfileSet(name, flags) {
  if (!name) {
    if (wantsJson(flags)) {
      printJson(cliError('profile set requires a name', 'INVALID_USAGE'));
    } else {
      console.error('Usage: unity-cmd profile set <name> [--host <ip>] [--port <n>] [--host-kind <kind>]');
      console.error('');
      console.error('Change one or more fields; omitted flags stay unchanged.');
      console.error('  unity-cmd profile set editor --port 6548');
      console.error('  unity-cmd profile set editor-play --host 192.168.1.10');
    }
    process.exit(1);
  }

  const existing = loadProfile(name);
  if (!existing) {
    if (wantsJson(flags)) {
      printJson(cliError(`Profile not found: ${name}`, 'PROFILE_NOT_FOUND'));
    } else {
      console.error(`Profile not found: ${name}`);
      console.error(`Create it first: unity-cmd profile create ${name} ...`);
    }
    process.exit(1);
  }

  const hasPatch =
    flags.host !== undefined || flags.port !== undefined || flags['host-kind'] !== undefined;
  if (!hasPatch) {
    if (wantsJson(flags)) {
      printJson(
        cliError(
          'profile set requires at least one of: --host, --port, --host-kind',
          'INVALID_USAGE',
        ),
      );
    } else {
      console.error(`Profile "${name}" — nothing to change. Pass at least one flag:`);
      console.error('');
      console.log(formatProfileBlock(existing));
      console.error('');
      console.error('Examples:');
      console.error(`  unity-cmd profile set ${name} --port 6548`);
      console.error(`  unity-cmd profile set ${name} --host-kind editor_play --port 6794`);
    }
    process.exit(1);
  }

  let merged;
  try {
    merged = mergeProfilePatch(existing, flags);
  } catch (err) {
    if (wantsJson(flags)) {
      printJson(cliError(err.message, err.error_code ?? 'INVALID_HOST_KIND'));
    } else {
      console.error(err.message);
    }
    process.exit(1);
  }

  const saved = updateProfile(name, merged);

  if (wantsJson(flags)) {
    printJson({ ok: true, profile: saved });
    process.exit(0);
  }

  console.log(`Updated profile "${saved.name}"`);
  console.log(formatProfileBlock(saved));
  process.exit(0);
}

function runProfileDelete(name, flags) {
  if (!name) {
    if (wantsJson(flags)) {
      printJson(cliError('profile delete requires a name', 'INVALID_USAGE'));
    } else {
      console.error('Usage: unity-cmd profile delete <name>');
    }
    process.exit(1);
  }
  try {
    const result = deleteProfile(name);
    if (wantsJson(flags)) {
      printJson({ ok: true, ...result });
    } else {
      console.log(`Deleted profile "${result.name}"`);
    }
    process.exit(0);
  } catch (err) {
    if (wantsJson(flags)) {
      printJson(cliError(err.message, err.error_code ?? 'PROFILE_NOT_FOUND'));
    } else {
      console.error(err.message);
    }
    process.exit(1);
  }
}

function printJson(obj) {
  console.log(JSON.stringify(obj, null, 2));
}
