import fs from "node:fs";
import path from "node:path";
import {
  loadTargets,
  projectIdFromPath,
  readJsonFile,
  sessionFilePath,
  ucpLayout,
} from "#shared/index.js";
import { ucpSessionSchema } from "#shared/schema.js";
import type {
  EditorTarget,
  RuntimeTarget,
  UcpSession,
  UcpTarget,
  UcpTargetStatus,
} from "#shared/types.js";

const STALE_MS = 30_000;
const PROBE_TIMEOUT_MS = 3_000;

export interface RuntimeHealthResponse {
  ok?: boolean;
  type?: string;
  capabilities?: string[];
  name?: string;
  path?: string;
  product?: string;
}

export function discoverEditorTargetsFromSessions(ucpRoot?: string): EditorTarget[] {
  const { sessionsDir } = ucpLayout(ucpRoot);
  if (!fs.existsSync(sessionsDir)) return [];

  const targets: EditorTarget[] = [];
  for (const file of fs.readdirSync(sessionsDir).filter((f) => f.endsWith(".json"))) {
    if (file.includes("-runtime")) continue;

    const data = readJsonFile(path.join(sessionsDir, file));
    const parsed = ucpSessionSchema.safeParse(data);
    if (!parsed.success || parsed.data.type !== "editor") continue;

    const session = parsed.data;
    const queueId = file.replace(/\.json$/, "");
    targets.push({
      id: `editor-${queueId}`,
      type: "editor",
      name: session.name,
      projectPath: session.path,
      queueId,
    });
  }
  return targets;
}

export function mergeTargets(configured: UcpTarget[], ucpRoot?: string): UcpTarget[] {
  const result: UcpTarget[] = [...configured];
  const configuredQueueIds = new Set(
    configured
      .filter((t): t is EditorTarget => t.type === "editor")
      .map((t) => t.queueId ?? projectIdFromPath(t.projectPath)),
  );

  for (const auto of discoverEditorTargetsFromSessions(ucpRoot)) {
    const queueId = auto.queueId ?? projectIdFromPath(auto.projectPath);
    if (!configuredQueueIds.has(queueId)) {
      result.push(auto);
    }
  }

  return result;
}

export function listConfiguredAndDiscoveredTargets(ucpRoot?: string): UcpTarget[] {
  return mergeTargets(loadTargets(ucpRoot), ucpRoot);
}

export async function probeEditorTarget(
  target: EditorTarget,
  ucpRoot?: string,
): Promise<UcpTargetStatus> {
  const queueId = target.queueId ?? projectIdFromPath(target.projectPath);
  const sessionPath = sessionFilePath(queueId, ucpRoot);
  const data = readJsonFile(sessionPath);
  const parsed = ucpSessionSchema.safeParse(data);
  const now = new Date().toISOString();

  if (!parsed.success) {
    return {
      target,
      status: "offline",
      reachable: false,
      capabilities: [],
      probedAt: now,
      error: "editor_session_missing",
    };
  }

  const session = parsed.data;
  const updatedAt = Date.parse(session.updatedAt || "");
  const fresh = Number.isFinite(updatedAt) && Date.now() - updatedAt <= STALE_MS;
  const ready = fresh && session.status === "ready";

  return {
    target,
    status: ready ? "ready" : "offline",
    reachable: fresh,
    capabilities: session.capabilities ?? [],
    probedAt: now,
    error: ready ? undefined : "editor_not_ready",
  };
}

export async function probeRuntimeTarget(target: RuntimeTarget): Promise<UcpTargetStatus> {
  const now = new Date().toISOString();
  const url = `http://${target.host}:${target.port}/health`;

  try {
    const res = await fetch(url, { signal: AbortSignal.timeout(PROBE_TIMEOUT_MS) });
    const body = (await res.json()) as RuntimeHealthResponse;
    const ready = res.ok && body?.ok === true;

    return {
      target,
      status: ready ? "ready" : "offline",
      reachable: res.ok,
      capabilities: Array.isArray(body?.capabilities) ? body.capabilities : [],
      probedAt: now,
      error: ready ? undefined : String(body?.type ?? "runtime_not_ready"),
    };
  } catch (err) {
    return {
      target,
      status: "offline",
      reachable: false,
      capabilities: [],
      probedAt: now,
      error: err instanceof Error ? err.message : String(err),
    };
  }
}

export async function probeTarget(target: UcpTarget, ucpRoot?: string): Promise<UcpTargetStatus> {
  if (target.type === "editor") {
    return probeEditorTarget(target, ucpRoot);
  }
  return probeRuntimeTarget(target);
}

export function targetStatusToSession(status: UcpTargetStatus): UcpSession | null {
  const { target } = status;
  const projectPath = target.type === "editor" ? target.projectPath : (target.projectPath ?? "");

  if (target.type === "editor") {
    if (status.status !== "ready") return null;
    const queueId = target.queueId ?? projectIdFromPath(target.projectPath);
    return {
      id: target.id,
      name: target.name ?? path.basename(target.projectPath),
      path: projectPath,
      type: "editor",
      status: status.status,
      capabilities: status.capabilities,
      updatedAt: status.probedAt,
      queueId,
    };
  }

  if (status.status !== "ready") return null;

  return {
    id: target.id,
    name: target.name ?? `${target.host}:${target.port}`,
    path: projectPath,
    type: "runtime",
    status: status.status,
    capabilities: status.capabilities,
    updatedAt: status.probedAt,
    host: target.host,
    runtimePort: target.port,
  };
}

export async function probeAllTargets(ucpRoot?: string): Promise<UcpTargetStatus[]> {
  const targets = listConfiguredAndDiscoveredTargets(ucpRoot);
  return Promise.all(targets.map((target) => probeTarget(target, ucpRoot)));
}
