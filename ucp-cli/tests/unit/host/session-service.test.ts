import { describe, expect, it } from "vitest";
import { Logger } from "#host/logger/logger.js";
import { SessionService } from "#host/session/session-service.js";

describe("SessionService", () => {
  const base = {
    name: "GameDemo",
    path: "C:/Project/GameDemo",
    status: "ready" as const,
    capabilities: ["echo"],
    updatedAt: new Date().toISOString(),
  };

  it("prefers editor session when project has both editor and runtime", () => {
    const sessions = new SessionService(new Logger({ level: "error" }));
    sessions.upsert({ ...base, id: "editor-1", type: "editor" });
    sessions.upsert({ ...base, id: "runtime-1", type: "runtime", runtimePort: 6620 });

    const resolved = sessions.resolve({ projectPath: "C:/Project/GameDemo" });
    expect(resolved?.id).toBe("editor-1");
  });

  it("resolves runtime session by sessionType", () => {
    const sessions = new SessionService(new Logger({ level: "error" }));
    sessions.upsert({ ...base, id: "editor-1", type: "editor" });
    sessions.upsert({ ...base, id: "runtime-1", type: "runtime", runtimePort: 6620 });

    const resolved = sessions.resolve({
      projectPath: "C:/Project/GameDemo",
      sessionType: "runtime",
    });
    expect(resolved?.id).toBe("runtime-1");
  });
});
