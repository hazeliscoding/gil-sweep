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

export interface CraftIngredient {
  id: number;
  name: string;
  qty: number;
  /** Cheapest current listing per unit at sweep time (0 = not priceable). */
  unitPrice: number;
}

/** Craft-margin analysis for one recipe, computed at sweep time from live prices. */
export interface CraftValue {
  id: number;
  name: string;
  job: string;
  lvl: number | null;
  yield: number;
  salePrice: number;
  velDay: number;
  velScope: VelScope;
  /** Σ qty × ingredient min listing. Only trustworthy when costComplete. */
  cost: number;
  /** False when any ingredient had no market price (vendor-only etc.). */
  costComplete: boolean;
  margin: number;
  marginPct: number | null;
  /** Tracked non-crystal item ids among the ingredients ("uses what you farm"). */
  usesTracked: number[];
  ingredients: CraftIngredient[];
}

export interface SweepSnapshot {
  date: string;
  /** ISO timestamp of the run; null for the bundled seed snapshot. */
  timestamp: string | null;
  world: string;
  rows: SweepRow[];
  sbUnknown: SaddlebagUnknown[];
  /** Craft margins (absent on snapshots from before this feature). */
  crafts?: CraftValue[];
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
  /** Expansions whose folklore books are owned (legendary nodes need them). */
  folklore: Expansion[];
  /** Closing the window keeps the app (and its spawn-clock tray) running. */
  closeToTray: boolean;
  /** Starred item ids — get node-window and price-spike notifications. */
  watched: number[];
  saddlebag: SaddlebagParams;
}

/** Deep-partial config update (lets a slider patch one level at a time). */
export interface GilConfigPatch {
  world?: string;
  levels?: Partial<GilConfig['levels']>;
  msqExpansion?: Expansion;
  folklore?: Expansion[];
  closeToTray?: boolean;
  watched?: number[];
  saddlebag?: Partial<SaddlebagParams>;
}

/** One point of an item's local price history (from accumulated snapshots). */
export interface HistoryPoint {
  t: number;
  avg: number;
  velDay: number;
}

/** One item's week-over-week movement in the digest. */
export interface DigestChange {
  id: number;
  name: string;
  avgThen: number;
  avgNow: number;
  avgPct: number | null;
  velThen: number;
  velNow: number;
}

/** Week-over-week story: latest snapshot vs a ~week-old baseline. */
export interface DigestResult {
  world: string;
  latestDate: string;
  baselineDate: string | null;
  daysApart: number | null;
  /** Biggest market-throughput changes, largest first. */
  changes: DigestChange[];
  /** Farm-rotation items below ~5 sold/day (world scope) for 3+ consecutive sweeps. */
  prune: { id: number; name: string; recentVel: number[] }[];
}

export interface SnapshotStats {
  count: number;
  bytes: number;
}

/** Live market drill-down for one item (fetched on demand, not cached). */
export interface MarketDetail {
  curMin: number;
  medPPU: number;
  listedQty: number;
  unitsPerDay: number;
  daysInv: number | null;
  /** Cheapest 10 current listings. */
  listings: { ppu: number; qty: number }[];
  /** Recent sales, newest first (t in ms). Feeds the posting-window histogram. */
  sales: { ppu: number; qty: number; t: number }[];
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
  /** Per-item avg/velocity series across all stored snapshots for the configured world. */
  sweepHistory(): Promise<Record<number, HistoryPoint[]>>;
  /** One-time Universalis sale-history backfill for the configured world; resolves with items covered. */
  backfillHistory(): Promise<number>;
  /** Week-over-week digest for the configured world (local snapshots only, instant). */
  sweepDigest(): Promise<DigestResult>;
  /** Snapshot archive size for the housekeeping UI. */
  snapshotStats(): Promise<SnapshotStats>;
  /** Keeps the newest snapshot per world+day, deletes the rest; resolves with count deleted. */
  pruneSnapshots(): Promise<{ deleted: number }>;
  /** Live market drill-down (listings + recent sales) for one item. */
  marketDetail(id: number): Promise<MarketDetail>;
  /** All public world names, for the world picker. */
  listWorlds(): Promise<string[]>;
  /** Live listings + recent sales → per-item selling advice for the given targets. */
  retainerPlan(targets: RetainerTarget[]): Promise<RetainerAdvice[]>;
  /** Reveals the snapshots/config folder in the OS file manager. */
  openDataFolder(): Promise<string>;
}
