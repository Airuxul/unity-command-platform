import fs from "node:fs";
import { DEFAULT_HOST_PORT, ucpLayout } from "#shared/index.js";
import type { LogLevel } from "#shared/types.js";
import { EditorFileQueueAdapter } from "./adapter/editor-file-queue/editor-file-queue-adapter.js";
import { RuntimeHttpAdapter } from "./adapter/runtime-http/runtime-http-adapter.js";
import { CapabilityRegistry } from "./capability/capability-registry.js";
import { DiscoveryService } from "./discovery/discovery-service.js";
import { Logger } from "./logger/logger.js";
import { QueueService } from "./queue/queue-service.js";
import { RetryPolicy } from "./retry/retry-policy.js";
import { TransportRouter } from "./router/transport-router.js";
import { SchedulerService } from "./scheduler/scheduler-service.js";
import { UcpHostServer } from "./server/ucp-host-server.js";
import { SessionService } from "./session/session-service.js";

export interface UcpHostOptions {
  port?: number;
  ucpRoot?: string;
  logLevel?: LogLevel;
}

export interface UcpHost {
  logger: Logger;
  sessions: SessionService;
  queue: QueueService;
  discovery: DiscoveryService;
  scheduler: SchedulerService;
  server: UcpHostServer;
}

export async function createUcpHost(options: UcpHostOptions = {}): Promise<UcpHost> {
  const ucpRoot = options.ucpRoot;
  const layout = ucpLayout(ucpRoot);
  fs.mkdirSync(layout.root, { recursive: true });

  const logger = new Logger({ level: options.logLevel ?? "info" });
  const sessions = new SessionService(logger);
  const queue = new QueueService(logger);
  const discovery = new DiscoveryService(sessions, logger, ucpRoot);
  const capabilities = new CapabilityRegistry();
  const editorAdapter = new EditorFileQueueAdapter(logger, ucpRoot);
  const runtimeAdapter = new RuntimeHttpAdapter(logger);
  const router = new TransportRouter({
    capabilities,
    editorAdapter,
    runtimeAdapter,
    logger,
  });
  const retry = new RetryPolicy();
  const scheduler = new SchedulerService({ queue, router, sessions, retry, logger });
  const server = new UcpHostServer({
    queue,
    sessions,
    logger,
    port: options.port ?? DEFAULT_HOST_PORT,
    ucpRoot,
  });

  discovery.start();
  scheduler.start();
  await server.start();

  return { logger, sessions, queue, discovery, scheduler, server };
}
