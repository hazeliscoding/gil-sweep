/**
 * Renderer-side mirror of desktop/src/shared/types.ts — keep the two in sync.
 */

export type Job = 'MIN' | 'BTN' | 'both';
export type Expansion = 'ARR' | 'HW' | 'StB' | 'ShB' | 'EW' | 'DT';
export const EXPANSIONS: Expansion[] = ['ARR', 'HW', 'StB', 'ShB', 'EW', 'DT'];

export type VelScope = 'world' | 'dc' | 'region' | '-';

export interface TrackedItem {
  name: string;
  id: number;
  job: Job;
  level: number;
  where: string;
  kind: string;
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
  qty: number;
  velDay: number;
}

export interface DemandInfo {
  recipeCount: number;
  leves: number;
  supply: { count: number; seals: number } | null;
  quests: number;
  topConsumers: DemandConsumer[];
}

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
  unitPrice: number;
  viaCraft?: boolean;
}

/** Craft-margin analysis for one recipe, computed at sweep time from live prices. */
export interface CraftValue {
  id: number;
  name: string;
  job: string;
  lvl: number | null;
  yield: number;
  salePrice: number;
  hq?: boolean;
  velDay: number;
  velScope: VelScope;
  cost: number;
  costComplete: boolean;
  margin: number;
  marginPct: number | null;
  usesTracked: number[];
  ingredients: CraftIngredient[];
}

export interface SweepSnapshot {
  date: string;
  timestamp: string | null;
  world: string;
  rows: SweepRow[];
  sbUnknown: SaddlebagUnknown[];
  crafts?: CraftValue[];
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
  /** Crafter job levels (CRP/BSM/ARM/GSM/LTW/WVR/ALC/CUL) — gate the Crafting page. */
  crafters: Record<string, number>;
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
  crafters?: Record<string, number>;
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
  changes: DigestChange[];
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
  listings: { ppu: number; qty: number }[];
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

/** Live-listings selling advice for one item. */
export interface RetainerAdvice {
  id: number;
  curMin: number;
  medPPU: number;
  listPrice: number;
  stack: number;
  daysInv: number | null;
  unitsPerDay: number;
  verdict: string;
}
