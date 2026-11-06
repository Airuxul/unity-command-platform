import { describe, expect, it } from "vitest";
import { createDispatch } from "#host/app.js";
import { HostDispatchError, HostError } from "#host/errors.js";
import { Logger } from "#host/logger/logger.js";
import type { UcpCommand, UcpSession } from "#shared/types.js";

describe("createDispatch", () => {
  it("returns unknown_session_type and does not call a transport", async () => {
    const dispatch = createDispatch(new Logger({ level: "error" }));
    const session = {
      id: "x",
      name: "x",
      path: "C:/X",
      type: "device",
      status: "ready",
      capabilities: ["ping"],
      updatedAt: new Date().toISOString(),
    } as unknown as UcpSession;
    const command: UcpCommand = { id: "1", type: "ping", timeout: 1000, args: {} };
    await expect(dispatch(session, command)).rejects.toBeInstanceOf(HostDispatchError);
    await expect(dispatch(session, command)).rejects.toMatchObject({
      code: HostError.unknownSessionType,
    });
  });
});
