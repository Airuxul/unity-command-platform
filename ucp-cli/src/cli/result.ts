import type { CliResult } from "#shared/types.js";

export function printResult(result: unknown): void {
  console.log(JSON.stringify(result, null, 2));
}

export function exitFromResult(result: unknown): never {
  const ok = Boolean(
    result && typeof result === "object" && "ok" in result && (result as CliResult).ok,
  );
  process.exit(ok ? 0 : 1);
}
