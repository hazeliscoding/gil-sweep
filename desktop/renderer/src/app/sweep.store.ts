import { Injectable, computed, signal } from '@angular/core';
import {
  GilConfig,
  GilConfigPatch,
  HistoryPoint,
  MarketDetail,
  SweepRow,
  SweepSnapshot,
  TrackedItem,
} from './models';

/** State of the market drill-down panel (opened by clicking a row). */
export interface DetailState {
  id: number;
  name: string;
  row: SweepRow | null;
  loading: boolean;
  error: string | null;
  data: MarketDetail | null;
}

/** A snapshot this old (or from another world, or the bundled seed) triggers a background refresh on boot. */
const STALE_MS = 24 * 3600 * 1000;

/**
 * App-wide state: the current config (world/levels/expansion), the latest sweep
 * snapshot, and the run status. The snappiness contract: the snapshot loads
 * once (from disk, instantly) and every slider change re-ranks it client-side —
 * network only happens on boot (world list) and on "Run sweep".
 */
@Injectable({ providedIn: 'root' })
export class SweepStore {
  private get api() {
    return window.api;
  }

  readonly config = signal<GilConfig | null>(null);
  readonly snapshot = signal<SweepSnapshot | null>(null);
  readonly items = signal<TrackedItem[]>([]);
  readonly worlds = signal<string[]>([]);
  readonly history = signal<Record<number, HistoryPoint[]>>({});
  readonly running = signal(false);
  readonly error = signal<string | null>(null);
  readonly detail = signal<DetailState | null>(null);

  /**
   * Snapshot rows joined with the bundled item DB — snapshots persisted before
   * a data-schema addition (e.g. spawn hours) pick the new fields up from the
   * shipped items.json instead of waiting for the next sweep.
   */
  readonly rows = computed<SweepRow[]>(() => {
    const raw = this.snapshot()?.rows ?? [];
    const byId = new Map(this.items().map((i) => [i.id, i]));
    return raw.map((r) => {
      const item = byId.get(r.id);
      return item ? { ...r, spawns: item.spawns, uptime: item.uptime } : r;
    });
  });

  /** Snapshot age in whole minutes (null for the bundled seed / unknown). */
  readonly snapshotAgeMin = computed<number | null>(() => {
    const ts = this.snapshot()?.timestamp;
    if (!ts) return null;
    return Math.max(0, Math.round((Date.now() - new Date(ts).getTime()) / 60000));
  });

  /** True when the loaded snapshot doesn't match the configured world. */
  readonly worldMismatch = computed(() => {
    const snap = this.snapshot();
    const cfg = this.config();
    return !!snap && !!cfg && snap.world !== cfg.world;
  });

  async init(): Promise<void> {
    try {
      const [config, snapshot, items] = await Promise.all([
        this.api.getConfig(),
        this.api.latestSweep(),
        this.api.listItems(),
      ]);
      this.config.set(config);
      this.snapshot.set(snapshot);
      this.items.set(items);
      this.loadHistory();

      // Stale-on-boot: the old data is already on screen — refresh quietly
      // behind it so the numbers are simply fresh (no spinner, no button).
      const stale =
        !snapshot ||
        snapshot.seed ||
        !snapshot.timestamp ||
        Date.now() - Date.parse(snapshot.timestamp) > STALE_MS ||
        snapshot.world !== config.world;
      if (stale) this.runSweep({ auto: true });
    } catch (e) {
      this.error.set(`Failed to load: ${(e as Error).message}`);
    }
    // World list is a nice-to-have; offline boot still works with the configured world.
    try {
      this.worlds.set(await this.api.listWorlds());
    } catch {
      const w = this.config()?.world;
      this.worlds.set(w ? [w] : []);
    }
  }

  private async loadHistory(): Promise<void> {
    try {
      const hist = await this.api.sweepHistory();
      this.history.set(hist);
      // Thin history (fresh install / new world): backfill once from
      // Universalis sale history so sparklines have shape from day one.
      const maxLen = Object.values(hist).reduce((m, s) => Math.max(m, s.length), 0);
      if (maxLen < 5 && !this.backfilled) {
        this.backfilled = true;
        await this.api.backfillHistory();
        this.history.set(await this.api.sweepHistory());
      }
    } catch {
      /* sparklines are decorative — never block on them */
    }
  }
  private backfilled = false;

  async openDetail(row: SweepRow): Promise<void> {
    this.detail.set({ id: row.id, name: row.name, row, loading: true, error: null, data: null });
    try {
      const data = await this.api.marketDetail(row.id);
      // Only apply if the user hasn't clicked elsewhere meanwhile.
      if (this.detail()?.id === row.id) {
        this.detail.set({ id: row.id, name: row.name, row, loading: false, error: null, data });
      }
    } catch (e) {
      if (this.detail()?.id === row.id) {
        this.detail.set({
          id: row.id,
          name: row.name,
          row,
          loading: false,
          error: `Failed to load listings: ${(e as Error).message}`,
          data: null,
        });
      }
    }
  }

  closeDetail(): void {
    this.detail.set(null);
  }

  /** Instant local update (re-ranks immediately); persistence is fire-and-forget. */
  patchConfig(patch: GilConfigPatch): void {
    const current = this.config();
    if (!current) return;
    const merged: GilConfig = {
      ...current,
      ...patch,
      levels: { ...current.levels, ...(patch.levels ?? {}) },
      crafters: { ...current.crafters, ...(patch.crafters ?? {}) },
      saddlebag: { ...current.saddlebag, ...(patch.saddlebag ?? {}) },
    };
    this.config.set(merged);
    this.api.setConfig(patch).catch(() => void 0);
  }

  async setWorld(world: string): Promise<void> {
    this.patchConfig({ world });
    await this.runSweep(); // prices are per-world; refresh right away
  }

  async runSweep(opts: { auto?: boolean } = {}): Promise<void> {
    if (this.running()) return;
    this.running.set(true);
    this.error.set(null);
    try {
      this.snapshot.set(await this.api.runSweep());
      this.loadHistory();
    } catch (e) {
      // An auto-refresh failing (offline boot) is not worth a banner — the
      // stale data on screen is still the best data we have.
      if (!opts.auto) this.error.set(`Sweep failed: ${(e as Error).message}`);
    } finally {
      this.running.set(false);
    }
  }
}
