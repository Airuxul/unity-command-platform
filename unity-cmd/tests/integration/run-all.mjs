import net from 'node:net';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { loadProfile, DEFAULT_PLAYER_PORT } from '../../src/client/connection.js';

const RUNNER = fileURLToPath(new URL('./runner.mjs', import.meta.url));

async function main() {
  await runScenario('editor-lifecycle', process.env.UNITY_CMD_PROFILE ?? 'editor');

  const playerProfileName = process.env.UNITY_CMD_PLAYER_PROFILE ?? 'package-play';
  const playerProfile = loadProfile(playerProfileName);
  if (!playerProfile?.host || !playerProfile?.port) {
    console.log(
      `[integration] skip player-runtime: profile '${playerProfileName}' missing or invalid (requires host+port).`,
    );
    return;
  }

  const listening = await isPortListening(playerProfile.host, playerProfile.port, 1000);
  if (!listening) {
    console.log(
      `[integration] skip player-runtime: ${playerProfile.host}:${playerProfile.port} is not listening.`,
    );
    return;
  }

  await runScenario('player-runtime', playerProfileName);
}

function runScenario(scenario, profile) {
  return new Promise((resolve, reject) => {
    console.log(`[integration] run scenario=${scenario} profile=${profile}`);
    const child = spawn(process.execPath, [RUNNER], {
      env: {
        ...process.env,
        UNITY_CMD_SCENARIO: scenario,
        UNITY_CMD_PROFILE: profile,
      },
      stdio: 'inherit',
    });
    child.on('error', reject);
    child.on('close', (code) => {
      if (code === 0) return resolve();
      reject(new Error(`scenario '${scenario}' failed with exit code ${code}`));
    });
  });
}

function isPortListening(host, port, timeoutMs = 1000) {
  return new Promise((resolve) => {
    const socket = new net.Socket();
    let settled = false;

    const finish = (result) => {
      if (settled) return;
      settled = true;
      socket.destroy();
      resolve(result);
    };

    socket.setTimeout(timeoutMs);
    socket.once('connect', () => finish(true));
    socket.once('timeout', () => finish(false));
    socket.once('error', () => finish(false));
    socket.connect(port ?? DEFAULT_PLAYER_PORT, host ?? '127.0.0.1');
  });
}

main().catch((err) => {
  console.error(err.message ?? err);
  process.exit(1);
});
