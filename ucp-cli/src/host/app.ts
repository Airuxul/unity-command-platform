import fs from "node:fs";
import { DEFAULT_HOST_PORT, readBuildId, ucpLayout } from "#shared/index.js";
import type { LogLevel, UcpCommand, UcpResult, UcpSession } from "#shared/types.js";
import { EditorFileQueueAdapter } from "./adapter/editor-file-queue/editor-file-queue-adapter.js";
import { RuntimeHttpAdapter } from "./adapter/runtime-http/runtime-http-adapter.js";
import { CommandRunner, type Dispatch } from "./commands/command-runner.js";
import { HostDispatchError, HostError } from "./errors.js";
import { Logger } from "./logger/logger.js";
import { UcpHostServer } from "./server/ucp-host-server.js";
import { SessionDirectory } from "./sessions/session-directory.js";

export interface UcpHostOptions {
  port?: number;
  ucpRoot?: string;
  logLevel?: LogLevel;
  build?: number;
  token?: string;
  /** After POST /shutdown was accepted and the host has stopped. */
  onShutdown?: () => void;
}

export interface UcpHost {
  logger: Logger;
  sessions: SessionDirectory;
  runner: CommandRunner;
  server: UcpHostServer;
  stop(): Promise<void>;
}

export function createDispatch(logger: Logger, ucpRoot?: string): Dispatch {
  const editor = new EditorFileQueueAdapter(logger, ucpRoot);
  const runtime = new RuntimeHttpAdapter(logger);
  const transports = new Map<
    string,
    (session: UcpSession, command: UcpCommand) => Promise<UcpResult>
  >([
    ["editor", (session, command) => editor.dispatch(session, command)],
    ["runtime", (session, command) => runtime.dispatch(session, command)],
  ]);
  return (session, command) => {
    const transport = transports.get(session.type);
    if (!transport) {
      return Promise.reject(
        new HostDispatchError(
          HostError.unknownSessionType,
          `No transport for session type '${session.type}'.`,
        ),
      );
    }
    return transport(session, command);
  };
}

export async function createUcpHost(options: UcpHostOptions = {}): Promise<UcpHost> {
  const ucpRoot = options.ucpRoot;
  fs.mkdirSync(ucpLayout(ucpRoot).root, { recursive: true });

  const logger = new Logger({ level: options.logLevel ?? "info" });
  const sessions = new SessionDirectory(logger, ucpRoot);
  const runner = new CommandRunner(sessions, createDispatch(logger, ucpRoot), logger);
  const stop = async () => {
    await server.stop();
    runner.stop();
    sessions.stop();
  };
  const server = new UcpHostServer({
    runner,
    sessions,
    logger,
    port: options.port ?? DEFAULT_HOST_PORT,
    ucpRoot,
    build: options.build ?? readBuildId(),
    token: options.token,
    // After the 200 has gone out.
    onShutdown: () => setImmediate(() => void stop().then(options.onShutdown)),
  });

  sessions.start();
  await sessions.scan();
  runner.start();
  await server.start();

  return { logger, sessions, runner, server, stop };
}
