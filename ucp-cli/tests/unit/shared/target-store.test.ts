import { describe, expect, it } from "vitest";
import { getTarget, loadTargets, removeTarget, upsertTarget } from "#shared/target-store.js";

describe("target-store", () => {
  const ucpRoot = `C:/tmp/ucp-test-${process.pid}`;

  it("upserts editor and runtime targets", () => {
    upsertTarget(
      {
        id: "demo-editor",
        type: "editor",
        projectPath: "C:/Project/GameDemo",
      },
      ucpRoot,
    );
    upsertTarget(
      {
        id: "demo-runtime",
        type: "runtime",
        host: "192.168.0.10",
        port: 6620,
        projectPath: "C:/Project/GameDemo",
      },
      ucpRoot,
    );

    const targets = loadTargets(ucpRoot);
    expect(targets).toHaveLength(2);
    expect(getTarget("demo-runtime", ucpRoot)?.type).toBe("runtime");
    expect((getTarget("demo-runtime", ucpRoot) as { host: string }).host).toBe("192.168.0.10");
  });

  it("updates an existing target via set semantics", () => {
    upsertTarget(
      {
        id: "device",
        type: "runtime",
        host: "10.0.0.1",
        port: 6620,
      },
      ucpRoot,
    );
    upsertTarget(
      {
        id: "device",
        type: "runtime",
        host: "10.0.0.2",
        port: 6621,
      },
      ucpRoot,
    );

    const target = getTarget("device", ucpRoot);
    expect(target).toMatchObject({ host: "10.0.0.2", port: 6621 });
    expect(removeTarget("device", ucpRoot)).toBe(true);
  });
});
