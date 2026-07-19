import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HistoryPoint } from './models';

/**
 * A full-size line chart for one item's history series — the sparkline's big
 * sibling. Pure SVG, no chart library (zero-dependency rule). X is time,
 * Y auto-scales; min/max/first/last labels carry the numbers, McMaster style:
 * information first, decoration never.
 */
@Component({
  selector: 'app-trend-chart',
  standalone: true,
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="trend-chart">
      <div class="meta"><strong>{{ label() }}</strong>
        @if (series().length >= 2) {
          · {{ minVal() | number: '1.0-1' }}–{{ maxVal() | number: '1.0-1' }}
          · latest {{ lastVal() | number: '1.0-1' }}
        }
      </div>
      @if (series().length >= 2) {
        <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" preserveAspectRatio="none" class="trend-svg">
          <line [attr.x1]="0" [attr.y1]="H - PAD" [attr.x2]="W" [attr.y2]="H - PAD" class="axis" />
          <line [attr.x1]="0" [attr.y1]="PAD" [attr.x2]="W" [attr.y2]="PAD" class="grid" />
          <polyline [attr.points]="pts()" fill="none" class="line" />
          @for (c of dots(); track $index) {
            <circle [attr.cx]="c[0]" [attr.cy]="c[1]" r="2" class="dot" />
          }
        </svg>
        <div class="trend-x">
          <span>{{ firstDate() }}</span>
          <span>{{ lastDate() }}</span>
        </div>
      } @else {
        <div class="status">Not enough history yet — every sweep adds a point.</div>
      }
    </div>
  `,
})
export class TrendChartComponent {
  readonly series = input.required<HistoryPoint[]>();
  readonly field = input.required<'avg' | 'velDay'>();
  readonly label = input.required<string>();

  readonly W = 640;
  readonly H = 160;
  readonly PAD = 8;

  private readonly values = computed(() => this.series().map((p) => p[this.field()]));
  readonly minVal = computed(() => Math.min(...this.values()));
  readonly maxVal = computed(() => Math.max(...this.values()));
  readonly lastVal = computed(() => this.values().at(-1) ?? 0);

  private readonly coords = computed(() => {
    const s = this.series();
    const v = this.values();
    if (s.length < 2) return [];
    const t0 = s[0].t;
    const t1 = s[s.length - 1].t;
    const spanT = t1 - t0 || 1;
    const min = this.minVal();
    const span = this.maxVal() - min || 1;
    const drawH = this.H - 2 * this.PAD;
    return s.map((p, i) => [
      ((p.t - t0) / spanT) * this.W,
      this.H - this.PAD - ((v[i] - min) / span) * drawH,
    ]);
  });

  readonly pts = computed(() =>
    this.coords()
      .map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`)
      .join(' '),
  );
  readonly dots = computed(() => this.coords());
  readonly firstDate = computed(() => new Date(this.series()[0]?.t ?? 0).toLocaleDateString());
  readonly lastDate = computed(() => new Date(this.series().at(-1)?.t ?? 0).toLocaleDateString());
}
