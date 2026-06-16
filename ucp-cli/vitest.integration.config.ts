import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

const root = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  resolve: {
    alias: {
      "#shared": path.join(root, "src/shared"),
      "#host": path.join(root, "src/host"),
      "#cli": path.join(root, "src/cli"),
    },
  },
  test: {
    include: ["tests/integration/**/*.test.ts"],
    environment: "node",
    testTimeout: 180_000,
    hookTimeout: 120_000,
    fileParallelism: false,
  },
});
