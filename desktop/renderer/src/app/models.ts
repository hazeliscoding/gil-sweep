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

export interface SweepSnapshot {
  date: string;
  timestamp: string | null;
  world: string;
  rows: SweepRow[];
  sbUnknown: SaddlebagUnknown[];
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
