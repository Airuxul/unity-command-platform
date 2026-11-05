/**
 * Records the structure of every successful result — keys and value types, not values — so a
 * refactor that drops, renames or retypes a field fails a snapshot instead of passing silently.
 *
 * Each command's shape is the merge of every call the file made: object keys that only some calls
 * (or some array elements) returned are marked with a trailing `?`, and a field seen with several
 * types lists them all. The scenarios are deterministic, so the merged shape is too.
 */
type Shape = string | Shape[] | { [key: string]: Shape } | { $union: Shape[] };

const shapes = new Map<string, Shape>();

export function recordShape(command: string, data: unknown): void {
  const shape = shapeOf(data);
  const previous = shapes.get(command);
  shapes.set(command, previous === undefined ? shape : merge(previous, shape));
}

/** The recorded shapes as snapshot text, commands in name order. */
export function recordedShapes(): string {
  const sorted = Object.fromEntries([...shapes.entries()].sort(([a], [b]) => a.localeCompare(b)));
  return `${JSON.stringify(sorted, null, 2)}\n`;
}

function shapeOf(value: unknown): Shape {
  if (value === null || value === undefined) return "null";
  if (Array.isArray(value)) {
    if (value.length === 0) return [];
    return [value.map(shapeOf).reduce(merge)];
  }
  if (typeof value === "object") {
    const entries = Object.entries(value as Record<string, unknown>)
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([k, v]) => [k, shapeOf(v)] as const);
    return Object.fromEntries(entries);
  }
  return typeof value;
}

function isUnion(s: Shape): s is { $union: Shape[] } {
  return typeof s === "object" && !Array.isArray(s) && "$union" in s;
}

function isObject(s: Shape): s is { [key: string]: Shape } {
  return typeof s === "object" && !Array.isArray(s) && !("$union" in s);
}

function merge(a: Shape, b: Shape): Shape {
  if (JSON.stringify(a) === JSON.stringify(b)) return a;
  if (Array.isArray(a) && Array.isArray(b)) {
    if (a.length === 0) return b;
    if (b.length === 0) return a;
    return [merge(a[0], b[0])];
  }
  if (isObject(a) && isObject(b)) return mergeObjects(a, b);
  return union(a, b);
}

function mergeObjects(a: { [key: string]: Shape }, b: { [key: string]: Shape }): Shape {
  const strip = (k: string) => (k.endsWith("?") ? k.slice(0, -1) : k);
  const lookup = (o: { [key: string]: Shape }, k: string) => o[k] ?? o[`${k}?`];
  const keys = new Set([...Object.keys(a), ...Object.keys(b)].map(strip));
  const out: { [key: string]: Shape } = {};
  for (const key of [...keys].sort()) {
    const left = lookup(a, key);
    const right = lookup(b, key);
    const optional = left === undefined || right === undefined || `${key}?` in a || `${key}?` in b;
    const merged = left === undefined ? right : right === undefined ? left : merge(left, right);
    out[optional ? `${key}?` : key] = merged as Shape;
  }
  return out;
}

function union(a: Shape, b: Shape): Shape {
  const members = [...(isUnion(a) ? a.$union : [a]), ...(isUnion(b) ? b.$union : [b])];
  // Fold same-kind members together so {a} | {a, b} becomes {a, b?} rather than two entries.
  const folded: Shape[] = [];
  for (const m of members) {
    const i = folded.findIndex(
      (f) =>
        JSON.stringify(f) === JSON.stringify(m) ||
        (isObject(f) && isObject(m)) ||
        (Array.isArray(f) && Array.isArray(m)),
    );
    if (i < 0) folded.push(m);
    else folded[i] = merge(folded[i], m);
  }
  if (folded.length === 1) return folded[0];
  folded.sort((x, y) => JSON.stringify(x).localeCompare(JSON.stringify(y)));
  return { $union: folded };
}
