import type { Command } from "commander";
import { probeAllTargets } from "#host/targets/target-probe.js";
import { getTarget, loadTargets, removeTarget, upsertTarget } from "#shared/target-store.js";
import type { EditorTarget, RuntimeTarget } from "#shared/types.js";
import { fetchUcpHostStatus } from "../client/ucp-host-client.js";
import { printResult } from "../result.js";

interface SetOptions {
  type?: string;
  name?: string;
  project?: string;
  host?: string;
  port?: string;
  queueId?: string;
}

export function registerTargetsCommand(program: Command): void {
  const targets = program.command("targets").description("Manage UCP target configuration");

  targets
    .command("list")
    .description("List configured targets and probe their status")
    .option("--local", "Probe locally without ucp-host")
    .action(async (opts: { local?: boolean }) => {
      if (opts.local) {
        const statuses = await probeAllTargets(process.env.UCP_ROOT);
        printResult({ ok: true, targets: statuses });
        return;
      }

      try {
        const status = await fetchUcpHostStatus();
        printResult({ ok: true, ...status });
      } catch (err) {
        const statuses = await probeAllTargets(process.env.UCP_ROOT);
        printResult({
          ok: true,
          warning: err instanceof Error ? err.message : String(err),
          targets: statuses,
        });
      }
    });

  targets
    .command("set <id>")
    .description("Create or update a target in ~/.ucp/targets.json")
    .option("--type <type>", "editor or runtime")
    .option("--name <name>", "Display name")
    .option("--project <path>", "Unity project path")
    .option("--host <host>", "Runtime host/IP")
    .option("--port <port>", "Runtime port", (v) => Number.parseInt(v, 10))
    .option("--queue-id <id>", "Editor queue id override")
    .action((id: string, opts: SetOptions) => {
      const existing = getTarget(id, process.env.UCP_ROOT);
      const type = (opts.type ?? existing?.type) as "editor" | "runtime" | undefined;

      if (!type) {
        printResult({
          ok: false,
          error_code: "missing_type",
          hint: "Provide --type editor|runtime for new targets",
        });
        process.exit(1);
      }

      if (type === "editor") {
        const projectPath = opts.project ?? (existing as EditorTarget | undefined)?.projectPath;
        if (!projectPath) {
          printResult({
            ok: false,
            error_code: "missing_project",
            hint: "Editor targets require --project",
          });
          process.exit(1);
        }

        const saved = upsertTarget(
          {
            id,
            type: "editor",
            name: opts.name ?? (existing as EditorTarget | undefined)?.name,
            projectPath,
            queueId: opts.queueId ?? (existing as EditorTarget | undefined)?.queueId,
          },
          process.env.UCP_ROOT,
        );
        printResult({ ok: true, target: saved });
        return;
      }

      const host = opts.host ?? (existing as RuntimeTarget | undefined)?.host;
      const portValue = opts.port ?? (existing as RuntimeTarget | undefined)?.port;
      const port = typeof portValue === "string" ? Number.parseInt(portValue, 10) : portValue;
      if (!host || !port) {
        printResult({
          ok: false,
          error_code: "missing_endpoint",
          hint: "Runtime targets require --host and --port",
        });
        process.exit(1);
      }

      const saved = upsertTarget(
        {
          id,
          type: "runtime",
          name: opts.name ?? (existing as RuntimeTarget | undefined)?.name,
          host,
          port,
          projectPath: opts.project ?? (existing as RuntimeTarget | undefined)?.projectPath,
        },
        process.env.UCP_ROOT,
      );
      printResult({ ok: true, target: saved });
    });

  targets
    .command("remove <id>")
    .description("Remove a target from ~/.ucp/targets.json")
    .action((id: string) => {
      const removed = removeTarget(id, process.env.UCP_ROOT);
      if (!removed) {
        printResult({ ok: false, error_code: "not_found", hint: `Target not found: ${id}` });
        process.exit(1);
      }
      printResult({ ok: true, removed: id });
    });

  targets
    .command("show")
    .description("Show raw targets configuration")
    .action(() => {
      printResult({ ok: true, targets: loadTargets(process.env.UCP_ROOT) });
    });
}
