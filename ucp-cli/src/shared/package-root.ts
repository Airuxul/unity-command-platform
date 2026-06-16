import path from "node:path";
import { fileURLToPath } from "node:url";

/** Resolve package root from this module location (src/shared or dist/shared). */
export function resolvePackageRoot(): string {
  return path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
}

export function resolveHostBin(): string {
  return path.join(resolvePackageRoot(), "bin", "ucp-host.js");
}
