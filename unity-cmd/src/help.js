import { ping } from './client/command.js';
import { loadProfile, listProfiles, normalizeHostKind, hostKindMatches } from './client/connection.js';
import { loadCatalog, readCachedCatalog } from './catalog.js';
import { hostKindLabel, reachHint } from './host-labels.js';
import { HOST_KIND } from './constants.js';
import { cliResult } from './cli-result.js';
import { resolveProfileName } from './remote-command.js';

const OFFLINE_HINTS = {
  editor: 'compile, console, profiler, play/stop, screenshot, ping',
  editor_play: 'echo, ping',
  player: 'echo, ping',
};

function exampleLines(profileName, hostKind) {
  const k = normalizeHostKind(hostKind);
  const p = `--profile ${profileName}`;
  if (k === HOST_KIND.EditorPlay || k === HOST_KIND.Player) {
    return [`  unity-cmd ${p} echo`, `  unity-cmd ${p} ping`];
  }
  return [
    `  unity-cmd ${p} compile`,
    `  unity-cmd ${p} play`,
    `  unity-cmd ${p} console --lines 20`,
  ];
}

function formatCommandList(catalog) {
  const sorted = [...(catalog.commands ?? [])].sort((a, b) =>
    (a.name ?? '').localeCompare(b.name ?? ''),
  );
  const lines = [];
  for (const entry of sorted) {
    const aliases = entry.aliases?.length > 0 ? ` (${entry.aliases.join(', ')})` : '';
    const desc = entry.description ? ` — ${entry.description}` : '';
    lines.push(`  ${entry.name}${aliases}${desc}`);
    for (const param of entry.params ?? []) {
      lines.push(`      ${param}`);
    }
  }
  return lines;
}

export function formatLocalHelp(options = {}) {
  const profiles = options.profiles ?? [];
  const lines = [
    'unity-cmd — local commands',
    '',
    'Local commands (no --profile required):',
    '  help                         local help, or catalog with --profile <name> help',
    '  profile list                 list saved profile names',
    '  profile show <name>          show host, port, and host-kind for one profile',
    '  profile create <name>        save endpoint after /health check (see: unity-cmd profile)',
    '  profile set <name>           change --host, --port, and/or --host-kind on existing profile',
    '  profile delete <name>        remove a profile file',
    '',
    'Remote commands (require --profile or UNITY_CMD_PROFILE):',
    '  ping                         check /health for the profile endpoint',
    '  list                         print command catalog as JSON',
    '  help                         catalog for that profile (human-readable)',
    '  <command>                    run a connector command (see --profile <name> help)',
  ];

  if (profiles.length > 0) {
    lines.push('', 'Saved profile names:');
    for (const p of profiles.sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))) {
      lines.push(`  ${p.name}`);
    }
  }

  return lines.join('\n');
}

export function formatProfileHelp(profile, options = {}) {
  const hostKind = normalizeHostKind(profile.connector_host ?? 'editor');
  const { catalog, connected = false, cached = false, reason } = options;

  const lines = [
    `unity-cmd — profile "${profile.name}" (${hostKindLabel(hostKind)})`,
    `${profile.host}:${profile.port}  host: ${hostKind}`,
  ];

  if (!connected) {
    lines.push('', 'Status: offline');
    if (reason) lines.push(`  ${reason}`);
    const hint = reachHint(hostKind);
    if (hint) lines.push('', hint);
    if (catalog?.commands?.length) {
      lines.push('', 'Commands (cached):', ...formatCommandList(catalog));
    } else {
      lines.push('', `Typical: ${OFFLINE_HINTS[hostKind] ?? OFFLINE_HINTS.editor}`);
    }
    lines.push('', `When online: unity-cmd --profile ${profile.name} help`);
    return lines.join('\n');
  }

  if (catalog?.connector_build != null) {
    lines.push(`  build: ${catalog.connector_build}`);
  }
  if (catalog?.catalog_version) {
    lines.push(`  catalog: ${catalog.catalog_version}${cached ? ' (cached)' : ''}`);
  }

  lines.push('', 'Meta: ping | list | help', '', 'Commands:', ...formatCommandList(catalog ?? { commands: [] }));

  lines.push('', 'Examples:', ...exampleLines(profile.name, hostKind));
  lines.push('  --refresh-catalog  refresh cache');

  return lines.join('\n');
}

/** @returns {Promise<import('./cli-result.js').CliResult>} */
export async function runHelp(flags, timeoutMs) {
  const profileName = resolveProfileName(flags);
  if (!profileName) {
    return cliResult(0, { stdout: formatLocalHelp({ profiles: listProfiles() }) });
  }

  const saved = loadProfile(profileName);
  if (!saved) {
    return cliResult(0, {
      stdout: [
        `Profile "${profileName}" not found.`,
        `Create: unity-cmd profile create ${profileName} --host 127.0.0.1 --port <port> --host-kind <editor|editor_play|player>`,
      ].join('\n'),
    });
  }

  const profile = { ...saved, name: profileName };
  const target = {
    profile: profileName,
    host: saved.host,
    port: saved.port,
    connector_host: normalizeHostKind(saved.connector_host ?? 'editor'),
  };

  const probeMs = Math.min(timeoutMs, 5000);
  let healthOk = false;
  let healthDetail = null;
  try {
    const res = await ping(target, { timeoutMs: probeMs, retryOnDisconnect: false });
    healthOk = res.ok && hostKindMatches(target.connector_host, res.data?.host);
    healthDetail = res.ok ? res.data : { status: res.status };
  } catch (err) {
    healthDetail = String(err?.cause?.code ?? err?.message ?? err);
  }

  if (healthOk) {
    try {
      const catalog = await loadCatalog(target, { timeoutMs, forceRefresh: true });
      return cliResult(0, {
        stdout: formatProfileHelp(profile, { catalog, connected: true }),
      });
    } catch {
      const cached = readCachedCatalog(target);
      return cliResult(0, {
        stdout: formatProfileHelp(profile, {
          connected: false,
          reason: 'catalog fetch failed',
          catalog: cached,
          cached: Boolean(cached),
        }),
      });
    }
  }

  const cached = readCachedCatalog(target);
  const reason =
    healthDetail && typeof healthDetail === 'object'
      ? `expected ${target.connector_host}, got ${healthDetail.host ?? 'none'}`
      : String(healthDetail ?? 'unreachable');
  return cliResult(0, {
    stdout: formatProfileHelp(profile, {
      connected: false,
      reason,
      catalog: cached,
      cached: Boolean(cached),
    }),
  });
}
