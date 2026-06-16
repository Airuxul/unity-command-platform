import type { UcpCommand, UcpResult, UcpSession } from "#shared/types.js";
import type { Logger } from "../../logger/logger.js";

export class RuntimeHttpAdapter {
  constructor(readonly logger: Logger) {}

  async dispatch(session: UcpSession, command: UcpCommand): Promise<UcpResult> {
    const port = session.runtimePort;
    if (!port) {
      throw new Error("runtime session missing runtimePort");
    }
    const host = session.host ?? "127.0.0.1";
    const url = `http://${host}:${port}/command`;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), command.timeout);
    try {
      const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(command),
        signal: controller.signal,
      });
      const body = await res.json();
      if (!body || typeof body !== "object") {
        throw new Error("invalid runtime response");
      }
      const result = body as UcpResult;
      if (!res.ok && !result.error) {
        result.success = false;
        result.error = `http_${res.status}`;
      }
      return result;
    } finally {
      clearTimeout(timer);
    }
  }
}
