import type { CliResult } from "#shared/types.js";

export function printResult(result: unknown): void {
  console.log(JSON.stringify(result, null, 2));
}

/**
 * Sets the exit code rather than calling process.exit: on Windows, Node 24 aborts with a libuv
 * assertion (exit code 127) when it exits while fetch's sockets are still closing.
 */
export function exitFromResult(result: unknown): void {
  const ok = Boolean(
    result && typeof result === "object" && "ok" in result && (result as CliResult).ok,
  );
  process.exitCode = ok ? 0 : 1;
}
