import type { UcpCommand, UcpResult, UcpSession } from "#shared/types.js";
import type { EditorFileQueueAdapter } from "../adapter/editor-file-queue/editor-file-queue-adapter.js";
import type { RuntimeHttpAdapter } from "../adapter/runtime-http/runtime-http-adapter.js";
import type { CapabilityRegistry } from "../capability/capability-registry.js";
import type { Logger } from "../logger/logger.js";

export interface TransportRouterDeps {
  capabilities: CapabilityRegistry;
  editorAdapter: EditorFileQueueAdapter;
  runtimeAdapter: RuntimeHttpAdapter;
  logger: Logger;
}

export class TransportRouter {
  readonly capabilities: CapabilityRegistry;
  readonly editorAdapter: EditorFileQueueAdapter;
  readonly runtimeAdapter: RuntimeHttpAdapter;
  readonly logger: Logger;

  constructor({ capabilities, editorAdapter, runtimeAdapter, logger }: TransportRouterDeps) {
    this.capabilities = capabilities;
    this.editorAdapter = editorAdapter;
    this.runtimeAdapter = runtimeAdapter;
    this.logger = logger;
  }

  async route(session: UcpSession, command: UcpCommand): Promise<UcpResult> {
    if (!this.capabilities.supports(session, command.type)) {
      throw new Error(`session ${session.id} does not support command type: ${command.type}`);
    }
    if (session.type === "editor") {
      return this.editorAdapter.dispatch(session, command);
    }
    if (session.type === "runtime") {
      return this.runtimeAdapter.dispatch(session, command);
    }
    throw new Error(`unknown session type: ${session.type}`);
  }
}
