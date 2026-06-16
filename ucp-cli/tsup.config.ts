import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import type { Plugin } from "esbuild";
import { defineConfig } from "tsup";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function resolveSrcFile(root: string, subdir: string, importPath: string): string {
  const rel = importPath.replace(new RegExp(`^#${subdir}/`), "");
  const base = rel.endsWith(".js") ? rel.slice(0, -3) : rel;
  const tsPath = path.join(root, subdir, `${base}.ts`);
  if (fs.existsSync(tsPath)) return tsPath;
  return path.join(root, subdir, rel);
}

function ucpAliasPlugin(): Plugin {
  const root = path.join(__dirname, "src");
  return {
    name: "ucp-alias",
    setup(build) {
      for (const subdir of ["shared", "host", "cli"] as const) {
        const prefix = `#${subdir}/`;
        build.onResolve({ filter: new RegExp(`^#${subdir}/`) }, (args) => ({
          path: resolveSrcFile(root, subdir, args.path),
        }));
      }
    },
  };
}

export default defineConfig({
  entry: {
    index: "src/index.ts",
    "shared/index": "src/shared/index.ts",
    "host/app": "src/host/app.ts",
    "cli/client/ucp-host-client": "src/cli/client/ucp-host-client.ts",
    "cli/main": "src/cli/main.ts",
    "host/main": "src/host/main.ts",
  },
  format: ["esm"],
  target: "node20",
  outDir: "dist",
  clean: true,
  dts: true,
  sourcemap: true,
  splitting: false,
  shims: false,
  esbuildPlugins: [ucpAliasPlugin()],
});
