import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { beforeAll, describe, expect, it } from "vitest";
import { expectShapesUnchanged, ok } from "./helpers/content-scenario.js";
import { hasReadyEditorSession } from "./helpers/editor-commands.js";
import { ensureEditMode } from "./helpers/play-mode.js";

/** A real player build into a temp folder. Minutes, not seconds, so only with UCP_EDITOR_SLOW=1. */
const runSlowEditorTests =
  process.env.UCP_EDITOR_INTEGRATION === "1" && process.env.UCP_EDITOR_SLOW === "1";

const BUILD_TIMEOUT = 15 * 60_000;

describe.skipIf(!runSlowEditorTests)("build (Unity required, slow)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
  });

  it(
    "builds a development Windows player and reports it",
    async () => {
      const listed = await ok("scene_list", { folder: "Assets" });
      const scene = (listed.scenes as Array<{ path: string }>)[0]?.path;
      expect(scene, "the test project needs a scene under Assets").toBeTruthy();

      const dir = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-build-"));
      const output = path.join(dir, "UcpProbe.exe");
      try {
        const data = await ok(
          "build",
          {
            target: "StandaloneWindows64",
            scenes: [scene],
            output_path: output,
            development: true,
          },
          BUILD_TIMEOUT,
        );
        expect(data).toMatchObject({
          result: "Succeeded",
          target: "StandaloneWindows64",
          development: true,
        });
        expect(Number(data.size_bytes)).toBeGreaterThan(0);
        expect(fs.existsSync(output)).toBe(true);
      } finally {
        fs.rmSync(dir, { recursive: true, force: true });
      }
    },
    BUILD_TIMEOUT + 60_000,
  );

  it("returns results of the same shape", async () => {
    await expectShapesUnchanged("shapes.build.json");
  });
});
