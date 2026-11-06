import { Command } from "commander";
import { executeCommand, fetchUcpHostStatus } from "./client/ucp-host-client.js";
import { type ScriptStep, loadScript, runScript } from "./commands/run-script.js";
import { registerTargetsCommand } from "./commands/targets.js";
import { exitFromResult, printResult } from "./result.js";

export async function run(argv: string[]): Promise<void> {
  const args = argv.length === 0 ? ["--help"] : argv;

  const program = new Command();

  program
    .name("ucp-cli")
    .description("Unity Command Platform CLI")
    .version("0.3.0")
    .option("--project <path>", "Unity project path for session selection")
    .option("--session <id>", "Target session id")
    .option("--target <id>", "Target id from ~/.ucp/targets.json")
    .option("--session-type <type>", "Target session type: editor or runtime")
    .option("--timeout <ms>", "Command timeout in milliseconds", (v) => Number.parseInt(v, 10));

  type GlobalOpts = {
    project?: string;
    session?: string;
    target?: string;
    sessionType?: "editor" | "runtime";
    timeout?: number;
  };

  function globalOpts(): GlobalOpts | undefined {
    const opts = program.opts<GlobalOpts & { sessionType?: string }>();
    if (
      opts.sessionType !== undefined &&
      opts.sessionType !== "editor" &&
      opts.sessionType !== "runtime"
    ) {
      printResult({
        ok: false,
        error_code: "invalid_argument",
        message: `--session-type must be "editor" or "runtime", got '${opts.sessionType}'.`,
      });
      exitFromResult({ ok: false });
      return undefined;
    }
    return {
      project: opts.project,
      session: opts.session,
      target: opts.target,
      sessionType: opts.sessionType,
      timeout: opts.timeout,
    };
  }

  program
    .command("ping")
    .description("Ping Unity Editor via UCP")
    .action(async () => {
      const opts = globalOpts();
      if (!opts) return;
      const result = await executeCommand("ping", {
        project: opts.project,
        session: opts.session,
        target: opts.target ?? opts.session,
        sessionType: opts.sessionType,
        timeout: opts.timeout,
      });
      printResult(result);
      exitFromResult(result);
    });

  program
    .command("build <output_path>")
    .description("Build a player; scenes default to the enabled ones in Build Settings")
    // Not --target: that one picks the ucp target id.
    .option(
      "--platform <name>",
      "BuildTarget name, e.g. StandaloneWindows64 (default: the active one)",
    )
    .option("--scene <path...>", "scene asset paths to build")
    .option("--development", "development build")
    .action(
      async (
        outputPath: string,
        opts: { platform?: string; scene?: string[]; development?: boolean },
      ) => {
        const g = globalOpts();
        if (!g) return;
        const args: Record<string, unknown> = { output_path: outputPath };
        if (opts.platform) args.target = opts.platform;
        if (opts.scene) args.scenes = opts.scene;
        if (opts.development) args.development = true;
        const result = await executeCommand("build", {
          project: g.project,
          session: g.session,
          target: g.target ?? g.session,
          sessionType: g.sessionType,
          timeout: g.timeout,
          args,
        });
        printResult(result);
        exitFromResult(result);
      },
    );

  program
    .command("run-script <file>")
    .description(
      'Run a JSON flow of {"command", "args"?, "timeout"?, "session_type"?} steps in order; stops at the first failure',
    )
    .action(async (file: string) => {
      const g = globalOpts();
      if (!g) return;
      let steps: ScriptStep[];
      try {
        steps = loadScript(file);
      } catch (error) {
        const result = {
          ok: false,
          error_code: "invalid_argument",
          message: `${file}: ${error instanceof Error ? error.message : String(error)}`,
        };
        printResult(result);
        exitFromResult(result);
        return;
      }

      const result = await runScript(
        steps,
        {
          project: g.project,
          session: g.session,
          target: g.target ?? g.session,
          sessionType: g.sessionType,
          timeout: g.timeout,
        },
        executeCommand,
      );
      printResult(result);
      exitFromResult(result);
    });

  const host = program.command("host").description("ucp-host utilities");

  host
    .command("status")
    .description("Show host health, targets, and ready sessions")
    .action(async () => {
      const status = await fetchUcpHostStatus();
      printResult({ ok: true, ...status });
    });

  registerTargetsCommand(program);

  // `help` lists the Unity commands; this CLI's own usage stays on --help.
  program.helpCommand(false);
  program
    .command("help [command]")
    .description("List the Unity commands, or show one command's card (CLI usage: --help)")
    .option("--md", "print the cards as markdown")
    .action(async (command: string | undefined, opts: { md?: boolean }) => {
      const g = globalOpts();
      if (!g) return;
      const args: Record<string, unknown> = {};
      if (command) args.command = command;
      if (opts.md) args.format = "md";
      const result = await executeCommand("help", {
        project: g.project,
        session: g.session,
        target: g.target ?? g.session,
        sessionType: g.sessionType,
        timeout: g.timeout,
        args,
      });
      const markdown = (result.data as { markdown?: unknown } | null)?.markdown;
      if (result.ok && opts.md && typeof markdown === "string") console.log(markdown);
      else printResult(result);
      exitFromResult(result);
    });

  program
    .command("run <type>")
    .description("Run arbitrary UCP command type against Unity Editor")
    .option("--args <json>", "JSON object of command args", "{}")
    .action(async (type: string, opts: { args?: string }) => {
      const g = globalOpts();
      if (!g) return;
      let args: Record<string, unknown> = {};
      if (opts.args) {
        try {
          const parsed: unknown = JSON.parse(opts.args);
          if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
            throw new Error("--args must be a JSON object");
          }
          args = parsed as Record<string, unknown>;
        } catch (error) {
          printResult({
            ok: false,
            error_code: "invalid_argument",
            message: error instanceof Error ? error.message : String(error),
          });
          exitFromResult({ ok: false });
          return;
        }
      }
      const result = await executeCommand(type, {
        project: g.project,
        session: g.session,
        target: g.target ?? g.session,
        sessionType: g.sessionType,
        timeout: g.timeout,
        args,
      });
      printResult(result);
      exitFromResult(result);
    });

  await program.parseAsync(args, { from: "user" });
}
