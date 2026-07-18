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
  latestSweep: () => ipcRenderer.invoke('sweep:latest'),
  runSweep: () => ipcRenderer.invoke('sweep:run'),
  listWorlds: () => ipcRenderer.invoke('worlds:list'),
  retainerPlan: (targets) => ipcRenderer.invoke('retainer:plan', targets),
  openDataFolder: () => ipcRenderer.invoke('data:openFolder'),
};

contextBridge.exposeInMainWorld('api', api);
