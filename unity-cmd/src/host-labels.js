import { normalizeHostKind } from './client/connection.js';
import { HOST_KIND } from './constants.js';

export function hostKindLabel(hostKind) {
  const k = normalizeHostKind(hostKind);
  if (k === HOST_KIND.Editor) return 'Unity Editor';
  if (k === HOST_KIND.EditorPlay) return 'Editor Play Mode';
  if (k === HOST_KIND.Player) return 'Dev Player';
  return hostKind;
}

export function hostKindDescription(hostKind) {
  return hostKindLabel(hostKind);
}

export function reachHint(hostKind) {
  const k = normalizeHostKind(hostKind);
  if (k === HOST_KIND.Editor) return 'Open the project in Unity Editor.';
  if (k === HOST_KIND.EditorPlay) return 'Enter Play Mode in the Editor.';
  if (k === HOST_KIND.Player) return 'Run a Development Build with com.air.unity-connector.';
  return '';
}
