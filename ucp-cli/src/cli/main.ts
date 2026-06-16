import { Command } from "commander";
import { executeCommand, fetchUcpHostStatus } from "./client/ucp-host-client.js";
import { registerTargetsCommand } from "./commands/targets.js";
import { exitFromResult, printResult } from "./result.js";

export async function run(argv: string[]): Promise<void> {
  const args = argv.length === 0 ? ["--help"] : argv;

  const program = new Command();

  program
    .name("ucp-cli")
    .description("Unity Command Platform CLI")
    .version("0.2.0")
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

  function globalOpts(): GlobalOpts {
    const opts = program.opts<GlobalOpts & { sessionType?: string }>();
    const sessionType =
      opts.sessionType === "editor" || opts.sessionType === "runtime"
        ? opts.sessionType
        : undefined;
    return { ...opts, sessionType };
  }

  program
    .command("ping")
    .description("Ping Unity Editor via UCP")
    .action(async () => {
      const opts = globalOpts();
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
    .command("build")
    .description("Run build command (skeleton)")
    .action(async () => {
      const opts = globalOpts();
      const result = await executeCommand("build", {
        project: opts.project,
        session: opts.session,
        target: opts.target ?? opts.session,
        sessionType: opts.sessionType,
        timeout: opts.timeout,
      });
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

  program
    .command("run <type>")
    .description("Run arbitrary UCP command type against Unity Editor")
    .option("--args <json>", "JSON object of command args", "{}")
    .action(async (type: string, opts: { args?: string }) => {
      const g = globalOpts();
      let args: Record<string, unknown> = {};
      if (opts.args) {
        args = JSON.parse(opts.args) as Record<string, unknown>;
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
