/**
 * @typedef {object} CliResult
 * @property {number} exitCode
 * @property {string} [stdout]
 * @property {string} [stderr]
 * @property {object} [json]
 */

/** @param {number} exitCode @param {{ stdout?: string, stderr?: string, json?: object }} [body] */
export function cliResult(exitCode, { stdout, stderr, json } = {}) {
  return { exitCode, stdout, stderr, json };
}

/** @param {CliResult} result */
export function applyCliResult(result) {
  if (result.stderr) {
    const err = result.stderr.endsWith('\n') ? result.stderr : `${result.stderr}\n`;
    process.stderr.write(err);
  }
  if (result.json != null) {
    console.log(JSON.stringify(result.json, null, 2));
  } else if (result.stdout != null) {
    const out = result.stdout.endsWith('\n') ? result.stdout : `${result.stdout}\n`;
    process.stdout.write(out);
  }
  process.exit(result.exitCode);
}
