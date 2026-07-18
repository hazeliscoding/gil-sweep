/**
 * Pure ranking/gating functions over snapshot rows — the client-side half of
 * the sweep engine. These run on every slider tick, so keep them allocation-
 * light and synchronous. Mirrors the CLI rules:
 *  - vendor/submarine/venture kinds are never farmable
 *  - expansion gate: item expansion must be <= MSQ progress
 *  - level gate: job level (max of both for job 'both')
 *  - farm tables demand world-scope velocity (dc/region = illiquid mirage)
 */
import { EXPANSIONS, GilConfig, SweepRow } from './models';

const NEVER_FARM = new Set(['vendor', 'submarine', 'venture']);

export function lockReason(row: SweepRow, cfg: GilConfig): string {
  if (row.kind === 'submarine') return 'FC submarine loot';
  if (row.kind === 'vendor') return 'vendor/scrip flip';
  if (row.kind === 'venture') return 'retainer venture';
  if (EXPANSIONS.indexOf(row.expansion) > EXPANSIONS.indexOf(cfg.msqExpansion)) {
    return `${row.expansion} content`;
  }
  const lvl =
    row.job === 'both' ? Math.max(cfg.levels.MIN, cfg.levels.BTN) : cfg.levels[row.job];
  if (row.level > lvl) return `needs ${row.job === 'both' ? 'level' : row.job} ${row.level}`;
  return '';
}

export function accessible(row: SweepRow, cfg: GilConfig): boolean {
  return lockReason(row, cfg) === '';
}

export function farmable(rows: SweepRow[], cfg: GilConfig): SweepRow[] {
  return rows.filter(
    (r) => accessible(r, cfg) && r.velDay > 0 && r.velScope === 'world' && !NEVER_FARM.has(r.kind),
  );
}

export function miningFarms(rows: SweepRow[], cfg: GilConfig, limit = 12): SweepRow[] {
  return farmable(rows, cfg)
    .filter((r) => r.job === 'MIN' && r.kind !== 'map' && r.kind !== 'crystal')
    .slice(0, limit);
}

export function botanyFarms(rows: SweepRow[], cfg: GilConfig, limit = 12): SweepRow[] {
  return farmable(rows, cfg)
    .filter((r) => r.job === 'BTN' && r.kind !== 'map' && r.kind !== 'crystal')
    .slice(0, limit);
}

export function maps(rows: SweepRow[], cfg: GilConfig): SweepRow[] {
  return farmable(rows, cfg).filter((r) => r.kind === 'map');
}

/** One map per 18h per character — the single pick: highest avg with velocity ≥ 2/day. */
export function bestMap(rows: SweepRow[], cfg: GilConfig): SweepRow | null {
  return (
    maps(rows, cfg)
      .filter((r) => r.velDay >= 2)
      .sort((a, b) => b.avg - a.avg)[0] ?? null
  );
}

export function crystals(rows: SweepRow[], cfg: GilConfig): SweepRow[] {
  return farmable(rows, cfg).filter((r) => r.kind === 'crystal');
}

/** ≥25% price swing with real liquidity — the reason to sweep weekly. */
export function movers(rows: SweepRow[]): SweepRow[] {
  return rows.filter(
    (r) => r.avgChangePct !== null && Math.abs(r.avgChangePct) >= 25 && r.velDay >= 5,
  );
}

export function locked(rows: SweepRow[], cfg: GilConfig, limit = 12): SweepRow[] {
  return rows.filter((r) => !accessible(r, cfg) && r.throughput > 0).slice(0, limit);
}
