import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RetainerAdvice, SweepRow } from '../models';
import { SweepStore } from '../sweep.store';
import { retainerTargets } from '../ranking';

interface PlanRow extends SweepRow {
  advice: RetainerAdvice;
}

/**
 * Retainer selling plan: for the items you'd realistically be selling (top
 * farmables at the current sliders), pull LIVE listings + recent sales and
 * recommend list price, stack size, and priority. Unlike the sweep snapshot,
 * this is fetched fresh — undercut prices go stale in minutes, not days.
 */
@Component({
  selector: 'app-retainers',
  standalone: true,
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="breadcrumb">Retainers</div>
    <h1>Retainer selling plan</h1>

    @if (!store.snapshot()) {
      <div class="empty-state">
        <h2>No market data yet</h2>
        <p>Run a sweep first — the plan covers your current top farmables.</p>
      </div>
    } @else {
      <div class="toolbar">
        <button class="btn-primary" [disabled]="loading()" (click)="refresh()">
          {{ loading() ? 'Fetching live listings…' : 'Refresh listings' }}
        </button>
        <span class="muted">
          {{ targets().length }} items · live listings + recent sales on {{ store.snapshot()!.world }}
          @if (fetchedAt(); as at) {
            · fetched {{ at }}
          }
        </span>
      </div>

      @if (error(); as err) {
        <div class="status error section">{{ err }}</div>
      }

      <table>
        <thead>
          <tr>
            <th>Item</th>
            <th>Kind</th>
            <th class="num">Cur min</th>
            <th class="num">Sales median</th>
            <th class="num">List at</th>
            <th class="num">Stack</th>
            <th class="num">Days of stock</th>
            <th class="num">Sold/day (live)</th>
            <th>Verdict</th>
          </tr>
        </thead>
        <tbody>
          @for (p of plan(); track p.id) {
            <tr>
              <td>{{ p.name }}</td>
              <td class="secondary">{{ p.kind }}</td>
              <td class="num">{{ p.advice.curMin | number: '1.0-0' }}</td>
              <td class="num">{{ p.advice.medPPU | number: '1.0-0' }}</td>
              <td class="num"><strong>{{ p.advice.listPrice | number: '1.0-0' }}</strong></td>
              <td class="num">{{ p.advice.stack | number: '1.0-0' }}</td>
              <td class="num">{{ p.advice.daysInv ?? '—' }}</td>
              <td class="num">{{ p.advice.unitsPerDay | number: '1.0-0' }}</td>
              <td><span [class]="verdictClass(p.advice.verdict)">{{ p.advice.verdict }}</span></td>
            </tr>
          } @empty {
            <tr>
              <td colspan="9" class="muted center">
                {{ loading() ? 'Fetching live listings…' : 'No advice yet — hit Refresh listings.' }}
              </td>
            </tr>
          }
        </tbody>
      </table>
      <div class="meta">
        <strong>List at</strong> undercuts the cheapest listing by 1 gil — unless the floor has
        crashed below what sales actually clear at, in which case hold near the clearing price.
        <strong>Stack</strong> is the median quantity buyers actually purchase.
      </div>
    }
  `,
})
export class RetainersComponent {
  readonly store = inject(SweepStore);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly advice = signal<Map<number, RetainerAdvice>>(new Map());
  readonly fetchedAt = signal<string | null>(null);
  private fetchedOnce = false;

  readonly targets = computed<SweepRow[]>(() => {
    const cfg = this.store.config();
    const rows = this.store.snapshot()?.rows ?? [];
    return cfg ? retainerTargets(rows, cfg) : [];
  });

  readonly plan = computed<PlanRow[]>(() =>
    this.targets()
      .map((t) => ({ ...t, advice: this.advice().get(t.id) }))
      .filter((p): p is PlanRow => !!p.advice),
  );

  constructor() {
    // Fetch as soon as the snapshot/config are available (once).
    effect(
      () => {
        if (this.targets().length && !this.fetchedOnce) {
          this.fetchedOnce = true;
          this.refresh();
        }
      },
      { allowSignalWrites: true },
    );
  }

  async refresh(): Promise<void> {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await window.api.retainerPlan(
        this.targets().map((t) => ({ id: t.id, kind: t.kind })),
      );
      this.advice.set(new Map(result.map((a) => [a.id, a])));
      this.fetchedAt.set(new Date().toLocaleTimeString());
    } catch (e) {
      this.error.set(`Failed to fetch listings: ${(e as Error).message}`);
    } finally {
      this.loading.set(false);
    }
  }

  verdictClass(verdict: string): string {
    if (verdict === 'healthy') return 'state-up';
    if (verdict.startsWith('shortage')) return 'state-hot';
    if (verdict.startsWith('floor crashed')) return 'state-down';
    if (verdict.startsWith('EMPTY')) return 'state-hot';
    return 'state-flat';
  }
}
