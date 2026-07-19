/**
 * Persists user config (world, gatherer levels, MSQ progress, Saddlebag query
 * params) as JSON in the Electron userData folder. Plain class — no Electron
 * imports — so it stays testable; main.ts supplies the file path.
 */
import { existsSync, readFileSync, writeFileSync } from 'fs';
import { GilConfig, GilConfigPatch } from '../../shared/types';

export const DEFAULT_CONFIG: GilConfig = {
  world: 'Cactuar',
  levels: { MIN: 89, BTN: 70 },
  msqExpansion: 'EW',
  folklore: [],
  closeToTray: true,
  watched: [],
  saddlebag: { timePeriod: 168, salesAmount: 2, averagePrice: 50, filters: [47, 48, 49] },
};

export class ConfigService {
  constructor(private readonly file: string) {}

  get(): GilConfig {
    if (!existsSync(this.file)) return { ...DEFAULT_CONFIG };
    try {
      const stored = JSON.parse(readFileSync(this.file, 'utf8'));
      return {
        ...DEFAULT_CONFIG,
        ...stored,
        levels: { ...DEFAULT_CONFIG.levels, ...(stored.levels ?? {}) },
        saddlebag: { ...DEFAULT_CONFIG.saddlebag, ...(stored.saddlebag ?? {}) },
      };
    } catch {
      return { ...DEFAULT_CONFIG };
    }
  }

  set(patch: GilConfigPatch): GilConfig {
    const current = this.get();
    const merged: GilConfig = {
      ...current,
      ...patch,
      levels: { ...current.levels, ...(patch.levels ?? {}) },
      saddlebag: { ...current.saddlebag, ...(patch.saddlebag ?? {}) },
    };
    writeFileSync(this.file, JSON.stringify(merged, null, 2));
    return merged;
  }
}
