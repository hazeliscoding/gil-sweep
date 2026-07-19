import { Injectable, computed, signal } from '@angular/core';
import { eorzeaClock } from './eorzea';

/**
 * The app-wide ticking clock. One real second per tick is plenty: an Eorzea
 * minute is ~2.9 real seconds, and every "up now / next in" cell derives from
 * this signal, so the whole UI stays in sync off a single interval.
 */
@Injectable({ providedIn: 'root' })
export class EorzeaClockService {
  readonly nowMs = signal(Date.now());
  readonly etLabel = computed(() => eorzeaClock(this.nowMs()));

  constructor() {
    setInterval(() => this.nowMs.set(Date.now()), 1000);
  }
}
