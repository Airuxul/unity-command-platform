import type { CommandChains, CommandLink } from "#shared/types.js";

export interface ChainStep {
  type: string;
  role: "before" | "main" | "after";
}

export class CommandChainError extends Error {}

/** Before-commands, then this command, then after-commands. Nested links expand in place. */
export function expandCommandChain(name: string, chains?: CommandChains): ChainStep[] {
  const steps: ChainStep[] = [];
  const stack: string[] = [];

  const walk = (type: string, role: ChainStep["role"]) => {
    const key = type.toLowerCase();
    if (stack.includes(key)) {
      throw new CommandChainError(
        `Command chain cycles at '${type}' (${[...stack, key].join(" -> ")}).`,
      );
    }
    stack.push(key);
    const link = lookup(chains, type);
    for (const before of link?.before ?? []) walk(before, "before");
    steps.push({ type, role });
    for (const after of link?.after ?? []) walk(after, "after");
    stack.pop();
  };

  walk(name, "main");
  return steps;
}

function lookup(chains: CommandChains | undefined, type: string): CommandLink | undefined {
  if (!chains) return undefined;
  if (chains[type]) return chains[type];
  const key = type.toLowerCase();
  for (const [name, link] of Object.entries(chains)) {
    if (name.toLowerCase() === key) return link;
  }
  return undefined;
}
