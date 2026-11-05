import fs from "node:fs";
import type { CliResult } from "#shared/types.js";
import type { ExecuteCommandOptions } from "../client/ucp-host-client.js";

/**
 * `ucp-cli run-script flow.json`: commands run one after another from the CLI, each through
 * ucp-host like a single `run`, so play / stop / compile resume across domain reloads exactly as
 * they do on their own. The first failure stops the flow.
 */
export interface ScriptStep {
  command: string;
  args?: Record<string, unknown>;
  /** Milliseconds; default: --timeout, else the command's own default. */
  timeout?: number;
  session_type?: "editor" | "runtime";
}

export interface StepOutcome {
  index: number;
  command: string;
  ok: boolean;
  duration_ms: number;
  message?: string;
  error_code?: string;
  hint?: string;
  data?: unknown;
}

export type Executor = (type: string, options: ExecuteCommandOptions) => Promise<CliResult>;

/** Accepts `[step, ...]` or `{ "steps": [step, ...] }`; throws with the step at fault. */
export function parseScript(text: string): ScriptStep[] {
  const parsed: unknown = JSON.parse(text);
  const steps = Array.isArray(parsed)
    ? parsed
    : isObject(parsed) && Array.isArray(parsed.steps)
      ? parsed.steps
      : null;
  if (!steps) throw new Error('expected an array of steps, or { "steps": [...] }');
  if (steps.length === 0) throw new Error("the script has no steps");

  return steps.map((step, i) => {
    const where = `steps[${i}]`;
    if (!isObject(step) || typeof step.command !== "string" || step.command.trim() === "") {
      throw new Error(`${where}: needs a "command" string`);
    }
    if (step.args !== undefined && !isObject(step.args)) {
      throw new Error(`${where}: "args" must be an object`);
    }
    if (
      step.timeout !== undefined &&
      !(typeof step.timeout === "number" && Number.isInteger(step.timeout) && step.timeout > 0)
    ) {
      throw new Error(`${where}: "timeout" must be a positive number of milliseconds`);
    }
    if (
      step.session_type !== undefined &&
      step.session_type !== "editor" &&
      step.session_type !== "runtime"
    ) {
      throw new Error(`${where}: "session_type" must be "editor" or "runtime"`);
    }
    return {
      command: step.command.trim(),
      args: step.args as Record<string, unknown> | undefined,
      timeout: step.timeout as number | undefined,
      session_type: step.session_type as ScriptStep["session_type"],
    };
  });
}

export function loadScript(file: string): ScriptStep[] {
  return parseScript(fs.readFileSync(file, "utf8"));
}

/** Runs the steps in order and stops at the first failure; the result covers every step that ran. */
export async function runScript(
  steps: ScriptStep[],
  base: ExecuteCommandOptions,
  execute: Executor,
): Promise<CliResult> {
  const outcomes: StepOutcome[] = [];
  const started = Date.now();

  for (const [index, step] of steps.entries()) {
    const stepStarted = Date.now();
    const result = await execute(step.command, {
      ...base,
      sessionType: step.session_type ?? base.sessionType,
      timeout: step.timeout ?? base.timeout,
      args: step.args ?? {},
    });
    outcomes.push({
      index,
      command: step.command,
      ok: result.ok,
      duration_ms: Date.now() - stepStarted,
      message: result.message,
      error_code: result.error_code,
      hint: result.hint,
      data: result.data,
    });

    if (!result.ok) {
      return {
        ok: false,
        message: `steps[${index}] ${step.command} failed: ${result.message ?? result.error_code ?? "unknown error"}`,
        error_code: result.error_code,
        hint: result.hint,
        data: {
          steps: outcomes,
          failed_at: index,
          not_run: steps.length - index - 1,
          total_ms: Date.now() - started,
        },
      };
    }
  }

  return {
    ok: true,
    message: `${steps.length} steps succeeded`,
    data: { steps: outcomes, failed_at: null, not_run: 0, total_ms: Date.now() - started },
  };
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
