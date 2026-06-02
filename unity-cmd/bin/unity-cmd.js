#!/usr/bin/env node
import { parseArgs, runCommand } from '../src/cli.js';
import { formatLocalHelp } from '../src/help.js';

const { positional, flags, timeoutMs } = parseArgs(process.argv.slice(2));
const command = positional[0] ?? (flags.help ? 'help' : null);

if (!command) {
  console.log(formatLocalHelp());
  process.exit(0);
}

await runCommand(command, flags, timeoutMs, positional.slice(1));
