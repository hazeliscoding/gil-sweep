/**
 * Demand-side attribution: why does an item sell? Signals come from the bundled
 * data/garland-demand.json (recipe consumers with qty-per-craft, leve turn-ins,
 * GC supply missions, quest usage) — extracted from Garland Tools per patch and
 * shipped with the app. Consumers are ranked by velDay × qty ≈ units of the
 * tracked item the market burns per day.
 */
import { readFileSync } from 'fs';
import { join } from 'path';
import { DemandConsumer, DemandInfo, TrackedItem } from '../../shared/types';

export interface GarlandSignals {
  v: number;
  id: number;
  recipeCount: number;
  consumers: { id: number; name: string; qty: number }[];
  leves: number;
  supply: { count: number; seals: number } | null;
  quests: number;
}

export class DemandIndex {
  constructor(private readonly byId: Record<string, GarlandSignals>) {}

  get(id: number): GarlandSignals | null {
    return this.byId[String(id)] ?? null;
  }

  /** Every consumer id across all items — priced in the same Universalis pass as the items. */
  consumerIds(): number[] {
    const ids = new Set<number>();
    for (const d of Object.values(this.byId)) for (const c of d.consumers) ids.add(c.id);
    return [...ids];
  }
}

export function loadDemandIndex(dataDir: string): DemandIndex {
  const raw = JSON.parse(readFileSync(join(dataDir, 'garland-demand.json'), 'utf8'));
  return new DemandIndex(raw);
}

/** Builds the human "why it sells" line + the ranked consumer list for one item. */
export function summarizeDemand(
  item: TrackedItem,
  d: GarlandSignals | null,
  velOf: (id: number) => number,
): { why: string; demand: DemandInfo | null } {
  if (item.kind === 'crystal') {
    return {
      why: 'universal craft mat',
      demand: d ? toInfo(d, []) : null,
    };
  }
  if (!d) return { why: '', demand: null };

  const ranked: DemandConsumer[] = d.consumers
    .map((c) => ({ ...c, velDay: velOf(c.id) }))
    .sort((a, b) => b.velDay * b.qty - a.velDay * a.qty || b.qty - a.qty);

  const parts = ranked
    .slice(0, 2)
    .map((c) => `${c.name} ×${c.qty}${c.velDay ? ` (${c.velDay}/d)` : ''}`);
  const more = d.recipeCount - 2;
  if (more > 0) parts.push(`+${more} more recipe${more === 1 ? '' : 's'}`);
  if (d.leves) parts.push(`leves ×${d.leves}`);
  if (d.supply) parts.push('GC supply');
  if (d.quests) parts.push(`quests ×${d.quests}`);

  let why = parts.join(' · ');
  if (!why && item.kind === 'diadem') why = 'Ishgardian Restoration turn-in';

  return { why, demand: toInfo(d, ranked.slice(0, 5)) };
}

function toInfo(d: GarlandSignals, topConsumers: DemandConsumer[]): DemandInfo {
  return {
    recipeCount: d.recipeCount,
    leves: d.leves,
    supply: d.supply,
    quests: d.quests,
    topConsumers,
  };
}
