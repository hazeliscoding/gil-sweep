/**
 * Electron main-process entry point. Wires the config + sweep services to the
 * IPC handlers the renderer calls through window.api, creates the single
 * window, and runs the spawn-clock tray. With closeToTray enabled (default),
 * closing the window hides it and the tray keeps the node clocks glanceable;
 * quit via the tray menu. In dev it loads the Angular dev server
 * (ELECTRON_RENDERER_URL) and opens DevTools; packaged it loads the built
 * renderer/index.html from disk.
 */
import { app, BrowserWindow, Menu, Tray, nativeImage } from 'electron';
import * as path from 'path';
import { ConfigService } from './core/config.service';
import { loadDemandIndex } from './core/demand';
import { eorzeaClock, nextWindows } from './core/eorzea';
import { SweepService } from './core/sweep.service';
import { registerIpc, Services } from './ipc';

// Consistent userData folder in dev ("gil-sweep-desktop" would be used otherwise).
app.setName('gil-sweep');

let win: BrowserWindow | null = null;
let tray: Tray | null = null;
let services: Services | null = null;
let quitting = false;

async function createWindow(): Promise<void> {
  win = new BrowserWindow({
    width: 1440,
    height: 900,
    backgroundColor: '#ffffff',
    autoHideMenuBar: true,
    // Window + taskbar icon; shipped in the bundle (see electron-builder "files").
    icon: path.join(app.getAppPath(), 'build', 'icon.png'),
    webPreferences: {
      preload: path.join(__dirname, '../preload/preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  // Close-to-tray: hide instead of destroy, so the spawn clocks stay one click
  // away. Real quits (tray menu, app.quit) set `quitting` via before-quit.
  win.on('close', (e) => {
    if (!quitting && services?.config.get().closeToTray) {
      e.preventDefault();
      win?.hide();
    }
  });

  const devUrl = process.env.ELECTRON_RENDERER_URL;
  if (devUrl) {
    await win.loadURL(devUrl);
    win.webContents.openDevTools({ mode: 'detach' });
  } else {
    await win.loadFile(path.join(__dirname, '..', '..', 'renderer', 'index.html'));
  }

  win.on('closed', () => {
    win = null;
  });
}

function showWindow(): void {
  if (win) {
    win.show();
    win.focus();
  } else {
    void createWindow();
  }
}

/** Tray: ET clock tooltip + the next node windows, refreshed twice a minute. */
function setupTray(): void {
  const icon = nativeImage
    .createFromPath(path.join(app.getAppPath(), 'build', 'icon.png'))
    .resize({ width: 16, height: 16 });
  tray = new Tray(icon);
  tray.on('click', showWindow);

  const refresh = (): void => {
    if (!tray || !services) return;
    const now = Date.now();
    const windows = nextWindows(services.sweep.items, services.config.get(), now);
    tray.setToolTip(`GilSweep — ET ${eorzeaClock(now)}`);
    tray.setContextMenu(
      Menu.buildFromTemplate([
        { label: `ET ${eorzeaClock(now)}`, enabled: false },
        { type: 'separator' },
        ...windows.map((w) => ({
          label: w.up ? `● ${w.name} — up, ends ~${w.minutes}m` : `${w.name} — in ~${w.minutes}m`,
          click: showWindow,
        })),
        { type: 'separator' },
        { label: 'Open GilSweep', click: showWindow },
        { label: 'Quit', click: () => app.quit() },
      ]),
    );
  };
  refresh();
  setInterval(refresh, 30_000);
  (globalThis as Record<string, unknown>)['__gilsweepTray'] = true;
}

app.on('before-quit', () => {
  quitting = true;
});

app.whenReady().then(async () => {
  if (process.platform === 'win32') app.setAppUserModelId('com.gilsweep.app');

  const userDataDir = app.getPath('userData');
  const dataDir = path.join(app.getAppPath(), 'data');
  services = {
    config: new ConfigService(path.join(userDataDir, 'config.json')),
    sweep: new SweepService(dataDir, path.join(userDataDir, 'snapshots'), loadDemandIndex(dataDir)),
    userDataDir,
  };
  registerIpc(services);
  setupTray();
  await createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  // With close-to-tray the window only hides, so this firing means the user
  // really closed everything (toggle off) — quit like a normal app.
  if (!services?.config.get().closeToTray && process.platform !== 'darwin') app.quit();
});
