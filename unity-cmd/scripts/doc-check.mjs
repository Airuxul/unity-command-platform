import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, '..');
const connectorRoot = path.join(root, '..', 'com.air.unity-connector');

function readVersion(pkgPath) {
  const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
  return pkg.version;
}

function readDocVersion(docPath) {
  const text = fs.readFileSync(docPath, 'utf8');
  const match = text.match(/^Version:\s*(\S+)/m);
  return match?.[1] ?? null;
}

function readConnectorBuild() {
  const text = fs.readFileSync(
    path.join(connectorRoot, 'Runtime', 'ConnectorBuild.cs'),
    'utf8',
  );
  const match = text.match(/public const int Id = (\d+)/);
  return match ? Number(match[1]) : null;
}

function readMinConnectorBuild() {
  const text = fs.readFileSync(path.join(root, 'src', 'constants.js'), 'utf8');
  const match = text.match(/MIN_CONNECTOR_BUILD\s*=\s*(\d+)/);
  return match ? Number(match[1]) : null;
}

function readScenarioBuilds() {
  const dir = path.join(root, 'tests', 'integration', 'scenarios');
  const values = [];
  for (const file of fs.readdirSync(dir).filter((f) => f.endsWith('.json'))) {
    const json = JSON.parse(fs.readFileSync(path.join(dir, file), 'utf8'));
    for (const step of json.steps ?? []) {
      if (step.expectConnectorBuild != null)
        values.push(step.expectConnectorBuild);
    }
  }
  return values;
}

function readDefaultPorts() {
  const text = fs.readFileSync(path.join(root, 'src', 'constants.js'), 'utf8');
  const read = (name) => {
    const match = text.match(new RegExp(`export const ${name} = (\\d+)`));
    return match ? Number(match[1]) : null;
  };
  return {
    editor: read('DEFAULT_EDITOR_PORT'),
    editorPlay: read('DEFAULT_EDITOR_PLAY_PORT'),
    player: read('DEFAULT_PLAYER_PORT'),
  };
}

function checkReadmePorts(readmePath, ports) {
  if (!fs.existsSync(readmePath)) {
    console.error(`Missing ${readmePath}`);
    return false;
  }
  const text = fs.readFileSync(readmePath, 'utf8');
  let ok = true;
  for (const port of [ports.editor, ports.editorPlay, ports.player]) {
    if (!text.includes(String(port))) {
      console.error(`${readmePath}: missing default port ${port} (see src/constants.js)`);
      ok = false;
    }
  }
  return ok;
}

const cmdVer = readVersion(path.join(root, 'package.json'));
const conVer = readVersion(path.join(connectorRoot, 'package.json'));
const connectorBuild = readConnectorBuild();
const minBuild = readMinConnectorBuild();
const scenarioBuilds = readScenarioBuilds();
const defaultPorts = readDefaultPorts();

const docs = [
  [path.join(root, 'docs', 'IMPLEMENTATION.md'), cmdVer],
  [path.join(connectorRoot, 'docs', 'IMPLEMENTATION.md'), conVer],
];

let ok = true;
for (const [docPath, expected] of docs) {
  if (!fs.existsSync(docPath)) {
    console.error(`Missing ${docPath}`);
    ok = false;
    continue;
  }
  const docVer = readDocVersion(docPath);
  if (docVer !== expected) {
    console.error(`${docPath}: Version ${docVer} !== package.json ${expected}`);
    ok = false;
  }
}

if (connectorBuild == null || minBuild == null || connectorBuild !== minBuild) {
  console.error(
    `ConnectorBuild.Id (${connectorBuild}) must match MIN_CONNECTOR_BUILD (${minBuild})`,
  );
  ok = false;
}

for (const build of scenarioBuilds) {
  if (build !== connectorBuild) {
    console.error(
      `Integration scenario expectConnectorBuild (${build}) !== ConnectorBuild.Id (${connectorBuild})`,
    );
    ok = false;
  }
}

if (
  defaultPorts.editor == null ||
  defaultPorts.editorPlay == null ||
  defaultPorts.player == null
) {
  console.error('Could not read DEFAULT_*_PORT from src/constants.js');
  ok = false;
} else {
  ok =
    checkReadmePorts(path.join(root, 'README.md'), defaultPorts) && ok;
  ok =
    checkReadmePorts(path.join(root, 'README.zh-CN.md'), defaultPorts) && ok;
}

if (!ok) process.exit(1);
console.log('doc:check OK');
