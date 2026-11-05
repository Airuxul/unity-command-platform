import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { projectIdFromPath, queueLayout, resolveUcpRoot } from "#shared/index.js";
import { expectShapesUnchanged, ok, rejected, run } from "./helpers/content-scenario.js";
import { gameDemoProjectPath, hasReadyEditorSession } from "./helpers/editor-commands.js";
import { ensureEditMode } from "./helpers/play-mode.js";

/**
 * package_list / add / remove against local file: packages, offline. One package has scripts, so
 * adding and removing it reloads the domain and the job has to finish in the next one. The
 * manifest and lock file are put back afterwards whatever happens.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

const PLAIN = "com.ucp.probe.plain";
const SCRIPTED = "com.ucp.probe.scripted";
const RESOLVE_TIMEOUT = 180_000;

function packagesFile(name: string): string {
  return path.join(gameDemoProjectPath(), "Packages", name);
}

function writePackage(root: string, name: string, scripted: boolean): string {
  const dir = path.join(root, name);
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(
    path.join(dir, "package.json"),
    JSON.stringify({ name, version: "0.0.1", displayName: `UCP probe ${name}`, unity: "2021.3" }),
  );
  if (scripted) {
    fs.mkdirSync(path.join(dir, "Runtime"));
    fs.writeFileSync(
      path.join(dir, "Runtime", `${name}.asmdef`),
      JSON.stringify({ name, rootNamespace: "UcpProbePackage" }),
    );
    fs.writeFileSync(
      path.join(dir, "Runtime", "UcpProbePackageMarker.cs"),
      "namespace UcpProbePackage { public static class Marker { public const int Value = 7; } }\n",
    );
  } else {
    fs.writeFileSync(path.join(dir, "README.md"), "ucp probe\n");
  }
  return `file:${dir.replace(/\\/g, "/")}`;
}

function queueFiles(): string[] {
  const projectId = projectIdFromPath(gameDemoProjectPath());
  const layout = queueLayout(projectId, process.env.UCP_ROOT);
  const jobs = path.join(resolveUcpRoot(process.env.UCP_ROOT), "jobs", projectId);
  return [layout.inbox, layout.outbox, jobs].flatMap((dir) =>
    fs.existsSync(dir) ? fs.readdirSync(dir).map((f) => `${path.basename(dir)}/${f}`) : [],
  );
}

describe.skipIf(!runEditorTests)("package commands (Unity required)", () => {
  const backups = new Map<string, string>();
  let root = "";
  let plain = "";
  let scripted = "";

  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
    for (const name of ["manifest.json", "packages-lock.json"]) {
      const file = packagesFile(name);
      if (fs.existsSync(file)) backups.set(file, fs.readFileSync(file, "utf8"));
    }
    root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-packages-"));
    plain = writePackage(root, PLAIN, false);
    scripted = writePackage(root, SCRIPTED, true);
  });

  afterAll(async () => {
    const changed = [...backups].some(([file, text]) => fs.readFileSync(file, "utf8") !== text);
    for (const [file, text] of backups) fs.writeFileSync(file, text);
    // A refresh makes Unity resolve the restored manifest, and recompile if that removed scripts.
    if (changed) await run("refresh", { compile: true }, RESOLVE_TIMEOUT);
    fs.rmSync(root, { recursive: true, force: true });
  }, RESOLVE_TIMEOUT + 60_000);

  it("lists the manifest's packages, and the dependencies on request", async () => {
    const direct = await ok("package_list");
    const names = (direct.packages as Array<{ package: string; direct: boolean }>).map(
      (p) => p.package,
    );
    expect(names).toContain("com.air.ucp-agent");

    const all = await ok("package_list", { indirect: true });
    expect(Number(all.count)).toBeGreaterThan(Number(direct.count));
  });

  it("adds and removes a package without scripts", async () => {
    const added = await ok("package_add", { package: plain }, RESOLVE_TIMEOUT);
    expect(added).toMatchObject({
      package: PLAIN,
      version: "0.0.1",
      source: "local",
      direct: true,
    });

    const removed = await ok("package_remove", { package: PLAIN }, RESOLVE_TIMEOUT);
    expect(removed).toMatchObject({ package: PLAIN, removed: true });
  });

  it("adds and removes a package with scripts across the domain reload", async () => {
    const added = await ok("package_add", { package: scripted }, RESOLVE_TIMEOUT);
    expect(added.package).toBe(SCRIPTED);
    const loaded = await ok("exec", { code: "return UcpProbePackage.Marker.Value;" });
    expect(loaded.result).toBe(7);

    const removed = await ok("package_remove", { package: SCRIPTED }, RESOLVE_TIMEOUT);
    expect(removed.removed).toBe(true);
    const listed = await ok("package_list");
    expect((listed.packages as Array<{ package: string }>).map((p) => p.package)).not.toContain(
      SCRIPTED,
    );
  });

  it("refuses to remove ucp-agent or a package the manifest does not list", async () => {
    const self = await rejected("package_remove", { package: "com.air.ucp-agent" });
    expect(self.message).toContain("ucp-agent itself");

    const missing = await rejected("package_remove", { package: "com.ucp.probe.none" });
    expect(missing.error_code).toBe("invalid_argument");
  });

  it("reports a package that cannot be resolved", async () => {
    const result = await rejected(
      "package_add",
      { package: `file:${path.join(root, "missing").replace(/\\/g, "/")}` },
      RESOLVE_TIMEOUT,
    );
    expect(result.message).toContain("missing");
    expect(queueFiles()).toEqual([]);
  });

  it("returns results of the same shape", async () => {
    await expectShapesUnchanged("shapes.packages.json");
  });
});
