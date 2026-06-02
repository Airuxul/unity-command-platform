import { resolveTimeoutMs } from './runtime.js';
import { runHelp } from './help.js';
import { runProfileCommand } from './profile.js';
import { executeRemoteCommand } from './remote-command.js';
import { applyCliResult } from './cli-result.js';

function assignFlag(flags, key, value) {
  if (key === 'timeout') return { timeoutMs: Number.parseInt(value, 10) };
  flags[key] = value === '' ? true : value;
  return {};
}

export function parseArgs(argv) {
  const args = [...argv];
  let timeoutMs;
  const flags = {};
  const positional = [];

  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a.startsWith('--')) {
      const eq = a.indexOf('=');
      if (eq > 2) {
        const key = a.slice(2, eq);
        const value = a.slice(eq + 1);
        const parsed = assignFlag(flags, key, value);
        if (parsed.timeoutMs != null) timeoutMs = parsed.timeoutMs;
        continue;
      }
      if (a === '--timeout' && args[i + 1]) {
        timeoutMs = Number.parseInt(args[++i], 10);
        continue;
      }
      const key = a.slice(2);
      const next = args[i + 1];
      if (next && !next.startsWith('--')) {
        const parsed = assignFlag(flags, key, next);
        if (parsed.timeoutMs != null) timeoutMs = parsed.timeoutMs;
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

export async function runCommand(command, flags, timeoutMs, subArgs = []) {
  if (command === 'help' || flags.help === true) {
    const result = await runHelp(flags, timeoutMs);
    applyCliResult(result);
    return;
  }
  if (command === 'profile') {
    await runProfileCommand(subArgs, flags, timeoutMs);
    return;
  }
  const result = await executeRemoteCommand(command, flags, timeoutMs);
  applyCliResult(result);
}
