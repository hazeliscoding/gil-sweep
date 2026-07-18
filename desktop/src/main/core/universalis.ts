/**
 * Universalis client (aggregated prices + world list). World-scope values are
 * preferred and the answering scope is recorded — non-world velocity means the
 * item barely trades on the configured world (see shared/types VelScope).
 */
import { VelScope } from '../../shared/types';

/** Universalis aggregated entry for one item (nq/hq × price/velocity × scope). */
export type AggregatedEntry = any;

interface ScopedValue {
  world?: { price?: number; quantity?: number };
  dc?: { price?: number; quantity?: number };
  region?: { price?: number; quantity?: number };
}

/** world ?? dc ?? region — the first scope that has data. */
export function pick(o: ScopedValue | undefined): { price?: number; quantity?: number } | null {
  if (!o) return null;
  return o.world ?? o.dc ?? o.region ?? null;
}

/** Which scope answered (for the illiquidity flag). */
export function scopeOf(o: ScopedValue | undefined): VelScope {
  if (!o) return '-';
  return o.world ? 'world' : o.dc ? 'dc' : o.region ? 'region' : '-';
}

/** Prices up to thousands of ids in chunks of 100 (the aggregated endpoint's max). */
export async function fetchAggregated(world: string, ids: number[]): Promise<Map<number, AggregatedEntry>> {
  const out = new Map<number, AggregatedEntry>();
  for (let i = 0; i < ids.length; i += 100) {
    const chunk = ids.slice(i, i + 100);
    const r = await fetch(`https://universalis.app/api/v2/aggregated/${world}/${chunk.join(',')}`, {
      signal: AbortSignal.timeout(30000),
    });
    if (!r.ok) throw new Error(`Universalis HTTP ${r.status}`);
    const j: any = await r.json();
    for (const it of j.results ?? []) out.set(it.itemId, it);
  }
  return out;
}

/** All public world names, sorted, for the world picker. */
export async function listWorlds(): Promise<string[]> {
  const r = await fetch('https://universalis.app/api/v2/worlds', {
    signal: AbortSignal.timeout(30000),
  });
  if (!r.ok) throw new Error(`Universalis worlds HTTP ${r.status}`);
  const j: any = await r.json();
  return (j as { id: number; name: string }[])
    .map((w) => w.name)
    .sort((a, b) => a.localeCompare(b));
}
