import { waitForProfileReady } from './client/connector-readiness.js';
import { buildReachabilityDiagnostics } from './client/instance-diagnostics.js';
import { loadProfile } from './client/connection.js';
import { cliError } from './errors.js';
import { cliResult } from './cli-result.js';
import { resolveProfileName } from './remote-command.js';

/**
 * Block until the profile's Editor connector is reachable and ready.
 * @param {object} flags
 * @param {number} timeoutMs
 */
export async function executeWaitCommand(flags, timeoutMs) {
  const profileName = resolveProfileName(flags);
  if (!profileName) {
    return cliResult(1, {
      json: cliError('Profile is required. Use --profile <name>.', 'NO_PROFILE'),
    });
  }

  const saved = loadProfile(profileName);
  if (!saved?.host || !saved?.port) {
    return cliResult(1, {
      json: cliError(`Profile not found: ${profileName}`, 'NO_PROFILE'),
    });
  }

  const target = await waitForProfileReady({
    profile: profileName,
    timeoutMs,
    logProgress: flags.quiet !== true,
  });

  if (!target?.host) {
    const diagnostics = await buildReachabilityDiagnostics({
      profile: profileName,
      host: saved.host,
      port: saved.port,
      connector_host: saved.connector_host,
      projectPath: process.cwd(),
    });
    return cliResult(1, {
      json: cliError(
        `Profile "${profileName}" not ready within ${timeoutMs}ms.`,
        'EDITOR_NOT_READY',
        diagnostics.hint,
        { profile: profileName, diagnostics },
      ),
    });
  }

  return cliResult(0, {
    json: {
      ok: true,
      profile: profileName,
      target: {
        host: target.host,
        port: target.port,
        connector_host: target.connector_host,
        connector_build: target.connector_build,
      },
    },
  });
}
