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
  crafters: { CRP: 100, BSM: 100, ARM: 100, GSM: 100, LTW: 100, WVR: 100, ALC: 100, CUL: 100 },
  saddlebag: { timePeriod: 168, salesAmount: 2, averagePrice: 50, filters: [47, 48, 49] },
};

export class ConfigService {
  constructor(private readonly file: string) {}

  /** False until the first save — drives the first-run onboarding. */
  exists(): boolean {
    return existsSync(this.file);
  }

  get(): GilConfig {
    if (!existsSync(this.file)) return { ...DEFAULT_CONFIG };
    try {
      const stored = JSON.parse(readFileSync(this.file, 'utf8'));
      return {
        ...DEFAULT_CONFIG,
        ...stored,
        levels: { ...DEFAULT_CONFIG.levels, ...(stored.levels ?? {}) },
        crafters: { ...DEFAULT_CONFIG.crafters, ...(stored.crafters ?? {}) },
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
      crafters: { ...current.crafters, ...(patch.crafters ?? {}) },
      saddlebag: { ...current.saddlebag, ...(patch.saddlebag ?? {}) },
    };
    writeFileSync(this.file, JSON.stringify(merged, null, 2));
    return merged;
  }
}
