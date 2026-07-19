import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { EXPANSIONS, Expansion, SnapshotStats, VerifyResult } from '../models';
import { SweepStore } from '../sweep.store';

/**
 * Settings: the same character state the sliders control, in form layout, plus
 * data-folder access. Config persists to the OS user-data folder via IPC.
 */
@Component({
  selector: 'app-settings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="breadcrumb">Settings</div>
    <h1>Settings</h1>

    @if (store.config(); as cfg) {
      <div class="section">
        <h2>Character</h2>
        <div class="actions">
          <div class="form-field">
            <label for="s-world">World</label>
            <select id="s-world" (change)="store.setWorld($any($event.target).value)">
              @for (w of store.worlds(); track w) {
                <option [value]="w" [selected]="w === cfg.world">{{ w }}</option>
              }
            </select>
          </div>
          <div class="form-field">
            <label for="s-min">Miner level</label>
            <input id="s-min" type="number" min="1" max="100" [value]="cfg.levels.MIN"
                   (change)="store.patchConfig({ levels: { MIN: +$any($event.target).value } })" />
          </div>
          <div class="form-field">
            <label for="s-btn">Botanist level</label>
            <input id="s-btn" type="number" min="1" max="100" [value]="cfg.levels.BTN"
                   (change)="store.patchConfig({ levels: { BTN: +$any($event.target).value } })" />
          </div>
          <div class="form-field">
            <label for="s-msq">MSQ progress</label>
            <select id="s-msq" [value]="cfg.msqExpansion"
                    (change)="store.patchConfig({ msqExpansion: $any($event.target).value })">
              @for (e of expansions; track e) {
                <option [value]="e">{{ e }}</option>
              }
            </select>
          </div>
        </div>
        <div class="form-field" style="max-width: none">
          <label>Crafter levels (gate the Crafting page)</label>
          <div class="actions">
            @for (job of crafterJobs; track job) {
              <label>
                {{ job }}
                <input type="number" min="1" max="100" class="cr-lvl" [id]="'cr-' + job"
                       [value]="cfg.crafters[job] ?? 100"
                       (change)="setCrafter(job, $any($event.target).value)" />
              </label>
            }
          </div>
        </div>
        <div class="form-field" style="max-width: none">
          <label>Folklore books owned (legendary nodes without one get a tag)</label>
          <div class="actions">
            @for (e of folkloreExpansions; track e) {
              <label>
                <input type="checkbox" [checked]="cfg.folklore.includes(e)"
                       (change)="toggleFolklore(e, $any($event.target).checked)" />
                {{ e }}
              </label>
            }
          </div>
        </div>
        <div class="meta">
          Changing the world runs a fresh sweep (prices are per-world). Levels, MSQ, and folklore
          only re-rank what's already loaded — instant, no network. On launch, a sweep runs
          automatically in the background whenever the loaded snapshot is older than a day.
        </div>
      </div>

      <div class="section">
        <h2>Behavior</h2>
        <div class="actions">
          <label>
            <input type="checkbox" [checked]="cfg.closeToTray"
                   (change)="store.patchConfig({ closeToTray: $any($event.target).checked })" />
            Keep running in the tray when the window is closed
          </label>
        </div>
        <div class="meta">
          The tray icon shows the Eorzea clock and the next timed-node windows for your character —
          right-click it to quit for real. Untick to make closing the window quit like a normal app.
        </div>
      </div>

      <div class="section">
        <h2>Tracked items</h2>
        <div class="actions">
          <input id="add-item" type="text" placeholder="item name, e.g. Dark Matter Cluster" style="width: 260px"
                 [value]="addQuery()" (input)="addQuery.set($any($event.target).value)" (keydown.enter)="track()" />
          <button class="btn-primary" id="btn-track" [disabled]="tracking() || !addQuery().trim()" (click)="track()">
            {{ tracking() ? 'Verifying…' : 'Verify & track' }}
          </button>
        </div>
        @if (trackResult(); as r) {
          <div class="status track-result" [class.error]="!r.found" [class.ok]="r.gatherable">
            {{ r.name ? r.name + ' — ' : '' }}{{ r.reason }}
          </div>
        }
        @if (customItems().length) {
          <table id="custom-items" style="max-width: 760px; margin-top: var(--space-4)">
            <thead>
              <tr><th>Item</th><th>Where</th><th>Kind</th><th></th></tr>
            </thead>
            <tbody>
              @for (i of customItems(); track i.id) {
                <tr>
                  <td>{{ i.name }}</td>
                  <td class="secondary">{{ i.where }}</td>
                  <td class="secondary">{{ i.kind }}</td>
                  <td class="center"><button (click)="store.removeCustomItem(i.id)">Remove</button></td>
                </tr>
              }
            </tbody>
          </table>
        }
        <div class="meta">
          Verification checks Garland Tools for the item's own gathering nodes (level, zone, spawn
          hours come along for free). Top sellers that turn out to be crafted, vendor, or FC-voyage
          items get tracked as non-farms so they never resurface as candidates. Gatherable additions
          trigger a sweep to price them.
        </div>
      </div>

      <div class="section">
        <h2>Data</h2>
        <div class="actions">
          <button (click)="openFolder()">Open data folder</button>
          <button (click)="prune()" [disabled]="pruning()">Prune to one snapshot per day</button>
          <span class="muted" id="snap-stats">
            {{ stats()?.count ?? '…' }} snapshots · {{ mb() }} MB
            @if (pruneResult() !== null) {
              · deleted {{ pruneResult() }}
            }
          </span>
        </div>
        <div class="meta">
          Sweep snapshots and this config live in your user-data folder. Snapshots accumulate so
          week-over-week price changes work; pruning keeps the newest per day (trends keep their
          shape, disk stays tidy). Nothing leaves your machine except the market API calls
          (Universalis, Saddlebag Exchange).
        </div>
      </div>
    }
  `,
})
export class SettingsComponent {
  readonly store = inject(SweepStore);
  readonly expansions = EXPANSIONS;
  /** ARR has no folklore books — they start with Heavensward. */
  readonly folkloreExpansions = EXPANSIONS.filter((e) => e !== 'ARR');

  readonly crafterJobs = ['CRP', 'BSM', 'ARM', 'GSM', 'LTW', 'WVR', 'ALC', 'CUL'];

  readonly addQuery = signal('');
  readonly tracking = signal(false);
  readonly trackResult = signal<VerifyResult | null>(null);
  readonly customItems = computed(() => this.store.items().filter((i) => i.custom));

  async track(): Promise<void> {
    const q = this.addQuery().trim();
    if (!q || this.tracking()) return;
    this.tracking.set(true);
    this.trackResult.set(null);
    try {
      this.trackResult.set(await this.store.trackItem(q));
      this.addQuery.set('');
    } catch (e) {
      this.trackResult.set({ found: false, gatherable: false, reason: `verification failed: ${(e as Error).message}` });
    } finally {
      this.tracking.set(false);
    }
  }

  setCrafter(job: string, value: string): void {
    this.store.patchConfig({ crafters: { [job]: Math.min(100, Math.max(1, +value || 100)) } });
  }

  readonly stats = signal<SnapshotStats | null>(null);
  readonly pruning = signal(false);
  readonly pruneResult = signal<number | null>(null);
  readonly mb = computed(() => (((this.stats()?.bytes ?? 0) / 1048576).toFixed(1)));

  constructor() {
    this.refreshStats();
  }

  private refreshStats(): void {
    window.api.snapshotStats().then((s) => this.stats.set(s)).catch(() => void 0);
  }

  async prune(): Promise<void> {
    this.pruning.set(true);
    try {
      const r = await window.api.pruneSnapshots();
      this.pruneResult.set(r.deleted);
      this.refreshStats();
    } finally {
      this.pruning.set(false);
    }
  }

  toggleFolklore(expansion: Expansion, owned: boolean): void {
    const current = this.store.config()?.folklore ?? [];
    const next = owned ? [...new Set([...current, expansion])] : current.filter((e) => e !== expansion);
    this.store.patchConfig({ folklore: next });
  }

  openFolder(): void {
    window.api.openDataFolder();
  }
}
