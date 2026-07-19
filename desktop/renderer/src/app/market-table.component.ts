import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { SweepRow } from './models';
import { EorzeaClockService } from './eorzea-clock.service';
import { NodeWindow, nodeWindow } from './eorzea';
import { SweepStore } from './sweep.store';
import { needsFolklore } from './ranking';
import { SparklineComponent } from './sparkline.component';

/**
 * The dense market table used by the farm/mover/watchlist sections: item,
 * location, price (+Δ%), liquidity, trend state, node window, and demand
 * attribution. Rows arrive already filtered/ranked; the "Node (ET)" cells tick
 * live off the shared Eorzea clock signal.
 */
@Component({
  selector: 'app-market-table',
  standalone: true,
  imports: [DecimalPipe, SparklineComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <table>
      <thead>
        <tr>
          <th>Item</th>
          <th>Where</th>
          @if (showReason()) {
            <th>Locked by</th>
          }
          <th class="num">Avg gil</th>
          <th class="num">Δ%</th>
          <th class="num">Sold/day</th>
          <th>Trend</th>
          <th>Node (ET)</th>
          <th>Why it sells</th>
        </tr>
      </thead>
      <tbody>
        @for (r of rows(); track r.id) {
          <tr class="clickable" (click)="store.openDetail(r)" [class.selected]="store.detail()?.id === r.id">
            <td>
              {{ r.name }}
              @if (folk(r)) {
                <span class="folk-tag" title="Needs this expansion's folklore book">folklore</span>
              }
              @if (sparkOf(r.id); as v) {
                <app-sparkline [values]="v" />
              }
            </td>
            <td class="secondary">{{ r.where }}</td>
            @if (showReason()) {
              <td class="secondary">{{ reasonOf()(r) }}</td>
            }
            <td class="num">{{ r.avg | number: '1.0-0' }}</td>
            <td class="num" [class.pos]="(r.avgChangePct ?? 0) > 0" [class.neg]="(r.avgChangePct ?? 0) < 0">
              @if (r.avgChangePct !== null) {
                {{ r.avgChangePct > 0 ? '+' : '' }}{{ r.avgChangePct | number: '1.0-1' }}%
              } @else {
                <span class="muted">—</span>
              }
            </td>
            <td class="num">{{ r.velDay | number: '1.0-1' }}</td>
            <td><span [class]="stateClass(r.sbState)">{{ r.sbState ?? '' }}</span></td>
            <td class="node-window">
              @if (windowOf(r); as w) {
                @if (w.up) {
                  <span class="state-up">● up · ends ~{{ w.endsInRealMin }}m</span>
                } @else {
                  <span class="secondary">{{ w.nextSpawnHour }}:00 · in ~{{ w.nextInRealMin }}m</span>
                }
              } @else {
                <span class="muted">—</span>
              }
            </td>
            <td class="why">{{ r.why || '—' }}</td>
          </tr>
        } @empty {
          <tr>
            <td [attr.colspan]="showReason() ? 9 : 8" class="muted center">Nothing here at these settings.</td>
          </tr>
        }
      </tbody>
    </table>
  `,
})
export class MarketTableComponent {
  private readonly clock = inject(EorzeaClockService);
  readonly store = inject(SweepStore);

  readonly rows = input.required<SweepRow[]>();
  readonly showReason = input(false);
  readonly reasonOf = input<(r: SweepRow) => string>(() => '');

  folk(r: SweepRow): boolean {
    const cfg = this.store.config();
    return cfg ? needsFolklore(r, cfg) : false;
  }

  /** Price series for the sparkline; hidden until there are ≥2 snapshots to draw. */
  sparkOf(id: number): number[] | null {
    const s = this.store.history()[id];
    return s && s.length >= 2 ? s.map((p) => p.avg) : null;
  }

  /** Live node-window state; reading clock.nowMs() keeps the cell ticking. */
  windowOf(r: SweepRow): NodeWindow | null {
    if (!r.spawns?.length) return null;
    return nodeWindow(r.spawns, r.uptime, this.clock.nowMs());
  }

  stateClass(state: string | null): string {
    switch (state) {
      case 'spiking':
      case 'increasing':
        return 'state-up';
      case 'decreasing':
      case 'crashing':
        return 'state-down';
      case 'out of stock':
        return 'state-hot';
      default:
        return 'state-flat';
    }
  }
}
