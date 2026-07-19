import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { SweepStore } from './sweep.store';
import { SparklineComponent } from './sparkline.component';

/**
 * Market drill-down panel: live listing depth, recent sales, and the
 * hour-of-day posting window, for whatever row was last clicked. Fixed to the
 * right edge; everything visible at once, McMaster style.
 */
@Component({
  selector: 'app-detail-panel',
  standalone: true,
  imports: [DecimalPipe, SparklineComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (store.detail(); as d) {
      <aside class="detail-panel">
        <div class="detail-header">
          <h3>{{ d.name }}</h3>
          <button (click)="store.closeDetail()" aria-label="Close">×</button>
        </div>

        @if (d.row; as r) {
          <div class="meta">
            {{ r.where }} · {{ r.kind }}
            @if (spark(); as v) {
              <app-sparkline [values]="v" />
            }
          </div>
        }

        @if (d.loading) {
          <div class="status">Fetching live listings…</div>
        } @else if (d.error) {
          <div class="status error">{{ d.error }}</div>
        } @else {
          @if (d.data; as m) {
          <div class="kpi-row">
            <div class="kpi">
              <div class="kpi-label">Cheapest now</div>
              <div class="kpi-value">{{ m.curMin | number: '1.0-0' }}</div>
              <div class="kpi-sub">sales median {{ m.medPPU | number: '1.0-0' }}</div>
            </div>
            <div class="kpi">
              <div class="kpi-label">Days of stock</div>
              <div class="kpi-value" [class.neg]="(m.daysInv ?? 0) > 7" [class.pos]="(m.daysInv ?? 99) < 1">
                {{ m.daysInv ?? '—' }}
              </div>
              <div class="kpi-sub">{{ m.listedQty | number: '1.0-0' }} listed · {{ m.unitsPerDay | number: '1.0-0' }}/day live</div>
            </div>
          </div>

          <h3>Cheapest listings</h3>
          <table>
            <thead><tr><th class="num">Price</th><th class="num">Qty</th><th class="num">Total</th></tr></thead>
            <tbody>
              @for (l of m.listings; track $index) {
                <tr>
                  <td class="num">{{ l.ppu | number: '1.0-0' }}</td>
                  <td class="num">{{ l.qty | number: '1.0-0' }}</td>
                  <td class="num secondary">{{ l.ppu * l.qty | number: '1.0-0' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="3" class="muted center">No listings — empty market.</td></tr>
              }
            </tbody>
          </table>

          <h3>Best posting hours <span class="muted">(your local time)</span></h3>
          <div class="hour-bars">
            @for (h of hourHist(); track $index) {
              <div [style.height.%]="h.pct" [class.hot]="h.hot" [title]="h.label"></div>
            }
          </div>
          <div class="hour-labels"><span>00</span><span>06</span><span>12</span><span>18</span><span>23</span></div>
          <div class="meta">Peak sales: {{ peakHours() }}</div>

          <h3>Recent sales</h3>
          <table>
            <thead><tr><th class="num">Price</th><th class="num">Qty</th><th>When</th></tr></thead>
            <tbody>
              @for (s of m.sales.slice(0, 8); track $index) {
                <tr>
                  <td class="num">{{ s.ppu | number: '1.0-0' }}</td>
                  <td class="num">{{ s.qty | number: '1.0-0' }}</td>
                  <td class="secondary">{{ ago(s.t) }}</td>
                </tr>
              } @empty {
                <tr><td colspan="3" class="muted center">No recorded sales.</td></tr>
              }
            </tbody>
          </table>

          @if (d.row?.why) {
            <h3>Why it sells</h3>
            <div class="meta">{{ d.row?.why }}</div>
          }
          }
        }
      </aside>
    }
  `,
})
export class DetailPanelComponent {
  readonly store = inject(SweepStore);

  readonly spark = computed<number[] | null>(() => {
    const id = this.store.detail()?.id;
    if (!id) return null;
    const s = this.store.history()[id];
    return s && s.length >= 2 ? s.map((p) => p.avg) : null;
  });

  /** Sales bucketed by local hour-of-day, normalized for the bar strip. */
  readonly hourHist = computed(() => {
    const sales = this.store.detail()?.data?.sales ?? [];
    const buckets = new Array(24).fill(0);
    for (const s of sales) buckets[new Date(s.t).getHours()] += s.qty;
    const max = Math.max(...buckets, 1);
    const sorted = [...buckets].sort((a, b) => b - a);
    const hotCut = sorted[2] || Infinity; // top 3 hours
    return buckets.map((q, h) => ({
      pct: Math.max(6, (q / max) * 100),
      hot: q >= hotCut && q > 0,
      label: `${String(h).padStart(2, '0')}:00 — ${q} sold`,
    }));
  });

  readonly peakHours = computed(() => {
    const hot = this.hourHist()
      .map((b, h) => ({ ...b, h }))
      .filter((b) => b.hot)
      .map((b) => `${String(b.h).padStart(2, '0')}:00`);
    return hot.length ? hot.join(', ') : 'not enough sales data';
  });

  ago(t: number): string {
    const min = Math.max(0, Math.round((Date.now() - t) / 60000));
    if (min < 60) return `${min}m ago`;
    const h = Math.floor(min / 60);
    return h < 48 ? `${h}h ago` : `${Math.floor(h / 24)}d ago`;
  }
}
