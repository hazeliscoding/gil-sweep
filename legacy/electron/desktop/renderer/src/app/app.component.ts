import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SweepStore } from './sweep.store';
import { EorzeaClockService } from './eorzea-clock.service';
import { DetailPanelComponent } from './detail-panel.component';
import { OnboardingComponent } from './onboarding.component';

/**
 * Root shell: persistent header (brand + world picker + Run sweep) and sidebar
 * nav around the routed page. The world picker and sweep trigger live here so
 * they're reachable from any page — everything binds to the shared SweepStore.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, DetailPanelComponent, OnboardingComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="app">
      <header class="app-header">
        <a class="brand" routerLink="/sweep" aria-label="GilSweep — home">
          <span class="brand-badge">G</span>
          <span class="brand-name">Gil<span>Sweep</span></span>
        </a>
        <div class="header-right">
          <span class="et-clock" title="Eorzea time">ET {{ clock.etLabel() }}</span>
          @if (store.snapshot(); as snap) {
            <span class="header-status">
              @if (snap.seed) {
                bundled data · run a sweep for live prices
              } @else if (store.snapshotAgeMin() !== null) {
                {{ snap.world }} · swept {{ ageLabel(store.snapshotAgeMin()!) }}
              }
            </span>
          }
          <label>
            World
            <select
              [disabled]="store.running()"
              (change)="store.setWorld($any($event.target).value)"
            >
              <!-- [selected] per option, not [value] on the select: the world list
                   arrives async, and a value applied before options exist is lost. -->
              @for (w of store.worlds(); track w) {
                <option [value]="w" [selected]="w === store.config()?.world">{{ w }}</option>
              }
            </select>
          </label>
          <button
            class="btn-primary"
            [disabled]="store.running()"
            (click)="store.runSweep()"
          >
            {{ store.running() ? 'Sweeping…' : 'Run sweep' }}
          </button>
        </div>
      </header>

      <div class="app-body">
        <aside class="sidebar">
          <nav>
            <a routerLink="/sweep" routerLinkActive="active">Sweep</a>
            <a routerLink="/crafting" routerLinkActive="active">Crafting</a>
            <a routerLink="/retainers" routerLinkActive="active">Retainers</a>
            <a routerLink="/trends" routerLinkActive="active">Trends</a>
            <a routerLink="/settings" routerLinkActive="active">Settings</a>
          </nav>
        </aside>

        <main class="main">
          <router-outlet />
        </main>
      </div>
      <app-detail-panel />
      <app-onboarding />
    </div>
  `,
})
export class AppComponent {
  readonly store = inject(SweepStore);
  readonly clock = inject(EorzeaClockService);

  constructor() {
    this.store.init();
  }

  ageLabel(min: number): string {
    if (min < 1) return 'just now';
    if (min < 60) return `${min}m ago`;
    const h = Math.floor(min / 60);
    return h < 48 ? `${h}h ago` : `${Math.floor(h / 24)}d ago`;
  }
}
