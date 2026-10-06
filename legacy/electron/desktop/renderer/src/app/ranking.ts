/**
 * Pure ranking/gating functions over snapshot rows — the client-side half of
 * the sweep engine. These run on every slider tick, so keep them allocation-
 * light and synchronous. Mirrors the CLI rules:
 *  - vendor/submarine/venture kinds are never farmable
 *  - expansion gate: item expansion must be <= MSQ progress
 *  - level gate: job level (max of both for job 'both')
 *  - farm tables demand world-scope velocity (dc/region = illiquid mirage)
 */
import { CraftValue, EXPANSIONS, GilConfig, SweepRow } from './models';

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

/** Can the configured crafters make this? 'any'-job recipes gate on the best crafter. */
export function craftableBy(c: CraftValue, cfg: GilConfig): boolean {
  if (c.lvl === null) return true;
  const cap =
    c.job === 'any'
      ? Math.max(...Object.values(cfg.crafters ?? {}), 0) || 100
      : (cfg.crafters?.[c.job] ?? 100);
  return c.lvl <= cap;
}

/**
 * The sweep-page crafting digest: best trustworthy-margin crafts that consume
 * something you can farm at the current sliders. crafts arrive pre-sorted by
 * margin × daily sales (see the sweep service).
 */
export function topValueCrafts(
  crafts: CraftValue[],
  rows: SweepRow[],
  cfg: GilConfig,
  limit = 5,
): CraftValue[] {
  const mine = new Set(
    farmable(rows, cfg)
      .filter((r) => r.kind !== 'crystal' && r.kind !== 'map')
      .map((r) => r.id),
  );
  return crafts
    .filter((c) => c.costComplete && c.velScope === 'world' && c.margin > 0 && craftableBy(c, cfg))
    .filter((c) => c.usesTracked.some((id) => mine.has(id)))
    .slice(0, limit);
}

/** Legendary nodes need their expansion's folklore book — annotate, don't gate (matches the CLI). */
export function needsFolklore(row: SweepRow, cfg: GilConfig): boolean {
  return row.kind === 'legendary' && !(cfg.folklore ?? []).includes(row.expansion);
}

/**
 * What you'd realistically have on retainers: top 10 farmable mats, up to 3
 * liquid maps, and crystals worth the slot (>100k gil/day market throughput).
 */
export function retainerTargets(rows: SweepRow[], cfg: GilConfig): SweepRow[] {
  const f = farmable(rows, cfg);
  const mats = f.filter((r) => r.kind !== 'map' && r.kind !== 'crystal').slice(0, 10);
  const liquidMaps = f
    .filter((r) => r.kind === 'map' && r.velDay >= 2)
    .sort((a, b) => b.avg - a.avg)
    .slice(0, 3);
  const worthCrystals = f.filter((r) => r.kind === 'crystal' && r.throughput > 100_000);
  return [...mats, ...liquidMaps, ...worthCrystals];
}
