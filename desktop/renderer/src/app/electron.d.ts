import {
  GilConfig,
  GilConfigPatch,
  HealthResult,
  HistoryPoint,
  MarketDetail,
  RetainerAdvice,
  RetainerTarget,
  SweepSnapshot,
  TrackedItem,
} from './models';

/**
 * The IPC bridge exposed by the Electron preload as `window.api`. Every method
 * is a Promise-returning wrapper over an `ipcRenderer.invoke` round-trip to a
 * handler in the main process; the renderer never touches fs/network directly.
 */
export interface GilApi {
  health(): Promise<HealthResult>;
  getConfig(): Promise<GilConfig>;
  setConfig(patch: GilConfigPatch): Promise<GilConfig>;
  listItems(): Promise<TrackedItem[]>;
  latestSweep(): Promise<SweepSnapshot | null>;
  runSweep(): Promise<SweepSnapshot>;
  sweepHistory(): Promise<Record<number, HistoryPoint[]>>;
  backfillHistory(): Promise<number>;
  marketDetail(id: number): Promise<MarketDetail>;
  listWorlds(): Promise<string[]>;
  retainerPlan(targets: RetainerTarget[]): Promise<RetainerAdvice[]>;
  openDataFolder(): Promise<string>;
}

declare global {
  // Augments the global Window so `window.api` is typed everywhere without an import.
  interface Window {
    api: GilApi;
  }
}

export {};
