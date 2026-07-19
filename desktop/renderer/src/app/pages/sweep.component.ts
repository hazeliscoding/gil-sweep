import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CraftValue, EXPANSIONS, SweepRow } from '../models';
import { SweepStore } from '../sweep.store';
import { MarketTableComponent } from '../market-table.component';
import {
  bestMap,
  botanyFarms,
  crystals,
  locked,
  lockReason,
  maps,
  miningFarms,
  movers,
  topValueCrafts,
} from '../ranking';

/**
 * The sweep dashboard: sliders gate what you can farm, everything below
 * re-ranks instantly from the in-memory snapshot (no fetches on drag). "Run
 * sweep" in the header refreshes prices; this page just derives views.
 */
@Component({
  selector: 'app-sweep',
  standalone: true,
  imports: [DecimalPipe, MarketTableComponent, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="breadcrumb">Sweep</div>

    @if (store.error(); as err) {
      <div class="status error section">{{ err }}</div>
    }

    @if (!store.snapshot()) {
      <div class="empty-state">
        <h2>No market data yet</h2>
        <p>Run a sweep to price the item database on your world.</p>
        <button class="btn-primary" [disabled]="store.running()" (click)="store.runSweep()">
          {{ store.running() ? 'Sweeping…' : 'Run sweep' }}
        </button>
      </div>
    } @else {
      @if (store.config(); as cfg) {
      @if (store.worldMismatch()) {
        <div class="status warn section">
          Showing prices for <strong>{{ store.snapshot()!.world }}</strong> but the configured
          world is <strong>{{ cfg.world }}</strong> — run a sweep to refresh.
        </div>
      }

      <div class="toolbar">
        <div class="field">
          <label for="min">Miner</label>
          <input id="min" type="range" min="1" max="100" [value]="cfg.levels.MIN"
                 (input)="setLevel('MIN', $any($event.target).value)" />
          <b>{{ cfg.levels.MIN }}</b>
        </div>
        <div class="field">
          <label for="btn">Botanist</label>
          <input id="btn" type="range" min="1" max="100" [value]="cfg.levels.BTN"
                 (input)="setLevel('BTN', $any($event.target).value)" />
          <b>{{ cfg.levels.BTN }}</b>
        </div>
        <div class="field">
          <label for="msq">MSQ</label>
          <input id="msq" type="range" min="0" max="5" [value]="expIndex()"
                 (input)="setExpansion($any($event.target).value)" />
          <b>{{ cfg.msqExpansion }}</b>
        </div>
        <span class="muted">{{ rows().length }} items tracked · drag to re-rank instantly</span>
      </div>

      <div class="kpi-row">
        <div class="kpi">
          <div class="kpi-label">Top farm</div>
          <div class="kpi-value">{{ topFarm()?.name ?? '—' }}</div>
          <div class="kpi-sub">
            @if (topFarm(); as t) {
              {{ t.avg | number: '1.0-0' }} gil × {{ t.velDay | number: '1.0-0' }}/day market
            }
          </div>
        </div>
        <div class="kpi">
          <div class="kpi-label">Daily map pick</div>
          <div class="kpi-value">{{ mapPick()?.name?.replace('Timeworn ', '') ?? '—' }}</div>
          <div class="kpi-sub">
            @if (mapPick(); as m) {
              {{ m.avg | number: '1.0-0' }} gil · one per 18h per character
            }
          </div>
        </div>
        <div class="kpi">
          <div class="kpi-label">Movers ≥25%</div>
          <div class="kpi-value">{{ moverRows().length }}</div>
          <div class="kpi-sub">price swings since last sweep</div>
        </div>
        <div class="kpi">
          <div class="kpi-label">Farmable now</div>
          <div class="kpi-value">{{ mining().length + botany().length }}</div>
          <div class="kpi-sub">MIN {{ mining().length }} · BTN {{ botany().length }}</div>
        </div>
        <div class="kpi">
          <div class="kpi-label">Top craft value</div>
          <div class="kpi-value">{{ topCrafts()[0]?.name ?? '—' }}</div>
          <div class="kpi-sub">
            @if (topCrafts()[0]; as t) {
              +{{ t.margin | number: '1.0-0' }}/craft · {{ t.job }}
            } @else {
              run a sweep for margins
            }
          </div>
        </div>
      </div>

      <div class="grid-2 section">
        <div>
          <h2>Mining — farm now</h2>
          <app-market-table [rows]="mining()" />
        </div>
        <div>
          <h2>Botany — farm now</h2>
          <app-market-table [rows]="botany()" />
        </div>
      </div>

      @if (topCrafts().length) {
        <div class="section">
          <h2>Process before selling <a routerLink="/crafting" class="h-link">all crafts →</a></h2>
          <table>
            <thead>
              <tr>
                <th>Craft</th>
                <th>Job</th>
                <th class="num">Margin/craft</th>
                <th class="num">Margin %</th>
                <th class="num">Sold/day</th>
                <th>Uses from your farms</th>
              </tr>
            </thead>
            <tbody>
              @for (c of topCrafts(); track c.id) {
                <tr>
                  <td>{{ c.name }}</td>
                  <td class="secondary">{{ c.job }} {{ c.lvl ?? '' }}</td>
                  <td class="num pos"><strong>{{ c.margin | number: '1.0-0' }}</strong></td>
                  <td class="num">{{ c.marginPct | number: '1.0-0' }}%</td>
                  <td class="num">{{ c.velDay | number: '1.0-1' }}</td>
                  <td class="secondary">{{ craftUses(c) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      @if (moverRows().length) {
        <div class="section">
          <h2>Movers since last sweep (≥25% swing)</h2>
          <app-market-table [rows]="moverRows()" />
        </div>
      }

      <div class="grid-2 section">
        <div>
          <h2>Daily map pick</h2>
          <table>
            <thead>
              <tr><th>Map</th><th class="num">Avg gil</th><th class="num">Sold/day</th></tr>
            </thead>
            <tbody>
              @for (m of mapRows(); track m.id) {
                <tr [class.pick]="m.id === mapPick()?.id">
                  <td>{{ m.name }}{{ m.id === mapPick()?.id ? ' ◀ pick' : '' }}</td>
                  <td class="num">{{ m.avg | number: '1.0-0' }}</td>
                  <td class="num">{{ m.velDay | number: '1.0-1' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="3" class="muted center">No maps at these settings.</td></tr>
              }
            </tbody>
          </table>
          <div class="meta">One per 18h per character (shared across gathering jobs) — do the pick, list the rest of your day around it.</div>
        </div>
        <div>
          <h2>Crystals (list everything you accumulate)</h2>
          <table>
            <thead>
              <tr><th>Crystal</th><th class="num">Avg gil</th><th class="num">Sold/day</th></tr>
            </thead>
            <tbody>
              @for (c of crystalRows(); track c.id) {
                <tr>
                  <td>{{ c.name }}</td>
                  <td class="num">{{ c.avg | number: '1.0-0' }}</td>
                  <td class="num">{{ c.velDay | number: '1.0-0' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="3" class="muted center">No crystals at these settings.</td></tr>
              }
            </tbody>
          </table>
        </div>
      </div>

      <div class="section">
        <h2>Locked / watchlist</h2>
        <app-market-table [rows]="lockedRows()" [showReason]="true" [reasonOf]="reasonFn()" />
      </div>

      @if (store.snapshot()!.sbUnknown.length) {
        <div class="section">
          <h2>Top sellers not tracked yet</h2>
          <table>
            <thead>
              <tr><th>Item</th><th class="num">Avg gil</th><th class="num">Sold/week</th><th>Trend</th></tr>
            </thead>
            <tbody>
              @for (s of store.snapshot()!.sbUnknown; track s.id) {
                <tr>
                  <td>{{ s.name }}</td>
                  <td class="num">{{ s.avg | number: '1.0-0' }}</td>
                  <td class="num">{{ s.soldWeek | number: '1.0-0' }}</td>
                  <td class="secondary">{{ s.state }}</td>
                </tr>
              }
            </tbody>
          </table>
          <div class="meta">
            Candidates for the item database — verify they're actually gatherable before chasing
            (top sellers are often crafted, vendor, or FC-voyage items).
          </div>
        </div>
      }
      }
    }
  `,
})
export class SweepComponent {
  readonly store = inject(SweepStore);

  readonly rows = computed<SweepRow[]>(() => this.store.rows());

  readonly expIndex = computed(() => {
    const e = this.store.config()?.msqExpansion ?? 'DT';
    return EXPANSIONS.indexOf(e);
  });

  readonly mining = computed(() => {
    const cfg = this.store.config();
    return cfg ? miningFarms(this.rows(), cfg) : [];
  });
  readonly botany = computed(() => {
    const cfg = this.store.config();
    return cfg ? botanyFarms(this.rows(), cfg) : [];
  });
  readonly topFarm = computed<SweepRow | null>(() => {
    const all = [...this.mining(), ...this.botany()].sort((a, b) => b.throughput - a.throughput);
    return all[0] ?? null;
  });
  readonly mapRows = computed(() => {
    const cfg = this.store.config();
    return cfg ? maps(this.rows(), cfg) : [];
  });
  readonly mapPick = computed(() => {
    const cfg = this.store.config();
    return cfg ? bestMap(this.rows(), cfg) : null;
  });
  readonly crystalRows = computed(() => {
    const cfg = this.store.config();
    return cfg ? crystals(this.rows(), cfg) : [];
  });
  readonly moverRows = computed(() => movers(this.rows()));
  readonly lockedRows = computed(() => {
    const cfg = this.store.config();
    return cfg ? locked(this.rows(), cfg) : [];
  });
  readonly reasonFn = computed(() => {
    const cfg = this.store.config();
    return (r: SweepRow) => (cfg ? lockReason(r, cfg) : '');
  });

  readonly topCrafts = computed<CraftValue[]>(() => {
    const cfg = this.store.config();
    const crafts = this.store.snapshot()?.crafts ?? [];
    return cfg ? topValueCrafts(crafts, this.rows(), cfg, 5) : [];
  });

  craftUses(c: CraftValue): string {
    return c.ingredients
      .filter((i) => c.usesTracked.includes(i.id))
      .map((i) => i.name)
      .join(', ');
  }

  setLevel(job: 'MIN' | 'BTN', value: string): void {
    this.store.patchConfig({ levels: { [job]: +value } });
  }

  setExpansion(index: string): void {
    this.store.patchConfig({ msqExpansion: EXPANSIONS[+index] });
  }
}
