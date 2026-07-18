import { Injectable, computed, signal } from '@angular/core';
import { GilConfig, GilConfigPatch, SweepSnapshot } from './models';

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
  readonly worlds = signal<string[]>([]);
  readonly running = signal(false);
  readonly error = signal<string | null>(null);

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
      const [config, snapshot] = await Promise.all([this.api.getConfig(), this.api.latestSweep()]);
      this.config.set(config);
      this.snapshot.set(snapshot);
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

  /** Instant local update (re-ranks immediately); persistence is fire-and-forget. */
  patchConfig(patch: GilConfigPatch): void {
    const current = this.config();
    if (!current) return;
    const merged: GilConfig = {
      ...current,
      ...patch,
      levels: { ...current.levels, ...(patch.levels ?? {}) },
      saddlebag: { ...current.saddlebag, ...(patch.saddlebag ?? {}) },
    };
    this.config.set(merged);
    this.api.setConfig(patch).catch(() => void 0);
  }

  async setWorld(world: string): Promise<void> {
    this.patchConfig({ world });
    await this.runSweep(); // prices are per-world; refresh right away
  }

  async runSweep(): Promise<void> {
    if (this.running()) return;
    this.running.set(true);
    this.error.set(null);
    try {
      this.snapshot.set(await this.api.runSweep());
    } catch (e) {
      this.error.set(`Sweep failed: ${(e as Error).message}`);
    } finally {
      this.running.set(false);
    }
  }
}
