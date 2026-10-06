import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * A tiny inline price sparkline (60×16 SVG). Deliberately minimal — no axes,
 * no tooltips, just the shape of the series; the Δ% column carries the number.
 */
@Component({
  selector: 'app-sparkline',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg class="spark" viewBox="0 0 60 16" width="60" height="16" preserveAspectRatio="none" aria-hidden="true">
      <polyline [attr.points]="pts()" fill="none" />
      <circle [attr.cx]="lastX()" [attr.cy]="lastY()" r="1.5" />
    </svg>
  `,
})
export class SparklineComponent {
  readonly values = input.required<number[]>();

  private readonly coords = computed(() => {
    const v = this.values();
    if (v.length < 2) return [];
    const min = Math.min(...v);
    const max = Math.max(...v);
    const span = max - min || 1;
    const stepX = 58 / (v.length - 1);
    return v.map((y, i) => [1 + i * stepX, 14 - ((y - min) / span) * 12]);
  });

  readonly pts = computed(() =>
    this.coords()
      .map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`)
      .join(' '),
  );
  readonly lastX = computed(() => this.coords().at(-1)?.[0]?.toFixed(1) ?? 0);
  readonly lastY = computed(() => this.coords().at(-1)?.[1]?.toFixed(1) ?? 0);
}
