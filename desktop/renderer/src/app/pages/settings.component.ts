import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { EXPANSIONS, Expansion } from '../models';
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
        <h2>Data</h2>
        <div class="actions">
          <button (click)="openFolder()">Open data folder</button>
        </div>
        <div class="meta">
          Sweep snapshots and this config live in your user-data folder. Snapshots accumulate so
          week-over-week price changes work; nothing leaves your machine except the market API calls
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

  toggleFolklore(expansion: Expansion, owned: boolean): void {
    const current = this.store.config()?.folklore ?? [];
    const next = owned ? [...new Set([...current, expansion])] : current.filter((e) => e !== expansion);
    this.store.patchConfig({ folklore: next });
  }

  openFolder(): void {
    window.api.openDataFolder();
  }
}
