#!/usr/bin/env node
/**
 * UCP smoke test — one Prefab round trip, ~3-4 min, human-readable output.
 *
 *   npm run smoke -- --project C:/Path/To/UnityProject
 *
 * This is the fast tier. `npm run test:integration` is the exhaustive one (every command, ~16 min);
 * this walks a single closed loop that proves the parts fit together: create a prefab asset with no
 * scene open, edit its contents, read the values back off disk, instantiate it, override the
 * instance, and apply those overrides back to the asset.
 *
 * Most of the wall clock is Unity re-importing the prefab after each write, not round-trip
 * overhead — batching several commands per request (docs/TODO.md AGENT-09) is what would speed
 * this up, not trimming assertions.
 *
 * Everything lives under Assets/UcpSmoke and in an additive scratch scene, so the scene the user
 * has open is never closed or modified. Re-runnable: it clears leftovers before it starts.
 */
import { describeFailure, executeCommand } from "#cli/client/ucp-host-client.js";

const FOLDER = "Assets/UcpSmoke";
const PREFAB = `${FOLDER}/Smoke.prefab`;
const VARIANT = `${FOLDER}/Smoke_Variant.prefab`;
const SCENE = `${FOLDER}/Smoke.unity`;

// --- CLI args ---------------------------------------------------------------------------------

function parseArgs(argv) {
  const options = { project: process.env.UCP_TEST_PROJECT, keep: false };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === "--project") options.project = argv[++i];
    else if (argv[i] === "--keep") options.keep = true;
    else if (argv[i] === "--help" || argv[i] === "-h") options.help = true;
    else throw new Error(`unknown argument '${argv[i]}'`);
  }
  return options;
}

// --- command plumbing -------------------------------------------------------------------------

let project;

async function run(type, args = {}, timeout = 60_000) {
  return executeCommand(type, { project, sessionType: "editor", timeout, args });
}

/** Runs a command, throwing the agent's own explanation if it refused. */
async function ok(type, args = {}, timeout = 60_000) {
  const result = await run(type, args, timeout);
  if (!result.ok) throw new Error(`${type}: ${describeFailure(result) || "unknown error"}`);
  return result.data ?? {};
}

/** Runs a command that must be refused, returning the explanation for assertion. */
async function refused(type, args = {}) {
  const result = await run(type, args);
  if (result.ok) throw new Error(`${type}: expected a refusal, but it succeeded`);
  if (result.error_code === "no_ready_session") {
    throw new Error(`${type}: agent unreachable, cannot judge the refusal`);
  }
  return describeFailure(result);
}

// --- assertions -------------------------------------------------------------------------------

function eq(actual, expected, what) {
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    throw new Error(`${what}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  }
}

function close(actual, expected, what) {
  if (typeof actual !== "number" || Math.abs(actual - expected) > 1e-4) {
    throw new Error(`${what}: expected ~${expected}, got ${JSON.stringify(actual)}`);
  }
}

function has(haystack, needle, what) {
  const list = Array.isArray(haystack) ? haystack : [];
  if (!list.includes(needle)) {
    throw new Error(
      `${what}: expected to contain ${JSON.stringify(needle)}, got ${JSON.stringify(list)}`,
    );
  }
}

function contains(text, needle, what) {
  if (!String(text).includes(needle)) {
    throw new Error(
      `${what}: expected text containing ${JSON.stringify(needle)}, got ${JSON.stringify(text)}`,
    );
  }
}

function truthy(value, what) {
  if (!value) throw new Error(`${what}: expected a value, got ${JSON.stringify(value)}`);
}

// --- the checks -------------------------------------------------------------------------------

const checks = [];
/** @param {string} label @param {() => Promise<string>} body returns the observed value to show */
function check(label, body) {
  checks.push({ label, body });
}

check("help", async () => {
  const data = await ok("help");
  const names = (data.commands ?? []).map((c) => c.name);
  eq(names.length, 82, "command count");
  eq(names.includes("manage"), false, "'manage' should be gone");
  const aliases = Object.keys(data.alias_to_command ?? {});
  eq(aliases.length, 5, "alias count");
  return `${names.length} commands, ${aliases.length} aliases, no 'manage'`;
});

check("asset_create_folder", async () => {
  const data = await ok("asset_create_folder", { path: FOLDER });
  eq(data.path, FOLDER, "folder path");
  return String(data.path);
});

check("prefab_create_empty", async () => {
  // No scene is opened or dirtied to get here — that is the point of this command.
  const data = await ok("prefab_create_empty", { path: PREFAB, name: "Smoke" });
  eq(data.root, "Smoke", "root name");
  eq(String(data.guid).length, 32, "guid length");
  return `${data.path}  guid=${String(data.guid).slice(0, 8)}…`;
});

check("prefab_info", async () => {
  const data = await ok("prefab_info", { path: PREFAB });
  eq(data.asset_type, "Regular", "asset type");
  eq(data.editable, true, "editable");
  eq(data.child_count, 0, "child count");
  return `${data.asset_type}, editable, ${data.child_count} children`;
});

check("gameobject_create", async () => {
  const data = await ok("gameobject_create", {
    prefab: PREFAB,
    parent: "Smoke",
    name: "Body",
    primitive: "cube",
  });
  eq(data.node, "Smoke/Body", "node path");
  // Proves the edit went to the asset, not to whatever scene happens to be open.
  eq(data.target?.kind, "prefab_asset", "edit target");
  has(data.components, "BoxCollider", "cube components");
  return `${data.node}  (target=${data.target?.kind})`;
});

check("component_add", async () => {
  const data = await ok("component_add", {
    prefab: PREFAB,
    node: "Smoke/Body",
    component: "Rigidbody",
    properties: { mass: 7.5, useGravity: false },
  });
  eq(data.component, "Rigidbody", "component type");
  has(data.properties_set, "m_Mass", "properties written");
  return `Rigidbody + ${(data.properties_set ?? []).join(", ")}`;
});

check("component_get", async () => {
  // Read back through a fresh load of the asset — the only proof the write reached disk.
  const data = await ok("component_get", {
    prefab: PREFAB,
    node: "Smoke/Body",
    component: "Rigidbody",
  });
  close(data.properties?.m_Mass, 7.5, "m_Mass");
  eq(data.properties?.m_UseGravity, false, "m_UseGravity");
  return `m_Mass=${data.properties?.m_Mass}  m_UseGravity=${data.properties?.m_UseGravity}`;
});

check("component_set round trip", async () => {
  await ok("component_set", {
    prefab: PREFAB,
    node: "Smoke/Body",
    component: "Rigidbody",
    properties: { mass: 3.25, useGravity: true },
  });
  const data = await ok("component_get", {
    prefab: PREFAB,
    node: "Smoke/Body",
    component: "Rigidbody",
    property: "mass",
  });
  close(data.properties?.m_Mass, 3.25, "m_Mass after set");
  return `m_Mass 7.5 → ${data.properties?.m_Mass}`;
});

check("component_list", async () => {
  const data = await ok("component_list", { prefab: PREFAB, node: "Smoke/Body" });
  const types = (data.components ?? []).map((c) => c.type);
  for (const wanted of ["Transform", "BoxCollider", "Rigidbody"]) {
    has(types, wanted, "component list");
  }
  return types.join(", ");
});

check("gameobject_transform", async () => {
  const data = await ok("gameobject_transform", {
    prefab: PREFAB,
    node: "Smoke/Body",
    position: [1, 2, 3],
    scale: 2,
  });
  eq(data.transform?.local_position, [1, 2, 3], "local position");
  // A single number is broadcast to every component.
  eq(data.transform?.local_scale, [2, 2, 2], "local scale from scalar");
  return "pos=[1,2,3]  scale=2 → [2,2,2]";
});

check("prefab_tree", async () => {
  const data = await ok("prefab_tree", { path: PREFAB });
  const root = data.roots?.[0];
  eq(root?.name, "Smoke", "tree root");
  const body = (root?.children ?? []).find((c) => c.name === "Body");
  truthy(body, "Body in tree");
  has(body.components, "Rigidbody", "Body components in tree");
  return `${root.name} > ${body.name} [${body.components.join(", ")}]`;
});

check("gameobject_find", async () => {
  const data = await ok("gameobject_find", { prefab: PREFAB, component: "Rigidbody" });
  const nodes = (data.matches ?? []).map((m) => m.node);
  has(nodes, "Smoke/Body", "find by component");
  return `component=Rigidbody → ${nodes.join(", ")}`;
});

check("prefab_create_variant", async () => {
  const data = await ok("prefab_create_variant", { path: PREFAB, target: VARIANT });
  eq(data.is_variant, true, "is variant");
  const info = await ok("prefab_info", { path: VARIANT });
  eq(info.base_prefab, PREFAB, "variant base");
  return `${data.path}  base=${info.base_prefab}`;
});

let instanceNode;

check("prefab_instantiate", async () => {
  // Additive scratch scene, so the user's own scene is untouched.
  await ok("scene_new", { setup: "empty", mode: "additive" });
  await ok("scene_save_as", { path: SCENE });

  const data = await ok("prefab_instantiate", { source: PREFAB, scene: SCENE });
  eq(data.nested, false, "instantiated into a scene, not nested");
  instanceNode = String(data.node);
  return `${instanceNode} in ${SCENE}`;
});

check("prefab_overrides (fresh)", async () => {
  const data = await ok("prefab_overrides", { node: instanceNode, scene: SCENE });
  // A root's transform and name are Unity "default overrides" present on every instance and never
  // applied; counting them would report overrides on an untouched instance.
  eq(data.has_overrides, false, "fresh instance has no real overrides");
  return `has_overrides=false (${(data.property_modifications ?? []).length} default overrides ignored)`;
});

check("prefab_overrides (modified)", async () => {
  await ok("gameobject_transform", {
    node: `${instanceNode}/Body`,
    scene: SCENE,
    position: [5, 0, 0],
  });
  const data = await ok("prefab_overrides", { node: instanceNode, scene: SCENE });
  eq(data.has_overrides, true, "override detected");
  const touched = (data.property_modifications ?? []).some((m) =>
    String(m.property).includes("m_LocalPosition"),
  );
  eq(touched, true, "m_LocalPosition listed");
  return "has_overrides=true, m_LocalPosition listed";
});

check("prefab_apply", async () => {
  const applied = await ok("prefab_apply", { node: instanceNode, scene: SCENE });
  eq(applied.applied_to, PREFAB, "applied to asset");
  // Closes the loop: the scene-side edit is now in the asset on disk.
  const asset = await ok("gameobject_info", { prefab: PREFAB, node: "Smoke/Body" });
  eq(asset.transform?.local_position, [5, 0, 0], "asset position after apply");
  return `instance [5,0,0] → asset ${JSON.stringify(asset.transform?.local_position)}`;
});

check("error messages are actionable", async () => {
  const missingComponent = await refused("component_get", {
    prefab: PREFAB,
    node: "Smoke",
    component: "Rigidbody",
  });
  contains(missingComponent, "It has:", "missing-component error lists what is there");

  const missingNode = await refused("gameobject_info", { prefab: PREFAB, node: "Smoke/Nope" });
  contains(missingNode, "Children:", "missing-node error lists siblings");

  return "missing component lists components; missing node lists children";
});

// --- runner -----------------------------------------------------------------------------------

/** Prefab Mode and loaded scenes both pin assets, so release them before deleting. */
async function cleanup() {
  // Closing with force discards unsaved changes, so only ever close a prefab of our own.
  const stage = await run("prefab_stage_info");
  if (stage.data?.path?.startsWith(`${FOLDER}/`)) {
    await run("prefab_close", { save: false, force: true });
  }

  const info = await run("scene_info");
  const loaded = (info.data?.loaded_scenes ?? []).filter((s) => s.path?.startsWith(`${FOLDER}/`));
  if (loaded.length > 0) {
    // A scene cannot be closed while it is the only one loaded.
    if (loaded.length === (info.data?.loaded_scenes ?? []).length) {
      await run("scene_new", { setup: "empty", mode: "additive" });
    }
    for (const scene of loaded) {
      await run("scene_close", { scene: scene.path, force: true });
    }
  }

  await run("asset_delete", { path: FOLDER });
}

function pad(text, width) {
  return String(text).padEnd(width, " ");
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  if (options.help) {
    console.log(
      "Usage: npm run smoke -- [--project <unity project path>] [--keep]\n\n" +
        "  --project  Unity project to target (default: $UCP_TEST_PROJECT, else the only ready editor)\n" +
        "  --keep     leave Assets/UcpSmoke behind for inspection after a failure",
    );
    return 0;
  }
  project = options.project;

  const health = await run("ping", {}, 20_000);
  if (!health.ok) {
    console.error(`UCP smoke — cannot reach Unity: ${describeFailure(health)}`);
    console.error("Open the project in Unity, wait for it to finish compiling, then re-run.");
    return 1;
  }

  console.log(`\nUCP smoke — ${project ?? "auto-detected editor session"}\n`);
  await cleanup();

  const started = Date.now();
  let passed = 0;
  const width = Math.max(...checks.map((c) => c.label.length));

  for (const [index, { label, body }] of checks.entries()) {
    const number = String(index + 1).padStart(2, " ");
    try {
      const detail = await body();
      passed++;
      console.log(`  \u2713 ${number} ${pad(label, width)}  ${detail}`);
    } catch (error) {
      console.log(`  \u2717 ${number} ${pad(label, width)}  FAILED`);
      console.log(`       \u21b3 ${error instanceof Error ? error.message : String(error)}`);
    }
  }

  if (options.keep) {
    console.log(`\n  (--keep) left ${FOLDER} and ${SCENE} in place`);
  } else {
    await cleanup();
    const gone = await run("prefab_info", { path: PREFAB });
    if (gone.ok) console.log(`\n  warning: ${FOLDER} survived cleanup`);
  }

  const seconds = Math.round((Date.now() - started) / 1000);
  const total = checks.length;
  console.log(`\n  ${passed}/${total} passed in ${seconds}s\n`);
  return passed === total ? 0 : 1;
}

main()
  .then((code) => process.exit(code))
  .catch((error) => {
    console.error(
      `\nUCP smoke crashed: ${error instanceof Error ? error.message : String(error)}\n`,
    );
    process.exit(1);
  });
