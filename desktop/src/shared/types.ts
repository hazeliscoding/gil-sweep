/**
 * DTOs shared between the Electron main process and the Angular renderer.
 * The renderer mirrors these in src/app/models.ts — keep the two in sync.
 */

export type Job = 'MIN' | 'BTN' | 'both';
export type Expansion = 'ARR' | 'HW' | 'StB' | 'ShB' | 'EW' | 'DT';
export const EXPANSIONS: Expansion[] = ['ARR', 'HW', 'StB', 'ShB', 'EW', 'DT'];

/** Which Universalis scope answered a price/velocity query. Non-world = illiquid on-world. */
export type VelScope = 'world' | 'dc' | 'region' | '-';

/** One curated entry from data/items.json. */
export interface TrackedItem {
  name: string;
  id: number;
  job: Job;
  level: number;
  where: string;
  kind: string; // node | unspoiled | legendary | ephemeral | map | crystal | diadem | vendor | submarine | venture | reduction
  expansion: Expansion;
  note?: string;
  /** Timed nodes: spawn hours in Eorzea time (e.g. [10, 22]). */
  spawns?: number[];
  /** Timed nodes: window length in Eorzea MINUTES (120 = 2 ET hours ≈ 5.8 real min). */
  uptime?: number;
}

export interface DemandConsumer {
  id: number;
  name: string;
  /** Units of the tracked item consumed per craft of this consumer. */
  qty: number;
  /** Consumer's own sold/day on the configured world (0 = unknown/unmarketable). */
  velDay: number;
}

/** Demand-side attribution: why an item sells (from bundled Garland signals). */
export interface DemandInfo {
  recipeCount: number;
  leves: number;
  supply: { count: number; seals: number } | null;
  quests: number;
  topConsumers: DemandConsumer[];
}

/** One priced item in a sweep snapshot. Accessibility is derived in the renderer. */
export interface SweepRow extends TrackedItem {
  avg: number;
  min: number;
  velDay: number;
  velScope: VelScope;
  throughput: number;
  sbState: string | null;
  sbSoldWeek: number | null;
  avgChangePct: number | null;
  why: string;
  demand: DemandInfo | null;
}

/** Saddlebag top seller we don't track yet (candidate for the item DB). */
export interface SaddlebagUnknown {
  name: string;
  id: number;
  avg: number;
  soldWeek: number;
  state: string;
}

export interface SweepSnapshot {
  date: string;
  /** ISO timestamp of the run; null for the bundled seed snapshot. */
  timestamp: string | null;
  world: string;
  rows: SweepRow[];
  sbUnknown: SaddlebagUnknown[];
  /** True when this is the bundled first-boot seed, not a live sweep. */
  seed?: boolean;
}

export interface SaddlebagParams {
  timePeriod: number;
  salesAmount: number;
  averagePrice: number;
  filters: number[];
}

export interface GilConfig {
  world: string;
  levels: { MIN: number; BTN: number };
  msqExpansion: Expansion;
  saddlebag: SaddlebagParams;
}

/** Deep-partial config update (lets a slider patch one level at a time). */
export interface GilConfigPatch {
  world?: string;
  levels?: Partial<GilConfig['levels']>;
  msqExpansion?: Expansion;
  saddlebag?: Partial<SaddlebagParams>;
}

export interface HealthResult {
  status: string;
}

/** What the renderer asks retainer advice for: id + kind (maps always stack 1). */
export interface RetainerTarget {
  id: number;
  kind: string;
}

/** Live-listings selling advice for one item (see core/retainer.service.ts). */
export interface RetainerAdvice {
  id: number;
  /** Cheapest current listing (0 = no listings). */
  curMin: number;
  /** Median price recent sales actually cleared at. */
  medPPU: number;
  /** Recommended list price (undercut, or hold near clearing price on a crashed floor). */
  listPrice: number;
  /** Recommended stack size (median sale quantity, rounded to a natural size). */
  stack: number;
  /** Listed quantity ÷ live sales rate; null when nothing is selling. */
  daysInv: number | null;
  /** Units/day from the recent-sales window (live, not the sweep aggregate). */
  unitsPerDay: number;
  verdict: string;
}

/**
 * The IPC bridge exposed by the Electron preload as `window.api`. Channel names
 * in ipc.ts map 1:1 to these methods — keep preload.ts, ipc.ts, and this
 * interface in sync.
 */
export interface GilApi {
  health(): Promise<HealthResult>;
  getConfig(): Promise<GilConfig>;
  setConfig(patch: GilConfigPatch): Promise<GilConfig>;
  /** The bundled curated item DB (for spawn clocks etc. on older snapshots). */
  listItems(): Promise<TrackedItem[]>;
  /** Newest stored snapshot, else the bundled seed, else null. Never fetches. */
  latestSweep(): Promise<SweepSnapshot | null>;
  /** Runs a live sweep against Universalis/Saddlebag and persists the snapshot. */
  runSweep(): Promise<SweepSnapshot>;
  /** All public world names, for the world picker. */
  listWorlds(): Promise<string[]>;
  /** Live listings + recent sales → per-item selling advice for the given targets. */
  retainerPlan(targets: RetainerTarget[]): Promise<RetainerAdvice[]>;
  /** Reveals the snapshots/config folder in the OS file manager. */
  openDataFolder(): Promise<string>;
}
