/**
 * Registers every `ipcMain.handle` channel the renderer invokes. Channel names
 * map 1:1 to the methods on window.api (see preload.ts) and the GilApi type in
 * shared/types.ts — adding a feature means touching all three.
 *
 * Handlers are thin: unwrap args, delegate to a service. Business logic stays
 * in core/ where it's testable without Electron.
 */
import { ipcMain, shell } from 'electron';
import { ConfigService } from './core/config.service';
import { retainerPlan } from './core/retainer.service';
import { SweepService } from './core/sweep.service';
import { listWorlds } from './core/universalis';
import { GilConfigPatch, RetainerTarget } from '../shared/types';

export interface Services {
  config: ConfigService;
  sweep: SweepService;
  userDataDir: string;
}

export function registerIpc(services: Services): void {
  ipcMain.handle('health', () => ({ status: 'ok' }));

  ipcMain.handle('config:get', () => services.config.get());
  ipcMain.handle('config:set', (_e, patch: GilConfigPatch) => services.config.set(patch ?? {}));

  ipcMain.handle('items:list', () => services.sweep.items);
  ipcMain.handle('sweep:latest', () => services.sweep.latest());
  ipcMain.handle('sweep:run', () => services.sweep.run(services.config.get()));

  ipcMain.handle('worlds:list', () => listWorlds());

  ipcMain.handle('retainer:plan', (_e, targets: RetainerTarget[]) =>
    retainerPlan(services.config.get().world, targets ?? []),
  );

  ipcMain.handle('data:openFolder', async () => {
    await shell.openPath(services.userDataDir);
    return services.userDataDir;
  });
}
