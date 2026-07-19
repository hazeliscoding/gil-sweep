/**
 * The sweep engine: prices every tracked item on the configured world, joins
 * Saddlebag trend state, attributes demand ("why it sells"), computes Δ% vs the
 * previous snapshot, and persists the result as JSON in userData/snapshots.
 *
 * Accessibility (level/MSQ gating) is deliberately NOT computed here — the
 * renderer derives it from the raw rows so slider changes re-rank instantly
 * without re-fetching. Ported from the ffxiv-market-sweep CLI.
 */
import { existsSync, mkdirSync, readFileSync, readdirSync, statSync, unlinkSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import {
  CraftValue,
  DigestResult,
  GilConfig,
  HistoryPoint,
  SaddlebagUnknown,
  SnapshotStats,
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

  /** Cached Universalis backfill lives beside (not inside) the snapshots dir. */
  private backfillFile(world: string): string {
    return join(dirname(this.snapshotsDir), `history-backfill-${world}.json`);
  }

  /** All snapshots (bundled seed first, then stored) for one world, oldest→newest. */
  private loadSnapshots(world: string): SweepSnapshot[] {
    const snaps: SweepSnapshot[] = [];
    const seedFile = join(this.dataDir, 'seed-snapshot.json');
    if (existsSync(seedFile)) snaps.push(JSON.parse(readFileSync(seedFile, 'utf8')));
    if (existsSync(this.snapshotsDir)) {
      for (const f of readdirSync(this.snapshotsDir).filter((f) => f.endsWith('.json')).sort()) {
        snaps.push(JSON.parse(readFileSync(join(this.snapshotsDir, f), 'utf8')));
      }
    }
    const ts = (s: SweepSnapshot): number => (s.timestamp ? Date.parse(s.timestamp) : Date.parse(s.date));
    return snaps.filter((s) => s.world === world).sort((a, b) => ts(a) - ts(b));
  }

  /**
   * Week-over-week digest: latest snapshot vs the newest baseline at least
   * 5 days older (falling back to the oldest available while the archive is
   * young). Purely local — no network.
   */
  digest(world: string): DigestResult {
    const NEVER_FARM = new Set(['vendor', 'submarine', 'venture']);
    const snaps = this.loadSnapshots(world);
    const ts = (s: SweepSnapshot): number => (s.timestamp ? Date.parse(s.timestamp) : Date.parse(s.date));
    const latest = snaps[snaps.length - 1];
    const empty: DigestResult = {
      world,
      latestDate: latest?.date ?? '',
      baselineDate: null,
      daysApart: null,
      changes: [],
      prune: [],
    };
    if (!latest || snaps.length < 2) return empty;

    let baseline: SweepSnapshot | null = null;
    for (const s of snaps.slice(0, -1)) {
      if (ts(latest) - ts(s) >= 5 * 86400000) baseline = s; // newest one old enough
    }
    baseline ??= snaps[0];

    const thenById = new Map(baseline.rows.map((r) => [r.id, r]));
    const changes = latest.rows
      .filter((r) => !NEVER_FARM.has(r.kind) && thenById.has(r.id))
      .map((r) => {
        const then = thenById.get(r.id)!;
        return {
          id: r.id,
          name: r.name,
          avgThen: then.avg,
          avgNow: r.avg,
          avgPct: then.avg ? +((((r.avg - then.avg) / then.avg) * 100).toFixed(1)) : null,
          velThen: then.velDay,
          velNow: r.velDay,
          delta: Math.abs(r.avg * r.velDay - then.avg * then.velDay),
        };
      })
      .sort((a, b) => b.delta - a.delta)
      .slice(0, 10)
      .map(({ delta: _delta, ...c }) => c);

    // Prune: farm-rotation kinds stuck under 5/day (world scope) for the last 3 sweeps.
    const FARM_KINDS = new Set(['node', 'unspoiled', 'legendary', 'ephemeral', 'diadem', 'reduction']);
    const prune: DigestResult['prune'] = [];
    const recent = snaps.slice(-3);
    if (recent.length >= 3) {
      for (const item of latest.rows) {
        if (!FARM_KINDS.has(item.kind)) continue;
        const vels = recent.map((s) => s.rows.find((r) => r.id === item.id)).filter((r) => !!r);
        if (vels.length === 3 && vels.every((r) => r!.velScope === 'world' && r!.velDay < 5)) {
          prune.push({ id: item.id, name: item.name, recentVel: vels.map((r) => r!.velDay) });
        }
      }
    }

    return {
      world,
      latestDate: latest.date,
      baselineDate: baseline.date,
      daysApart: +(((ts(latest) - ts(baseline)) / 86400000).toFixed(1)),
      changes,
      prune,
    };
  }

  snapshotStats(): SnapshotStats {
    if (!existsSync(this.snapshotsDir)) return { count: 0, bytes: 0 };
    const files = readdirSync(this.snapshotsDir).filter((f) => f.endsWith('.json'));
    let bytes = 0;
    for (const f of files) bytes += statSync(join(this.snapshotsDir, f)).size;
    return { count: files.length, bytes };
  }

  /** Keeps the newest snapshot per world+day (filenames sort chronologically), deletes the rest. */
  pruneSnapshots(): { deleted: number } {
    if (!existsSync(this.snapshotsDir)) return { deleted: 0 };
    const files = readdirSync(this.snapshotsDir).filter((f) => f.endsWith('.json')).sort();
    const keep = new Map<string, string>(); // world+day -> newest file
    for (const f of files) {
      const m = f.match(/^sweep-(\d{4}-\d{2}-\d{2})T.*-(.+)\.json$/);
      const key = m ? `${m[2]}|${m[1]}` : f;
      keep.set(key, f); // later files overwrite → newest per day survives
    }
    const keepSet = new Set(keep.values());
    let deleted = 0;
    for (const f of files) {
      if (!keepSet.has(f)) {
        unlinkSync(join(this.snapshotsDir, f));
        deleted++;
      }
    }
    return { deleted };
  }

  /**
   * One-time sale-history backfill from Universalis: quantity-weighted daily
   * average prices for every tracked item, so sparklines have shape before the
   * snapshot archive has had time to grow. Cached per world in userData.
   */
  async backfill(world: string): Promise<number> {
    const ids = this.items.map((i) => i.id);
    const series: Record<number, HistoryPoint[]> = {};
    for (let i = 0; i < ids.length; i += 100) {
      const chunk = ids.slice(i, i + 100);
      const r = await fetch(
        `https://universalis.app/api/v2/history/${world}/${chunk.join(',')}?entriesToReturn=300`,
        { signal: AbortSignal.timeout(60000) },
      );
      if (!r.ok) throw new Error(`Universalis history HTTP ${r.status}`);
      const j: any = await r.json();
      const byId = j.items ?? { [j.itemID]: j };
      for (const id of chunk) {
        const entries = (byId[id]?.entries ?? []) as any[];
        if (!entries.length) continue;
        const days = new Map<number, { gil: number; qty: number }>();
        for (const e of entries) {
          const day = Math.floor((e.timestamp * 1000) / 86400000) * 86400000 + 43200000; // noon UTC
          const d = days.get(day) ?? { gil: 0, qty: 0 };
          d.gil += e.pricePerUnit * e.quantity;
          d.qty += e.quantity;
          days.set(day, d);
        }
        series[id] = [...days.entries()]
          .map(([t, d]) => ({ t, avg: Math.round(d.gil / d.qty), velDay: d.qty }))
          .sort((a, b) => a.t - b.t);
      }
    }
    writeFileSync(
      this.backfillFile(world),
      JSON.stringify({ world, fetchedAt: new Date().toISOString(), series }),
    );
    return Object.keys(series).length;
  }

  /**
   * Per-item price/velocity series: Universalis backfill (if fetched) plus
   * every stored snapshot (plus the bundled seed) for one world — the data
   * behind the sparklines. Reads a handful of local JSON files; stays instant.
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
    const bf = this.backfillFile(world);
    if (existsSync(bf)) {
      try {
        const cached = JSON.parse(readFileSync(bf, 'utf8'));
        for (const [id, points] of Object.entries(cached.series ?? {})) {
          out[Number(id)] = [...(points as HistoryPoint[])];
        }
      } catch {
        /* corrupt backfill cache — snapshots still work */
      }
    }
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

    // Craft margins, two passes:
    //  1) unit cost to craft each known recipe from bought mats (NQ min listings)
    //  2) margins with min(buy, craft) per ingredient — one level deep, so
    //     nugget→ingot chains price the nugget at whichever way is cheaper.
    // Outputs price at HQ when the HQ market dominates (mats have no HQ since 6.0,
    // so ingredient costs stay NQ).
    const buyUnit = (id: number): number =>
      Math.round((pick(prices.get(id)?.nq?.minListing)?.price ?? 0) as number);
    const craftUnit = new Map<number, number>();
    for (const r of Object.values(this.craftRecipes)) {
      let cost = 0;
      let ok = true;
      for (const ing of r.ingredients) {
        const u = buyUnit(ing.id);
        if (!u) {
          ok = false;
          break;
        }
        cost += u * ing.qty;
      }
      if (ok) craftUnit.set(r.id, Math.ceil(cost / r.yield));
    }

    const crafts: CraftValue[] = [];
    for (const r of Object.values(this.craftRecipes)) {
      const p = prices.get(r.id);
      const nqAvg = Math.round((pick(p?.nq?.averageSalePrice)?.price ?? 0) as number);
      const nqVel = velOf(r.id);
      const hqAvg = Math.round((pick(p?.hq?.averageSalePrice)?.price ?? 0) as number);
      const hqVel = +(((pick(p?.hq?.dailySaleVelocity)?.quantity ?? 0) as number).toFixed(1));
      const useHq = hqAvg > 0 && hqVel > nqVel;
      const salePrice = useHq ? hqAvg : nqAvg;
      const craftVelDay = useHq ? hqVel : nqVel;
      if (!salePrice || !craftVelDay) continue; // dead market — not worth listing

      let cost = 0;
      let costComplete = true;
      const ingredients = r.ingredients.map((ing) => {
        const buy = buyUnit(ing.id);
        const crafted = craftUnit.get(ing.id) ?? 0;
        const unitPrice = buy && crafted ? Math.min(buy, crafted) : buy || crafted;
        if (!unitPrice) costComplete = false;
        cost += unitPrice * ing.qty;
        return { ...ing, unitPrice, viaCraft: !!crafted && (!buy || crafted < buy) };
      });
      const margin = salePrice * r.yield - cost;
      crafts.push({
        id: r.id,
        name: r.name,
        job: r.job,
        lvl: r.lvl,
        yield: r.yield,
        salePrice,
        hq: useHq,
        velDay: craftVelDay,
        velScope: scopeOf(useHq ? p?.hq?.dailySaleVelocity : p?.nq?.dailySaleVelocity),
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
