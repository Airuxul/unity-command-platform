import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

/** Resolve package root from this module location (src/shared or dist/shared). */
export function resolvePackageRoot(): string {
  return path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
}

export function resolveHostBin(): string {
  return path.join(resolvePackageRoot(), "bin", "ucp-host.js");
}

/**
 * When dist/ was last built (tsup writes dist/build.json); 0 if never. Source and dist read the
 * same file, so a host started before a rebuild reports an older id than the CLI on disk.
 */
export function readBuildId(): number {
  try {
    const file = path.join(resolvePackageRoot(), "dist", "build.json");
    const build = Number((JSON.parse(fs.readFileSync(file, "utf8")) as { build?: unknown }).build);
    return Number.isFinite(build) ? build : 0;
  } catch {
    return 0;
  }
}
