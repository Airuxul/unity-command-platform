import path from "node:path";
import { projectIdFromPath, readJsonFile, ucpLayout, writeJsonAtomic } from "./paths.js";
import { targetsFileSchema } from "./schema.js";
import type { UcpTarget } from "./types.js";

export function targetsFilePath(ucpRoot?: string): string {
  return path.join(ucpLayout(ucpRoot).root, "targets.json");
}

export function loadTargets(ucpRoot?: string): UcpTarget[] {
  const data = readJsonFile(targetsFilePath(ucpRoot));
  const parsed = targetsFileSchema.safeParse(data ?? { targets: [] });
  if (!parsed.success) return [];
  return parsed.data.targets;
}

export function saveTargets(targets: UcpTarget[], ucpRoot?: string): void {
  writeJsonAtomic(targetsFilePath(ucpRoot), { targets });
}

export function getTarget(id: string, ucpRoot?: string): UcpTarget | null {
  return loadTargets(ucpRoot).find((t) => t.id === id) ?? null;
}

export function upsertTarget(target: UcpTarget, ucpRoot?: string): UcpTarget {
  const normalized = normalizeTarget(target);
  const targets = loadTargets(ucpRoot);
  const index = targets.findIndex((t) => t.id === normalized.id);
  if (index >= 0) {
    targets[index] = normalized;
  } else {
    targets.push(normalized);
  }
  saveTargets(targets, ucpRoot);
  return normalized;
}

export function removeTarget(id: string, ucpRoot?: string): boolean {
  const targets = loadTargets(ucpRoot);
  const next = targets.filter((t) => t.id !== id);
  if (next.length === targets.length) return false;
  saveTargets(next, ucpRoot);
  return true;
}

export function normalizeTarget(target: UcpTarget): UcpTarget {
  if (target.type === "editor") {
    const projectPath = path.resolve(target.projectPath).replace(/\\/g, "/");
    const queueId = target.queueId ?? projectIdFromPath(projectPath);
    return {
      ...target,
      projectPath,
      queueId,
      name: target.name ?? path.basename(projectPath),
    };
  }

  return {
    ...target,
    host: target.host.trim(),
    name: target.name ?? `${target.host}:${target.port}`,
    projectPath: target.projectPath
      ? path.resolve(target.projectPath).replace(/\\/g, "/")
      : undefined,
  };
}
