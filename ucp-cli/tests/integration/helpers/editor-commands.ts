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
  /** The command is expected to fail — used to pin down error handling, not just happy paths. */
  expectFailure?: boolean;
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
    type: "help",
    description: "editor command catalog",
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      const commands = data?.commands as Array<{ name: string }> | undefined;
      expect(Array.isArray(commands)).toBe(true);
      // The catalog is the discovery pass itself, so a missing family means a command class
      // failed to register rather than a doc being out of date.
      const names = (commands ?? []).map((c) => c.name);
      for (const expected of [
        "prefab_create",
        "gameobject_create",
        "component_set",
        "scene_info",
      ]) {
        expect(names).toContain(expected);
      }
    },
  },
  {
    type: "tag_list",
    description: "list project tags",
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(Array.isArray(data?.tags)).toBe(true);
    },
  },
  {
    type: "layer_list",
    description: "list project layers",
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(Array.isArray(data?.layers)).toBe(true);
    },
  },
  {
    type: "tool_set",
    description: "set the active transform tool",
    args: { tool: "move" },
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(data?.tool).toBe("Move");
    },
  },
  {
    type: "ui_snapshot",
    description: "ui_snapshot is rejected outside play mode",
    expectFailure: true,
    assert: (r) => {
      expect(r.ok).toBe(false);
      expect(String(r.message ?? r.error)).toContain("play mode");
    },
  },
  {
    type: "pause",
    description: "pause is rejected outside play mode",
    expectFailure: true,
    assert: (r) => {
      expect(r.ok).toBe(false);
      expect(String(r.message ?? r.error)).toContain("play mode");
    },
  },
  {
    type: "scene_info",
    description: "active and loaded scenes",
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(Array.isArray(data?.loaded_scenes)).toBe(true);
    },
  },
  {
    type: "scene_list",
    description: "scene assets in the project",
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(Array.isArray(data?.scenes)).toBe(true);
    },
  },
  {
    type: "asset_list",
    description: "assets under Assets/",
    args: { folder: "Assets", limit: 10 },
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(Array.isArray(data?.assets)).toBe(true);
    },
  },
  {
    type: "asset_find",
    description: "raw AssetDatabase filter search",
    args: { filter: "t:Scene", limit: 5 },
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(Array.isArray(data?.assets)).toBe(true);
    },
  },
  {
    type: "prefab_list",
    description: "prefab assets under Assets/",
    args: { folder: "Assets", limit: 10 },
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(Array.isArray(data?.prefabs)).toBe(true);
    },
  },
  {
    type: "prefab_stage_info",
    description: "whether a prefab is open in Prefab Mode",
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(typeof data?.open).toBe("boolean");
    },
  },
  {
    type: "selection_get",
    description: "current editor selection",
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(typeof data?.count).toBe("number");
    },
  },
  {
    type: "selection_clear",
    description: "clear the editor selection",
    assert: (r) => {
      expect(r.ok).toBe(true);
      const data = r.data as Record<string, unknown> | null;
      expect(data?.count).toBe(0);
    },
  },
  {
    type: "gameobject_find",
    description: "search the active scene by name",
    args: { name: "", exact: false, limit: 5 },
    expectFailure: true,
    assert: (r) => {
      // Every filter empty is a caller mistake, not an empty result set.
      expect(r.ok).toBe(false);
      expect(String(r.message ?? r.error)).toContain("filter");
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

/**
 * Every command type the Editor agent advertises. Kept here rather than derived so that adding a
 * command is a deliberate two-step: the constant lands in CommandNames.cs, and a test case has to
 * appear in EDITOR_COMMAND_CASES or SCENARIO_COMMANDS before this list will match again.
 */
export const ALL_EDITOR_COMMANDS: readonly string[] = [
  // agent / editor control
  "build",
  "compile",
  "console",
  "echo",
  "exec",
  "help",
  "menu",
  "pause",
  "ping",
  "play",
  "profiler",
  "refresh",
  "reserialize",
  "screenshot",
  "state",
  "stop",
  "ui_snapshot",
  // project settings
  "layer_add",
  "layer_list",
  "layer_remove",
  "tag_add",
  "tag_list",
  "tag_remove",
  "tool_set",
  // prefab assets
  "prefab_create",
  "prefab_create_empty",
  "prefab_create_variant",
  "prefab_delete",
  "prefab_duplicate",
  "prefab_edit",
  "prefab_info",
  "prefab_list",
  "prefab_move",
  "prefab_tree",
  // prefab instances
  "prefab_apply",
  "prefab_instance_info",
  "prefab_instantiate",
  "prefab_overrides",
  "prefab_replace",
  "prefab_revert",
  "prefab_unpack",
  // prefab mode
  "prefab_close",
  "prefab_open",
  "prefab_save",
  "prefab_stage_info",
  // gameobjects
  "gameobject_activate",
  "gameobject_create",
  "gameobject_delete",
  "gameobject_duplicate",
  "gameobject_find",
  "gameobject_info",
  "gameobject_layer",
  "gameobject_rename",
  "gameobject_reparent",
  "gameobject_tag",
  "gameobject_transform",
  "gameobject_tree",
  // components
  "component_add",
  "component_get",
  "component_list",
  "component_remove",
  "component_set",
  // scenes
  "scene_close",
  "scene_edit",
  "scene_info",
  "scene_list",
  "scene_new",
  "scene_open",
  "scene_save",
  "scene_save_as",
  // assets
  "asset_create_folder",
  "asset_delete",
  "asset_dependencies",
  "asset_duplicate",
  "asset_find",
  "asset_guid",
  "asset_info",
  "asset_list",
  "asset_move",
  // selection
  "selection_clear",
  "selection_get",
  "selection_set",
];

/** Alias names that must dispatch as well as their canonical command. */
export const EDITOR_COMMAND_ALIASES: readonly string[] = [
  "asset_rename",
  "commands",
  "prefab_rename",
  "recompile",
  "reload",
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
