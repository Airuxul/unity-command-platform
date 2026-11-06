import { readFileSync } from "node:fs";
import { expect } from "vitest";
import type { CliResult } from "#shared/types.js";
import { ALL_EDITOR_COMMANDS, EDITOR_COMMAND_ALIASES } from "./editor-commands.js";

export type CommandHost = "editor" | "runtime";

const BOTH: readonly CommandHost[] = ["editor", "runtime"];

export interface RuntimeCommandCase {
  type: string;
  description: string;
  args?: Record<string, unknown>;
  hosts: readonly CommandHost[];
  assert: (host: CommandHost, result: CliResult) => void;
}

interface CatalogCommand {
  name: string;
  description?: string;
  aliases?: string[];
}

const runtimeCatalog = JSON.parse(
  readFileSync(new URL("../__snapshots__/catalog.runtime.commands.json", import.meta.url), "utf8"),
) as { commands: CatalogCommand[]; alias_to_command?: Record<string, string> };

/** Names from the committed runtime help catalog. A new runtime command updates that snapshot. */
export const ALL_RUNTIME_COMMANDS: readonly string[] = runtimeCatalog.commands.map((c) => c.name);

export const RUNTIME_COMMAND_ALIASES: readonly string[] = runtimeCatalog.commands.flatMap(
  (c) => c.aliases ?? [],
);

/** Same command name is registered for the editor host and the runtime host. */
export const BOTH_HOST_COMMANDS: readonly string[] = ALL_RUNTIME_COMMANDS.filter((name) =>
  ALL_EDITOR_COMMANDS.includes(name),
);

export const BOTH_HOST_ALIASES: readonly string[] = RUNTIME_COMMAND_ALIASES.filter((alias) =>
  EDITOR_COMMAND_ALIASES.includes(alias),
);

function commandNames(result: CliResult): string[] {
  const data = (result.data ?? {}) as { commands?: Array<{ name: string }> };
  return (data.commands ?? []).map((c) => c.name).sort();
}

function card(result: CliResult): { name?: string; description?: string } {
  return (
    ((result.data ?? {}) as { command?: { name?: string; description?: string } }).command ?? {}
  );
}

export const RUNTIME_COMMAND_CASES: readonly RuntimeCommandCase[] = [
  {
    type: "ping",
    description: "answers pong on either host",
    hosts: BOTH,
    assert: (_host, result) => {
      expect(result.ok, JSON.stringify(result)).toBe(true);
      expect(result.message).toBe("pong");
    },
  },
  {
    type: "echo",
    description: "echoes on the host it was sent to",
    args: { message: "both-sides" },
    hosts: BOTH,
    assert: (host, result) => {
      expect(result.ok, JSON.stringify(result)).toBe(true);
      expect(result.message).toBe("echo ok");
      const data = result.data as { channel?: string; message?: string } | null;
      expect(data?.channel).toBe(host);
      expect(data?.message).toBe("both-sides");
    },
  },
  {
    type: "echo",
    description: "rejects a parameter it does not declare",
    args: { nope: true },
    hosts: BOTH,
    assert: (_host, result) => {
      expect(result.ok).toBe(false);
      expect(result.error_code).toBe("invalid_argument");
      expect(result.message).toContain("nope");
      expect(result.message).toContain("message");
    },
  },
  {
    type: "call",
    description: "lists the [UcpFunction] methods that host can run",
    hosts: BOTH,
    assert: (_host, result) => {
      expect(result.ok, JSON.stringify(result)).toBe(true);
      const data = result.data as { count?: number; functions?: unknown[] } | null;
      expect(Array.isArray(data?.functions)).toBe(true);
      expect(data?.count).toBe(data?.functions?.length);
    },
  },
  {
    type: "help",
    description: "lists that host's commands",
    hosts: BOTH,
    assert: (host, result) => {
      expect(result.ok, JSON.stringify(result)).toBe(true);
      const names = commandNames(result);
      for (const name of BOTH_HOST_COMMANDS) expect(names).toContain(name);
      if (host === "runtime") expect(names).toEqual([...ALL_RUNTIME_COMMANDS].sort());
    },
  },
  {
    type: "help",
    description: "returns the card of the echo command on that host",
    args: { command: "echo" },
    hosts: BOTH,
    assert: (host, result) => {
      expect(result.ok, JSON.stringify(result)).toBe(true);
      const described = card(result);
      expect(described.name).toBe("echo");
      expect(described.description).toBe(
        host === "runtime" ? "Echo from Runtime host" : "Echo from Editor host",
      );
    },
  },
  {
    type: "?",
    description: "help alias lists that host's commands",
    hosts: BOTH,
    assert: (host, result) => {
      expect(result.ok, JSON.stringify(result)).toBe(true);
      const names = commandNames(result);
      if (host === "runtime") expect(names).toEqual([...ALL_RUNTIME_COMMANDS].sort());
      else expect(names).toEqual([...ALL_EDITOR_COMMANDS].sort());
    },
  },
  {
    type: "commands",
    description: "editor help alias; the runtime host does not advertise it",
    hosts: BOTH,
    assert: (host, result) => {
      if (host === "runtime") {
        expect(result.ok).toBe(false);
        expect(result.error_code).toBe("host_unknown_command");
        return;
      }
      expect(result.ok, JSON.stringify(result)).toBe(true);
      expect(commandNames(result)).toEqual([...ALL_EDITOR_COMMANDS].sort());
    },
  },
];
