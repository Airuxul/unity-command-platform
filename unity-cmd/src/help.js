const STATIC_HELP = `unity-cmd — send commands to unity-connector

Local commands (no catalog):
  ping, list, help

Common:
  unity-cmd recompile
  unity-cmd console [--type error,warning] [--lines 20]
  unity-cmd list [--refresh-catalog]

Environment:
  UNITY_CMD_PROJECT, UNITY_CMD_TIMEOUT_MS (default 20000)
`;

/**
 * @param {import('./catalog.js').CatalogIndex | null} catalog
 */
export function formatHelp(catalog) {
  if (!catalog?.commands?.length) {
    return `${STATIC_HELP}\n(No live catalog — open Unity and run unity-cmd list)`;
  }

  const lines = [
    'unity-cmd — commands from Unity Editor (live catalog)',
    `catalog_version: ${catalog.catalog_version ?? 'unknown'}`,
    catalog.connector_build != null ? `connector_build: ${catalog.connector_build}` : null,
    '',
    'Local: ping | list | help',
    '',
    'Commands:',
  ].filter(Boolean);

  const sorted = [...catalog.commands].sort((a, b) =>
    (a.name ?? '').localeCompare(b.name ?? ''),
  );

  for (const entry of sorted) {
    const aliases =
      entry.aliases?.length > 0 ? ` (aliases: ${entry.aliases.join(', ')})` : '';
    const job = entry.is_job ? ' [job]' : '';
    const desc = entry.description ? ` — ${entry.description}` : '';
    lines.push(`  ${entry.name}${aliases}${job}${desc}`);
  }

  lines.push(
    '',
    'Examples:',
    '  unity-cmd console --type error,warning --lines 20',
    '  unity-cmd recompile --timeout 120000',
    '  unity-cmd connector.state',
    '  unity-cmd <command> [--key value] [--timeout ms]',
    '',
    'Flags: --refresh-catalog  force refresh command catalog cache',
  );

  return lines.join('\n');
}
