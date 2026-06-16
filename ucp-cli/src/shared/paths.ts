import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

export function resolveUcpRoot(override?: string): string {
  if (override?.trim()) return path.resolve(override);
  const env = process.env.UCP_ROOT?.trim();
  if (env) return path.resolve(env);
  return path.join(os.homedir(), ".ucp");
}

export function projectIdFromPath(projectPath: string): string {
  const normalized = path.resolve(projectPath).replace(/\\/g, "/").toLowerCase();
  return crypto.createHash("sha256").update(normalized).digest("hex").slice(0, 16);
}

export function ucpLayout(ucpRoot?: string) {
  const root = resolveUcpRoot(ucpRoot);
  return {
    root,
    hostJson: path.join(root, "host.json"),
    hostLock: path.join(root, "host.lock"),
    sessionsDir: path.join(root, "sessions"),
    queuesDir: path.join(root, "queues"),
  };
}

export function queueLayout(projectId: string, ucpRoot?: string) {
  const { queuesDir } = ucpLayout(ucpRoot);
  const base = path.join(queuesDir, projectId);
  return {
    base,
    inbox: path.join(base, "inbox"),
    outbox: path.join(base, "outbox"),
  };
}

export function sessionFilePath(projectId: string, ucpRoot?: string): string {
  const { sessionsDir } = ucpLayout(ucpRoot);
  return path.join(sessionsDir, `${projectId}.json`);
}

export function ensureDir(dir: string): void {
  fs.mkdirSync(dir, { recursive: true });
}

export function writeJsonAtomic(filePath: string, data: unknown): void {
  ensureDir(path.dirname(filePath));
  const tmp = `${filePath}.${process.pid}.tmp`;
  fs.writeFileSync(tmp, `${JSON.stringify(data, null, 2)}\n`, "utf8");
  fs.renameSync(tmp, filePath);
}

export function readJsonFile(filePath: string): unknown | null {
  if (!fs.existsSync(filePath)) return null;
  try {
    let text = fs.readFileSync(filePath, "utf8");
    // Unity File.WriteAllText emits UTF-8 BOM; strip so JSON.parse succeeds.
    if (text.charCodeAt(0) === 0xfeff) {
      text = text.slice(1);
    }
    return JSON.parse(text);
  } catch {
    return null;
  }
}
