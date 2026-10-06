/**
 * Desktop notifications for watched (starred) items: a toast when a watched
 * node's window opens, and a price-alert toast after sweeps with big swings.
 * Fires only for items the configured character can actually gather. Clicking
 * a toast brings the window back from the tray.
 */
import { Notification } from 'electron';
import { canGather, itemWindow } from './core/eorzea';
import { Services } from './ipc';
import { GilConfig, SweepSnapshot } from '../shared/types';

/** Previous up-state per item id, so toasts fire on the closed→open transition only. */
const wasUp = new Map<number, boolean>();

export function startNodeAlerts(services: Services, showWindow: () => void, intervalMs = 30_000): void {
  const tick = (): void => {
    const cfg = services.config.get();
    const watched = new Set(cfg.watched ?? []);
    const now = Date.now();
    for (const item of services.sweep.items) {
      if (!watched.has(item.id) || !item.spawns?.length) continue;
      const w = canGather(item, cfg) ? itemWindow(item, now) : null;
      const up = w?.up ?? false;
      if (up && wasUp.get(item.id) === false && Notification.isSupported()) {
        const n = new Notification({
          title: `Node up: ${item.name}`,
          body: `${item.where} — ends in ~${w!.minutes}m`,
        });
        n.on('click', showWindow);
        n.show();
      }
      wasUp.set(item.id, up);
    }
  };
  tick();
  setInterval(tick, intervalMs);
}

/** One batched toast for watched items that swung ≥25% in the sweep that just ran. */
export function notifySpikes(snapshot: SweepSnapshot, cfg: GilConfig): void {
  const watched = new Set(cfg.watched ?? []);
  const spikes = snapshot.rows.filter(
    (r) => watched.has(r.id) && r.avgChangePct !== null && Math.abs(r.avgChangePct) >= 25,
  );
  if (!spikes.length || !Notification.isSupported()) return;
  new Notification({
    title: `Price alert — ${snapshot.world}`,
    body: spikes
      .slice(0, 4)
      .map((r) => `${r.name} ${r.avgChangePct! > 0 ? '+' : ''}${r.avgChangePct}% (${r.avg} gil)`)
      .join('\n'),
  }).show();
}
