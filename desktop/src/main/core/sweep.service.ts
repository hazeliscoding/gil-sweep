/**
 * The sweep engine: prices every tracked item on the configured world, joins
 * Saddlebag trend state, attributes demand ("why it sells"), computes Δ% vs the
 * previous snapshot, and persists the result as JSON in userData/snapshots.
 *
 * Accessibility (level/MSQ gating) is deliberately NOT computed here — the
 * renderer derives it from the raw rows so slider changes re-rank instantly
 * without re-fetching. Ported from the ffxiv-market-sweep CLI.
 */
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'fs';
import { join } from 'path';
import {
  CraftValue,
  GilConfig,
  HistoryPoint,
  SaddlebagUnknown,
  SweepRow,
  SweepSnapshot,
  TrackedItem,
} from '../../shared/types';
import { DemandIndex, summarizeDemand } from './demand';
import { fetchAggregated, pick, scopeOf } from './universalis';
import { fetchSaddlebag } from './saddlebag';

/** One recipe from the bundled crafts.json (see the data pipeline repo). */
interface CraftRecipe {
  id: number;
  name: string;
  job: string;
  rlvl: number | null;
  lvl: number | null;
  yield: number;
  ingredients: { id: number; name: string; qty: number }[];
}

export class SweepService {
  readonly items: TrackedItem[];
  private readonly craftRecipes: Record<string, CraftRecipe>;
  /** Tracked non-crystal ids — crystals are in every recipe, so they don't count as "uses my farmables". */
  private readonly trackedMatIds: Set<number>;

  constructor(
    private readonly dataDir: string,
    private readonly snapshotsDir: string,
    private readonly demand: DemandIndex,
  ) {
    this.items = JSON.parse(readFileSync(join(dataDir, 'items.json'), 'utf8'));
    this.craftRecipes = JSON.parse(readFileSync(join(dataDir, 'crafts.json'), 'utf8'));
    this.trackedMatIds = new Set(this.items.filter((i) => i.kind !== 'crystal').map((i) => i.id));
    mkdirSync(snapshotsDir, { recursive: true });
  }

  /** Newest stored snapshot; falls back to the bundled seed so first boot has data. */
  latest(): SweepSnapshot | null {
    const files = existsSync(this.snapshotsDir)
      ? readdirSync(this.snapshotsDir).filter((f) => f.endsWith('.json')).sort()
      : [];
    if (files.length) {
      return JSON.parse(readFileSync(join(this.snapshotsDir, files[files.length - 1]), 'utf8'));
    }
    const seedFile = join(this.dataDir, 'seed-snapshot.json');
    return existsSync(seedFile) ? JSON.parse(readFileSync(seedFile, 'utf8')) : null;
  }

  /**
   * Per-item price/velocity series across every stored snapshot (plus the
   * bundled seed) for one world — the data behind the sparklines. Grows with
   * every sweep; reads a handful of local JSON files, so it stays instant.
   */
  history(world: string): Record<number, HistoryPoint[]> {
    const snaps: SweepSnapshot[] = [];
    const seedFile = join(this.dataDir, 'seed-snapshot.json');
    if (existsSync(seedFile)) snaps.push(JSON.parse(readFileSync(seedFile, 'utf8')));
    if (existsSync(this.snapshotsDir)) {
      for (const f of readdirSync(this.snapshotsDir).filter((f) => f.endsWith('.json')).sort()) {
        snaps.push(JSON.parse(readFileSync(join(this.snapshotsDir, f), 'utf8')));
      }
    }
    const out: Record<number, HistoryPoint[]> = {};
    for (const s of snaps) {
      if (s.world !== world) continue;
      const t = s.timestamp ? Date.parse(s.timestamp) : Date.parse(s.date);
      if (!Number.isFinite(t)) continue;
      for (const r of s.rows) (out[r.id] ??= []).push({ t, avg: r.avg, velDay: r.velDay });
    }
    for (const series of Object.values(out)) {
      series.sort((a, b) => a.t - b.t);
      if (series.length > 60) series.splice(0, series.length - 60);
    }
    return out;
  }

  async run(config: GilConfig): Promise<SweepSnapshot> {
    // One pricing pass covers everything: tracked items, demand consumers
    // (= the craftable goods), and every recipe ingredient for margin math.
    const ingredientIds = Object.values(this.craftRecipes).flatMap((r) =>
      r.ingredients.map((i) => i.id),
    );
    const ids = [
      ...new Set([...this.items.map((i) => i.id), ...this.demand.consumerIds(), ...ingredientIds]),
    ];
    const prices = await fetchAggregated(config.world, ids);
    const saddlebag = await fetchSaddlebag(config.world, config.saddlebag);
    const sbById = new Map((saddlebag ?? []).map((s) => [Number(s.itemID), s]));

    // Δ% vs whatever the user last saw (stored snapshot or the seed), same world only.
    const prev = this.latest();
    const prevById = prev && prev.world === config.world ? new Map(prev.rows.map((r) => [r.id, r])) : null;

    const velOf = (id: number): number =>
      +(((pick(prices.get(id)?.nq?.dailySaleVelocity)?.quantity ?? 0) as number).toFixed(1));

    const rows: SweepRow[] = this.items.map((item) => {
      const p = prices.get(item.id);
      const avg = Math.round((pick(p?.nq?.averageSalePrice)?.price ?? 0) as number);
      const min = Math.round((pick(p?.nq?.minListing)?.price ?? 0) as number);
      const velDay = velOf(item.id);
      const sb = sbById.get(item.id);
      const prevRow = prevById?.get(item.id);
      const { why, demand } = summarizeDemand(item, this.demand.get(item.id), velOf);
      return {
        ...item,
        avg,
        min,
        velDay,
        velScope: scopeOf(p?.nq?.dailySaleVelocity),
        throughput: Math.round(avg * velDay),
        sbState: sb?.state ?? null,
        sbSoldWeek: sb?.quantitySold ?? null,
        avgChangePct: prevRow?.avg ? +((((avg - prevRow.avg) / prevRow.avg) * 100).toFixed(1)) : null,
        why,
        demand,
      };
    });
    rows.sort((a, b) => b.throughput - a.throughput);

    const knownIds = new Set(this.items.map((i) => i.id));
    const sbUnknown: SaddlebagUnknown[] = (saddlebag ?? [])
      .filter((s) => !knownIds.has(Number(s.itemID)))
      .slice(0, 15)
      .map((s) => ({
        name: s.name,
        id: Number(s.itemID),
        avg: s.avg,
        soldWeek: s.quantitySold,
        state: s.state,
      }));

    // Craft margins: sale price × yield − Σ(qty × ingredient min listing).
    const crafts: CraftValue[] = [];
    for (const r of Object.values(this.craftRecipes)) {
      const p = prices.get(r.id);
      const salePrice = Math.round((pick(p?.nq?.averageSalePrice)?.price ?? 0) as number);
      const craftVelDay = velOf(r.id);
      if (!salePrice || !craftVelDay) continue; // dead market — not worth listing
      let cost = 0;
      let costComplete = true;
      const ingredients = r.ingredients.map((ing) => {
        const unitPrice = Math.round((pick(prices.get(ing.id)?.nq?.minListing)?.price ?? 0) as number);
        if (!unitPrice) costComplete = false;
        cost += unitPrice * ing.qty;
        return { ...ing, unitPrice };
      });
      const margin = salePrice * r.yield - cost;
      crafts.push({
        id: r.id,
        name: r.name,
        job: r.job,
        lvl: r.lvl,
        yield: r.yield,
        salePrice,
        velDay: craftVelDay,
        velScope: scopeOf(p?.nq?.dailySaleVelocity),
        cost,
        costComplete,
        margin,
        marginPct: cost > 0 ? +(((margin / cost) * 100).toFixed(1)) : null,
        usesTracked: r.ingredients.filter((i) => this.trackedMatIds.has(i.id)).map((i) => i.id),
        ingredients,
      });
    }
    crafts.sort((a, b) => b.margin * b.velDay - a.margin * a.velDay);

    const now = new Date();
    const snapshot: SweepSnapshot = {
      date: now.toISOString().slice(0, 10),
      timestamp: now.toISOString(),
      world: config.world,
      rows,
      sbUnknown,
      crafts,
    };
    const file = `sweep-${now.toISOString().replace(/[:.]/g, '-')}-${config.world}.json`;
    writeFileSync(join(this.snapshotsDir, file), JSON.stringify(snapshot));
    return snapshot;
  }
}
