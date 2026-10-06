/**
 * Main-process Eorzea time math for the tray menu (the renderer has its own
 * copy in renderer/src/app/eorzea.ts — same constants, keep in sync).
 * 1 Eorzean day = 70 real minutes; 1 ET hour = 175 real seconds.
 */
import { EXPANSIONS, GilConfig, TrackedItem } from '../../shared/types';

const ET_MIN_PER_DAY = 24 * 60;

function eorzeaMinuteOfDay(realMs: number): number {
  return ((realMs * 3600) / 175 / 60000) % ET_MIN_PER_DAY;
}

export function eorzeaClock(realMs: number): string {
  const m = Math.floor(eorzeaMinuteOfDay(realMs));
  return `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`;
}

export interface TrayWindow {
  name: string;
  up: boolean;
  /** Real minutes until the window closes (up) or opens (down). */
  minutes: number;
}

/** Level + MSQ gate for the tray/alerts (timed kinds are all gatherable kinds). */
export function canGather(it: TrackedItem, cfg: GilConfig): boolean {
  if (EXPANSIONS.indexOf(it.expansion) > EXPANSIONS.indexOf(cfg.msqExpansion)) return false;
  const lvl = it.job === 'both' ? Math.max(cfg.levels.MIN, cfg.levels.BTN) : cfg.levels[it.job];
  return it.level <= lvl;
}

/** Current window state for one timed item; null when the item isn't timed. */
export function itemWindow(
  it: TrackedItem,
  realMs: number,
): { up: boolean; minutes: number } | null {
  if (!it.spawns?.length) return null;
  const now = eorzeaMinuteOfDay(realMs);
  const uptime = it.uptime ?? 120;
  let best: { up: boolean; minutes: number } | null = null;
  for (const s of it.spawns) {
    const sinceOpen = (now - s * 60 + ET_MIN_PER_DAY) % ET_MIN_PER_DAY;
    if (sinceOpen < uptime) {
      return { up: true, minutes: Math.ceil(((uptime - sinceOpen) * 175) / 3600) };
    }
    const minutes = Math.ceil(((ET_MIN_PER_DAY - sinceOpen) * 175) / 3600);
    if (!best || minutes < best.minutes) best = { up: false, minutes };
  }
  return best;
}

/**
 * The next node windows for the tray: timed items the configured character can
 * gather, up-now first (soonest to close), then soonest-to-open.
 */
export function nextWindows(
  items: TrackedItem[],
  cfg: GilConfig,
  realMs: number,
  limit = 6,
): TrayWindow[] {
  const res: TrayWindow[] = [];
  for (const it of items) {
    if (!canGather(it, cfg)) continue;
    const w = itemWindow(it, realMs);
    if (w) res.push({ name: it.name, ...w });
  }
  res.sort((a, b) => (a.up === b.up ? a.minutes - b.minutes : a.up ? -1 : 1));
  return res.slice(0, limit);
}
