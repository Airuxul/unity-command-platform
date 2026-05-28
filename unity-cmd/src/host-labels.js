import { normalizeHostKind } from './client/connection.js';

export function hostKindLabel(hostKind) {
  const k = normalizeHostKind(hostKind);
  if (k === 'editor') return 'Unity Editor';
  if (k === 'editor_play') return 'Editor Play Mode';
  if (k === 'player') return 'Dev Player';
  return hostKind;
}

export function hostKindDescription(hostKind) {
  return hostKindLabel(hostKind);
}

export function reachHint(hostKind) {
  const k = normalizeHostKind(hostKind);
  if (k === 'editor') return 'Open the project in Unity Editor.';
  if (k === 'editor_play') return 'Enter Play Mode in the Editor.';
  if (k === 'player') return 'Run a Development Build with unity-connector.';
  return '';
}
