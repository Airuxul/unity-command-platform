// Rewrites a session file in a loop, the way one of its writers does, until killed.
// argv: <strategy> <file> <json body>
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const [strategy, file, body] = process.argv.slice(2);
const here = path.dirname(fileURLToPath(import.meta.url));
const { writeJsonAtomic } = await import(
  pathToFileURL(path.join(here, "../../../../dist/shared/index.js")).href
);

// UcpPaths.WriteJsonAtomic in com.air.ucp-agent before 0.5.
function deleteThenMove(target, text) {
  const tmp = `${target}.tmp.${process.pid}`;
  fs.writeFileSync(tmp, text);
  if (fs.existsSync(target)) fs.unlinkSync(target);
  fs.renameSync(tmp, target);
}

for (;;) {
  const data = { ...JSON.parse(body), updatedAt: new Date().toISOString() };
  try {
    if (strategy === "delete-then-move") deleteThenMove(file, JSON.stringify(data, null, 2));
    else writeJsonAtomic(file, data);
  } catch {
    // A reader had it open; the next round tries again, as the agent's next heartbeat would.
  }
}
