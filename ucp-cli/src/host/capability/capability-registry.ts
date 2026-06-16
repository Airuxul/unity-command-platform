import type { UcpSession } from "#shared/types.js";

export class CapabilityRegistry {
  supports(session: UcpSession, commandType: string): boolean {
    const type = commandType.toLowerCase();
    return session.capabilities?.some((c) => c.toLowerCase() === type) ?? false;
  }
}
