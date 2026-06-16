import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { projectIdFromPath, queueLayout, readJsonFile, writeJsonAtomic } from "#shared/index.js";

describe("paths", () => {
  it("projectIdFromPath is stable", () => {
    const a = projectIdFromPath("C:\\Project\\GameDemo");
    const b = projectIdFromPath("c:/project/gamedemo");
    expect(a).toBe(b);
    expect(a).toHaveLength(16);
  });

  it("writeJsonAtomic roundtrip", () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-"));
    const file = path.join(dir, "nested", "test.json");
    writeJsonAtomic(file, { hello: "world" });
    expect(readJsonFile(file)).toEqual({ hello: "world" });
  });

  it("readJsonFile strips UTF-8 BOM from Unity session files", () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-bom-"));
    const file = path.join(dir, "session.json");
    fs.writeFileSync(file, '\uFEFF{"id":"x"}\n', "utf8");
    expect(readJsonFile(file)).toEqual({ id: "x" });
  });

  it("queueLayout paths", () => {
    const root = path.join(os.tmpdir(), "ucp-test-root");
    const layout = queueLayout("abc123", root);
    expect(layout.inbox).toMatch(/queues[\\/]abc123[\\/]inbox$/);
    expect(layout.outbox).toMatch(/queues[\\/]abc123[\\/]outbox$/);
  });
});
