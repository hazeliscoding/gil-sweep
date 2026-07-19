/**
 * Preload bridge: the only surface the (context-isolated) renderer can touch in
 * the main process. Each method is a typed wrapper over ipcRenderer.invoke whose
 * channel name matches a handler in ipc.ts 1:1 — keep this object, {@link GilApi},
 * and registerIpc in sync. Exposed on `window.api`.
 */
import { contextBridge, ipcRenderer } from 'electron';
import type { GilApi } from '../shared/types';

const api: GilApi = {
  health: () => ipcRenderer.invoke('health'),
  getConfig: () => ipcRenderer.invoke('config:get'),
  setConfig: (patch) => ipcRenderer.invoke('config:set', patch),
  listItems: () => ipcRenderer.invoke('items:list'),
  trackItem: (query) => ipcRenderer.invoke('items:track', query),
  removeCustomItem: (id) => ipcRenderer.invoke('items:removeCustom', id),
  isFirstRun: async () => !(await ipcRenderer.invoke('config:exists')),
  latestSweep: () => ipcRenderer.invoke('sweep:latest'),
  runSweep: () => ipcRenderer.invoke('sweep:run'),
  sweepHistory: () => ipcRenderer.invoke('sweep:history'),
  backfillHistory: () => ipcRenderer.invoke('sweep:backfill'),
  sweepDigest: () => ipcRenderer.invoke('sweep:digest'),
  snapshotStats: () => ipcRenderer.invoke('snapshots:stats'),
  pruneSnapshots: () => ipcRenderer.invoke('snapshots:prune'),
  marketDetail: (id) => ipcRenderer.invoke('market:detail', id),
  listWorlds: () => ipcRenderer.invoke('worlds:list'),
  retainerPlan: (targets) => ipcRenderer.invoke('retainer:plan', targets),
  openDataFolder: () => ipcRenderer.invoke('data:openFolder'),
};

contextBridge.exposeInMainWorld('api', api);
