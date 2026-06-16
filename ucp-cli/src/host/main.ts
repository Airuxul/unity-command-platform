#!/usr/bin/env node
import { createUcpHost } from "./app.js";

const port = process.env.UCP_HOST_PORT ? Number.parseInt(process.env.UCP_HOST_PORT, 10) : undefined;
const logLevel = process.env.UCP_LOG_LEVEL as "debug" | "info" | "warn" | "error" | undefined;
const ucpRoot = process.env.UCP_ROOT;

createUcpHost({ port, ucpRoot, logLevel }).catch((err) => {
  console.error("[ucp-host] fatal", err);
  process.exit(1);
});

process.on("SIGINT", () => process.exit(0));
process.on("SIGTERM", () => process.exit(0));
