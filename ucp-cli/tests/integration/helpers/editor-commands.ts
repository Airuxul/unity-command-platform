import { expect } from "vitest";
import { ensureUcpHost, executeCommand } from "#cli/client/ucp-host-client.js";
import { DEFAULT_HOST_PORT } from "#shared/index.js";
import type { CliResult } from "#shared/types.js";

export interface EditorCommandCase {
  type: string;
  description: string;
  args?: Record<string, unknown>;
  timeout?: number;
  project?: string;
  skip?: boolean;
  assert: (result: CliResult) => void;
}

export const EDITOR_COMMAND_CASES: EditorCommandCase[] = [
  {
    type: "ping",
    description: "health check",
    assert: (r) => {
      expect(r.ok).toBe(true);
      expect(r.message).toBe("pong");
    },
  },
  {
    type: "echo",
    description: "echo message",
    args: { message: "ucp-integration-test" },
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(data?.message).toBe("ucp-integration-test");
      expect(data?.channel).toBe("editor");
    },
  },
  {
    type: "state",
    description: "agent/editor state snapshot",
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(data?.play_mode).toBeTruthy();
      expect(data?.agent_state).toBeTruthy();
    },
  },
  {
    type: "console",
    description: "read console entries",
    args: { type: "log,warning,error", lines: 5, stacktrace: "none" },
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(Array.isArray(data?.entries)).toBe(true);
    },
  },
  {
    type: "refresh",
    description: "asset database refresh without compile",
    args: { compile: false, force: false },
    timeout: 60_000,
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(data?.refreshed).toBe(true);
    },
  },
  {
    type: "screenshot",
    description: "capture game view",
    args: { view: "game", width: 320, height: 240 },
    timeout: 30_000,
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(data?.path).toBeTruthy();
    },
  },
  {
    type: "profiler",
    description: "profiler status",
    args: { action: "status" },
    assert: (r) => {
      expect(r.ok).toBe(true);
    },
  },
  {
    type: "manage",
    description: "editor manage refresh action",
    args: { action: "refresh" },
    assert: (r) => {
      expect(r.ok).toBe(true);
    },
  },
  {
    type: "reserialize",
    description: "reserialize project metadata only (empty path list)",
    args: { paths: "" },
    timeout: 120_000,
    skip: true,
    assert: (r) => {
      expect(r.ok).toBe(true);
    },
  },
  {
    type: "menu",
    description: "execute menu item",
    args: { menu_path: "Window/General/Console" },
    assert: (r) => {
      expect(r.ok).toBe(true);
    },
  },
  {
    type: "compile",
    description: "script compilation (deferred)",
    args: { timeout_ms: 120_000 },
    timeout: 180_000,
    skip: true,
    assert: (r) => {
      expect(r.ok).toBe(true);
    },
  },
  {
    type: "play",
    description: "enter play mode (deferred)",
    timeout: 60_000,
    skip: true,
    assert: (r) => {
      expect(r.ok).toBe(true);
    },
  },
  {
    type: "stop",
    description: "exit play mode (deferred)",
    timeout: 60_000,
    skip: true,
    assert: (r) => {
      expect(r.ok).toBe(true);
    },
  },
  {
    type: "exec",
    description: "execute C# snippet",
    args: { code: "return 42;" },
    timeout: 30_000,
    skip: true,
    assert: (r) => {
      expect(r.ok).toBe(true);
    },
  },
  {
    type: "build",
    description: "UCP build skeleton",
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(data?.implemented).toBe(false);
    },
  },
];

export function gameDemoProjectPath(): string {
  return process.env.UCP_TEST_PROJECT ?? "C:/Project/GameDemo";
}

export async function runEditorCommand(testCase: EditorCommandCase): Promise<CliResult> {
  return executeCommand(testCase.type, {
    project: testCase.project ?? gameDemoProjectPath(),
    sessionType: "editor",
    timeout: testCase.timeout ?? 30_000,
    args: testCase.args ?? {},
  });
}

export async function hasReadyEditorSession(port = DEFAULT_HOST_PORT): Promise<boolean> {
  try {
    await ensureUcpHost({ ucpRoot: process.env.UCP_ROOT });
    const res = await fetch(`http://127.0.0.1:${port}/sessions`);
    const body = (await res.json()) as {
      sessions?: Array<{ status?: string; type?: string }>;
    };
    return (body.sessions ?? []).some((s) => s.status === "ready" && s.type === "editor");
  } catch {
    return false;
  }
}
