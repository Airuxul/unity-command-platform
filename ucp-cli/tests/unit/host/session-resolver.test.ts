import { describe, expect, it } from "vitest";
import { resolveSession } from "#host/sessions/session-resolver.js";
import type { UcpSession } from "#shared/types.js";

function session(overrides: Partial<UcpSession>): UcpSession {
  return {
    id: "editor-1",
    name: "demo",
    path: "C:/Project/Demo",
    type: "editor",
    status: "ready",
    capabilities: ["ping"],
    updatedAt: new Date().toISOString(),
    ...overrides,
  };
}

const editor = session({ id: "editor-demo", path: "C:/Project/Demo" });
const runtime = session({
  id: "runtime-demo",
  type: "runtime",
  path: "C:/Project/Demo",
});
const other = session({ id: "editor-other", path: "C:/Project/Other" });

describe("resolveSession", () => {
  const all = [editor, runtime, other];
  const get = (id: string) => all.find((s) => s.id === id) ?? null;

  it("picks by id", () => {
    expect(resolveSession(all, { sessionId: "runtime-demo" }, get, () => true)?.id).toBe(
      "runtime-demo",
    );
  });

  it("prefers the editor of a project when the type is not given", () => {
    expect(resolveSession(all, { projectPath: "c:/project/demo" }, get, () => true)?.id).toBe(
      "editor-demo",
    );
  });

  it("picks the one session of the requested type", () => {
    expect(
      resolveSession(
        all,
        { projectPath: "C:/Project/Demo", sessionType: "runtime" },
        get,
        () => true,
      )?.id,
    ).toBe("runtime-demo");
  });

  it("refuses when several sessions match and none is distinguished", () => {
    expect(resolveSession(all, {}, get, () => true)).toBeNull();
  });
});
