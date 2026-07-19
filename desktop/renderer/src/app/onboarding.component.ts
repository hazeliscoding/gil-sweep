import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { EXPANSIONS, Expansion } from './models';
import { SweepStore } from './sweep.store';

/**
 * First-run onboarding: one modal, three questions — world, gatherer levels,
 * MSQ progress. Saving writes the config (which is what ends "first run");
 * everything else (crafters, folklore, tray) lives in Settings.
 */
@Component({
  selector: 'app-onboarding',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (store.firstRun()) {
      <div class="modal-overlay onboarding">
        <div class="modal">
          <h2>Welcome to GilSweep</h2>
          <p class="meta">
            Tell it who your character is — everything re-ranks around these three answers.
            You can change them any time in Settings or with the dashboard sliders.
          </p>
          <div class="form-field">
            <label for="ob-world">World</label>
            <select id="ob-world" [value]="world()" (change)="world.set($any($event.target).value)">
              @for (w of store.worlds(); track w) {
                <option [value]="w" [selected]="w === world()">{{ w }}</option>
              }
            </select>
          </div>
          <div class="actions">
            <div class="form-field">
              <label for="ob-min">Miner</label>
              <input id="ob-min" type="number" min="1" max="100" [value]="min()"
                     (change)="min.set(+$any($event.target).value)" />
            </div>
            <div class="form-field">
              <label for="ob-btn">Botanist</label>
              <input id="ob-btn" type="number" min="1" max="100" [value]="btn()"
                     (change)="btn.set(+$any($event.target).value)" />
            </div>
            <div class="form-field">
              <label for="ob-msq">MSQ</label>
              <select id="ob-msq" [value]="msq()" (change)="msq.set($any($event.target).value)">
                @for (e of expansions; track e) {
                  <option [value]="e" [selected]="e === msq()">{{ e }}</option>
                }
              </select>
            </div>
          </div>
          <div class="actions">
            <button class="btn-primary" id="ob-save" (click)="save()">Start sweeping</button>
          </div>
        </div>
      </div>
    }
  `,
})
export class OnboardingComponent {
  readonly store = inject(SweepStore);
  readonly expansions = EXPANSIONS;
  readonly world = signal('Cactuar');
  readonly min = signal(90);
  readonly btn = signal(90);
  readonly msq = signal<Expansion>('DT');

  save(): void {
    const currentWorld = this.store.config()?.world;
    this.store.patchConfig({
      levels: { MIN: this.min(), BTN: this.btn() },
      msqExpansion: this.msq(),
    });
    this.store.firstRun.set(false);
    if (this.world() !== currentWorld) {
      this.store.setWorld(this.world()); // per-world prices → fresh sweep
    } else {
      this.store.patchConfig({ world: this.world() }); // persist = first run over
    }
  }
}
