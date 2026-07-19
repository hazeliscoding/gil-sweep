import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { CraftValue } from '../models';
import { SweepStore } from '../sweep.store';
import { craftableBy, farmable } from '../ranking';

/**
 * Craft value-add: is it worth processing what you farm before selling it?
 * Margins come from the sweep snapshot (sale price × yield − ingredient min
 * listings, crystals included), so this page is instant and re-filters live
 * as the sliders change what you can farm.
 */
@Component({
  selector: 'app-crafting',
  standalone: true,
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="breadcrumb">Crafting</div>
    <h1>Craft value-add</h1>

    @if (!store.snapshot()?.crafts) {
      <div class="empty-state">
        <h2>No craft margins yet</h2>
        <p>Margins are computed during a sweep — run one (or wait for the auto-refresh).</p>
      </div>
    } @else {
      <div class="toolbar">
        <label>
          <input type="checkbox" [checked]="onlyMine()" (change)="onlyMine.set($any($event.target).checked)" />
          Only crafts using what I can farm right now
        </label>
        <span class="muted">
          {{ rows().length }} profitable crafts you can make · HQ-aware · mats at min(buy, craft) · crystals included
        </span>
      </div>

      <table>
        <thead>
          <tr>
            <th>Craft</th>
            <th>Job</th>
            <th class="num">Sells</th>
            <th class="num">Sold/day</th>
            <th class="num">Mat cost</th>
            <th class="num">Margin/craft</th>
            <th class="num">Margin %</th>
            <th>Uses from your farms</th>
          </tr>
        </thead>
        <tbody>
          @for (c of rows(); track c.id) {
            <tr>
              <td>{{ c.name }}{{ c.yield > 1 ? ' ×' + c.yield : '' }}</td>
              <td class="secondary">{{ c.job }} {{ c.lvl ?? '' }}</td>
              <td class="num">
                {{ c.salePrice | number: '1.0-0' }}
                @if (c.hq) {
                  <span class="hq-tag" title="HQ market dominates — HQ price and velocity shown">HQ</span>
                }
              </td>
              <td class="num">{{ c.velDay | number: '1.0-1' }}</td>
              <td class="num">{{ c.cost | number: '1.0-0' }}</td>
              <td class="num pos"><strong>{{ c.margin | number: '1.0-0' }}</strong></td>
              <td class="num" [class.pos]="(c.marginPct ?? 0) >= 50">{{ c.marginPct | number: '1.0-0' }}%</td>
              <td class="secondary">{{ usesNames(c) }}</td>
            </tr>
          } @empty {
            <tr><td colspan="8" class="muted center">No profitable crafts at these settings.</td></tr>
          }
        </tbody>
      </table>
      <div class="meta">
        Margin = sale price × yield − ingredient cost, where each ingredient costs
        min(cheapest listing, crafting it from its own mats) — one level deep, crystals included.
        Outputs marked <span class="hq-tag">HQ</span> price at HQ because that's where their sales
        actually happen. Recipes with uncostable (vendor-only) ingredients are excluded, and only
        recipes within your crafter levels (Settings) are shown. Ranked by margin × daily sales.
      </div>
    }
  `,
})
export class CraftingComponent {
  readonly store = inject(SweepStore);
  readonly onlyMine = signal(true);

  /** Non-crystal mats you can farm at the current sliders. */
  private readonly myMatIds = computed<Set<number>>(() => {
    const cfg = this.store.config();
    if (!cfg) return new Set();
    return new Set(
      farmable(this.store.rows(), cfg)
        .filter((r) => r.kind !== 'crystal' && r.kind !== 'map')
        .map((r) => r.id),
    );
  });

  readonly rows = computed<CraftValue[]>(() => {
    const crafts = this.store.snapshot()?.crafts ?? [];
    const cfg = this.store.config();
    const mine = this.myMatIds();
    return crafts
      .filter((c) => c.costComplete && c.velScope === 'world' && c.margin > 0)
      .filter((c) => !cfg || craftableBy(c, cfg))
      .filter((c) => !this.onlyMine() || c.usesTracked.some((id) => mine.has(id)))
      .slice(0, 40);
  });

  usesNames(c: CraftValue): string {
    const mine = this.myMatIds();
    const names = c.ingredients
      .filter((i) => c.usesTracked.includes(i.id))
      .map((i) => (mine.has(i.id) ? i.name : `${i.name} (locked)`));
    return names.join(', ') || '—';
  }
}
