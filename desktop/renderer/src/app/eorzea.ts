/**
 * Eorzea time math — pure functions, called every clock tick, keep them cheap.
 *
 * 1 Eorzean day = 70 real minutes, so ET runs at 3600/175 ≈ 20.57× real time
 * (1 ET hour = 175 real seconds). A 120 ET-minute node window is ~5.8 real
 * minutes — which is why "up now" needs a ticking clock, not a table refresh.
 */

const ET_MS_PER_REAL_MS = 3600 / 175;
const ET_MIN_PER_DAY = 24 * 60;

/** Real milliseconds → Eorzea minute-of-day [0, 1440). */
export function eorzeaMinuteOfDay(realMs: number): number {
  const etMinutes = (realMs * ET_MS_PER_REAL_MS) / 60000;
  return etMinutes % ET_MIN_PER_DAY;
}

/** "HH:MM" Eorzea clock label. */
export function eorzeaClock(realMs: number): string {
  const m = Math.floor(eorzeaMinuteOfDay(realMs));
  const hh = String(Math.floor(m / 60)).padStart(2, '0');
  const mm = String(m % 60).padStart(2, '0');
  return `${hh}:${mm}`;
}

/** Eorzea minutes → whole real minutes (ceil so "1m" never means "already over"). */
export function etMinToRealMin(etMin: number): number {
  return Math.ceil((etMin * 175) / 3600);
}

export interface NodeWindow {
  /** Node is gatherable right now. */
  up: boolean;
  /** Real minutes until the current window closes (when up). */
  endsInRealMin: number;
  /** Next spawn hour in ET (when down). */
  nextSpawnHour: number;
  /** Real minutes until the next window opens (when down). */
  nextInRealMin: number;
}

const DEFAULT_UPTIME_ET_MIN = 120;

/**
 * Where the node stands right now given its ET spawn hours and window length.
 * Handles windows that wrap midnight and multiple spawns per day.
 */
export function nodeWindow(
  spawns: number[],
  uptimeEtMin: number | undefined,
  realMs: number,
): NodeWindow {
  const uptime = uptimeEtMin ?? DEFAULT_UPTIME_ET_MIN;
  const now = eorzeaMinuteOfDay(realMs);

  let bestNext: { hour: number; inEtMin: number } | null = null;
  for (const s of spawns) {
    const start = s * 60;
    // Minutes since this window opened, normalized to [0, 1440).
    const sinceOpen = (now - start + ET_MIN_PER_DAY) % ET_MIN_PER_DAY;
    if (sinceOpen < uptime) {
      return {
        up: true,
        endsInRealMin: etMinToRealMin(uptime - sinceOpen),
        nextSpawnHour: s,
        nextInRealMin: 0,
      };
    }
    const untilOpen = ET_MIN_PER_DAY - sinceOpen;
    if (!bestNext || untilOpen < bestNext.inEtMin) bestNext = { hour: s, inEtMin: untilOpen };
  }
  return {
    up: false,
    endsInRealMin: 0,
    nextSpawnHour: bestNext?.hour ?? spawns[0] ?? 0,
    nextInRealMin: etMinToRealMin(bestNext?.inEtMin ?? 0),
  };
}
