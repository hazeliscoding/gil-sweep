import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { DigestResult, HistoryPoint } from '../models';
import { SweepStore } from '../sweep.store';
import { TrendChartComponent } from '../trend-chart.component';

/**
 * Trends: the week-over-week digest (what changed, what to stop farming) and
 * full-size price/velocity charts for any tracked item. Everything here is
 * local snapshot data — instant, no network.
 */
@Component({
  selector: 'app-trends',
  standalone: true,
  imports: [DecimalPipe, TrendChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="breadcrumb">Trends</div>
    <h1>Trends</h1>

    @if (digest(); as d) {
      @if (d.baselineDate) {
        <div class="section">
          <h2>Since {{ d.baselineDate }} <span class="muted">({{ d.daysApart }}d ago)</span></h2>
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th class="num">Avg then</th>
                <th class="num">Avg now</th>
                <th class="num">Δ%</th>
                <th class="num">Sold/day then</th>
                <th class="num">Sold/day now</th>
              </tr>
            </thead>
            <tbody>
              @for (c of d.changes; track c.id) {
                <tr class="clickable" (click)="selected.set(c.id)">
                  <td>{{ c.name }}</td>
                  <td class="num secondary">{{ c.avgThen | number: '1.0-0' }}</td>
                  <td class="num">{{ c.avgNow | number: '1.0-0' }}</td>
                  <td class="num" [class.pos]="(c.avgPct ?? 0) > 0" [class.neg]="(c.avgPct ?? 0) < 0">
                    {{ c.avgPct === null ? '—' : (c.avgPct > 0 ? '+' : '') + c.avgPct + '%' }}
                  </td>
                  <td class="num secondary">{{ c.velThen | number: '1.0-1' }}</td>
                  <td class="num">{{ c.velNow | number: '1.0-1' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="muted center">No overlapping items between snapshots.</td></tr>
              }
            </tbody>
          </table>
          <div class="meta">Ranked by market-throughput change (price × velocity). Click a row to chart it.</div>
        </div>

        @if (d.prune.length) {
          <div class="section">
            <h2>Consider dropping from the rotation</h2>
            <div class="status warn">
              @for (p of d.prune; track p.id; let last = $last) {
                <span>{{ p.name }} ({{ p.recentVel.join(' → ') }}/day over the last 3 sweeps)</span>@if (!last) {<span> · </span>}
              }
            </div>
          </div>
        }
      } @else {
        <div class="status section">
          Week-over-week needs at least two snapshots a few days apart — keep sweeping, the story
          builds itself.
        </div>
      }
    }

    <div class="section">
      <h2>Item history</h2>
      <div class="toolbar">
        <div class="field">
          <label for="trend-item">Item</label>
          <select id="trend-item" [value]="selected() ?? ''" (change)="selected.set(+$any($event.target).value)">
            @for (r of chartable(); track r.id) {
              <option [value]="r.id" [selected]="r.id === selected()">{{ r.name }}</option>
            }
          </select>
        </div>
        <span class="muted">{{ points().length }} data points · one per sweep (plus backfilled daily history)</span>
      </div>
      <app-trend-chart [series]="points()" field="avg" label="Average sale price (gil)" />
      <app-trend-chart [series]="points()" field="velDay" label="Units sold per day" />
    </div>
  `,
})
export class TrendsComponent {
  readonly store = inject(SweepStore);
  readonly selected = signal<number | null>(null);
  readonly digest = signal<DigestResult | null>(null);

  /** Items worth charting: everything with at least 2 history points, throughput first. */
  readonly chartable = computed(() => {
    const hist = this.store.history();
    return this.store
      .rows()
      .filter((r) => (hist[r.id]?.length ?? 0) >= 2)
      .sort((a, b) => b.throughput - a.throughput);
  });

  readonly points = computed<HistoryPoint[]>(() => {
    const id = this.selected() ?? this.chartable()[0]?.id;
    return id ? (this.store.history()[id] ?? []) : [];
  });

  constructor() {
    window.api.sweepDigest().then((d) => this.digest.set(d)).catch(() => void 0);
  }
}
