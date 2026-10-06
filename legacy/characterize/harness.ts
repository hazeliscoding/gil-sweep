// Characterization harness: runs the v1 (Electron/Angular) TypeScript unchanged against recorded
// market responses and writes what it produced. The .NET tests assert v2 reproduces these.
//
//   cd legacy/characterize
//   TZ=UTC node --experimental-transform-types --no-warnings --import ./register.mjs harness.ts //     ../../tests/GilSweep.Tests/Fixtures ../../tests/GilSweep.Tests/Fixtures/V1
//
// The recorded responses come from capture.py (live Universalis and Saddlebag, 2026-10-05) and
// trim.py (only the fields Gil Sweep reads).
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const LEGACY = new URL('../electron/desktop', import.meta.url).href;
const LEGACY_DIR = fileURLToPath(LEGACY);
const [fixtures, out] = process.argv.slice(2);
mkdirSync(out, { recursive: true });

const readJson = (p: string) => JSON.parse(readFileSync(p, 'utf8'));
const aggregated = readJson(join(fixtures, 'Universalis', 'aggregated-cactuar.json')).results as any[];
const current = readJson(join(fixtures, 'Universalis', 'current-cactuar.json')).items as Record<string, any>;
const history = readJson(join(fixtures, 'Universalis', 'history-cactuar.json')).items as Record<string, any>;
const saddlebag = readJson(join(fixtures, 'Saddlebag', 'marketshare-cactuar.json'));
const worlds = readJson(join(fixtures, 'Universalis', 'worlds.json'));
const aggById = new Map(aggregated.map((r) => [r.itemId, r]));

const requests: string[] = [];
const respond = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
globalThis.fetch = (async (input: any) => {
  const url = String(input);
  requests.push(url);
  let m: RegExpMatchArray | null;
  if (url.startsWith('https://api.saddlebagexchange.com/')) return respond(saddlebag);
  if (url === 'https://universalis.app/api/v2/worlds') return respond(worlds);
  if ((m = url.match(/\/api\/v2\/aggregated\/[^/]+\/([\d,]+)/))) {
    const ids = m[1].split(',').map(Number);
    return respond({ results: ids.filter((id) => aggById.has(id)).map((id) => aggById.get(id)) });
  }
  if ((m = url.match(/\/api\/v2\/history\/[^/]+\/([\d,]+)/))) {
    const ids = m[1].split(',');
    if (ids.length === 1) return respond(history[ids[0]] ?? {});
    return respond({ items: Object.fromEntries(ids.filter((id) => history[id]).map((id) => [id, history[id]])) });
  }
  if ((m = url.match(/\/api\/v2\/[^/]+\/([\d,]+)\?/))) {
    const ids = m[1].split(',');
    if (ids.length === 1) return respond(current[ids[0]] ?? {});
    return respond({ items: Object.fromEntries(ids.filter((id) => current[id]).map((id) => [id, current[id]])) });
  }
  throw new Error('unexpected request ' + url);
}) as typeof fetch;

const { SweepService } = await import(`${LEGACY}/src/main/core/sweep.service.ts`);
const { loadDemandIndex } = await import(`${LEGACY}/src/main/core/demand.ts`);
const { marketDetail } = await import(`${LEGACY}/src/main/core/market-detail.ts`);
const { retainerPlan } = await import(`${LEGACY}/src/main/core/retainer.service.ts`);
const mainEorzea = await import(`${LEGACY}/src/main/core/eorzea.ts`);
const ranking = await import(`${LEGACY}/renderer/src/app/ranking.ts`);
const eorzea = await import(`${LEGACY}/renderer/src/app/eorzea.ts`);
const { DEFAULT_CONFIG } = await import(`${LEGACY}/src/main/core/config.service.ts`);

const write = (name: string, value: unknown) => writeFileSync(join(out, name), JSON.stringify(value, null, 1) + '\n');
const dataDir = join(LEGACY_DIR, 'data');

// A data folder without the bundled seed: v2 never treats the seed as a stored snapshot.
const noSeedData = mkdtempSync(join(tmpdir(), 'gs-data-'));
for (const f of ['items.json', 'crafts.json', 'garland-demand.json']) copyFileSync(join(dataDir, f), join(noSeedData, f));

// ---- A. A full sweep: previous snapshot = the bundled seed (v1's first-boot fallback). ----
const sweepDir = mkdtempSync(join(tmpdir(), 'gs-sweep-'));
const sweep = new SweepService(dataDir, join(sweepDir, 'snapshots'), loadDemandIndex(dataDir));
const cfg = { ...DEFAULT_CONFIG, world: 'Cactuar' };
const snapshot = await sweep.run(cfg);
const aggregatedRequests = requests.filter((r) => r.includes('/aggregated/'));
write('expected-sweep.json', {
  note: 'v1 SweepService.run on Cactuar, previous snapshot = seed-snapshot.json',
  aggregatedRequestIds: aggregatedRequests.map((r) => r.split('/').pop()!.split(',').map(Number)),
  world: snapshot.world,
  rows: snapshot.rows,
  sbUnknown: snapshot.sbUnknown,
  crafts: snapshot.crafts,
});

// ---- B. Ranking under several characters. ----
const configs: Record<string, any> = {
  v1Default: { ...DEFAULT_CONFIG },
  endgame: { ...DEFAULT_CONFIG, levels: { MIN: 100, BTN: 100 }, msqExpansion: 'DT', folklore: ['HW', 'StB', 'ShB', 'EW', 'DT'] },
  splitLevels: { ...DEFAULT_CONFIG, levels: { MIN: 100, BTN: 62 }, msqExpansion: 'DT', folklore: ['DT'], crafters: { CRP: 100, BSM: 92, ARM: 90, GSM: 100, LTW: 85, WVR: 100, ALC: 90, CUL: 100 } },
  fresh: { ...DEFAULT_CONFIG, levels: { MIN: 20, BTN: 15 }, msqExpansion: 'ARR', folklore: [], crafters: { CRP: 20, BSM: 1, ARM: 1, GSM: 30, LTW: 1, WVR: 1, ALC: 1, CUL: 10 } },
  heavensward: { ...DEFAULT_CONFIG, levels: { MIN: 60, BTN: 58 }, msqExpansion: 'HW', crafters: { CRP: 60, BSM: 60, ARM: 60, GSM: 60, LTW: 60, WVR: 60, ALC: 60, CUL: 60 } },
  noCrafters: { ...DEFAULT_CONFIG, levels: { MIN: 100, BTN: 100 }, msqExpansion: 'DT', crafters: {} },
};

function rank(rows: any[], crafts: any[], c: any) {
  const mining = ranking.miningFarms(rows, c);
  const botany = ranking.botanyFarms(rows, c);
  const topFarm = [...mining, ...botany].sort((a: any, b: any) => b.throughput - a.throughput)[0] ?? null;
  const mine = new Set(ranking.farmable(rows, c).filter((r: any) => r.kind !== 'crystal' && r.kind !== 'map').map((r: any) => r.id));
  const craftingPage = (onlyMine: boolean) =>
    crafts
      .filter((x: any) => x.costComplete && x.velScope === 'world' && x.margin > 0)
      .filter((x: any) => ranking.craftableBy(x, c))
      .filter((x: any) => !onlyMine || x.usesTracked.some((id: number) => mine.has(id)))
      .slice(0, 40)
      .map((x: any) => x.id);
  return {
    lockReasons: Object.fromEntries(rows.map((r: any) => [r.id, ranking.lockReason(r, c)])),
    farmable: ranking.farmable(rows, c).map((r: any) => r.id),
    mining: mining.map((r: any) => r.id),
    botany: botany.map((r: any) => r.id),
    topFarm: topFarm?.id ?? null,
    maps: ranking.maps(rows, c).map((r: any) => r.id),
    bestMap: ranking.bestMap(rows, c)?.id ?? null,
    crystals: ranking.crystals(rows, c).map((r: any) => r.id),
    movers: ranking.movers(rows).map((r: any) => r.id),
    locked: ranking.locked(rows, c).map((r: any) => r.id),
    needsFolklore: rows.filter((r: any) => ranking.needsFolklore(r, c)).map((r: any) => r.id),
    retainerTargets: ranking.retainerTargets(rows, c).map((r: any) => r.id),
    craftableBy: crafts.filter((x: any) => ranking.craftableBy(x, c)).map((x: any) => x.id),
    topValueCrafts: ranking.topValueCrafts(crafts, rows, c).map((x: any) => x.id),
    craftingPageOnlyMine: craftingPage(true),
    craftingPageAll: craftingPage(false),
  };
}
const seed = readJson(join(dataDir, 'seed-snapshot.json'));
write('expected-ranking.json', {
  configs,
  sweep: Object.fromEntries(Object.entries(configs).map(([k, c]) => [k, rank(snapshot.rows, snapshot.crafts, c)])),
  seed: Object.fromEntries(Object.entries(configs).map(([k, c]) => [k, rank(seed.rows, [], c)])),
});

// ---- C. Eorzea time and node windows. ----
const items = JSON.parse(readFileSync(join(dataDir, 'items.json'), 'utf8'));
const timed = items.filter((i: any) => i.spawns?.length);
const base = Date.UTC(2026, 9, 5, 12, 0, 0);
const times: number[] = [];
for (let k = 0; k < 160; k++) times.push(base + k * 97_013);
// Exact window edges: real ms at which Eorzea time is h:00, and one real ms either side.
const etHourMs = 175_000;
const dayStart = Math.floor(base / (24 * etHourMs)) * 24 * etHourMs;
for (const h of [0, 2, 4, 6, 10, 12, 14, 16, 22]) for (const d of [-1, 0, 1]) times.push(dayStart + 24 * etHourMs + h * etHourMs + d);
write('expected-eorzea.json', {
  times: times.map((t) => ({
    t,
    clock: eorzea.eorzeaClock(t),
    minuteOfDay: eorzea.eorzeaMinuteOfDay(t),
    nodes: Object.fromEntries(timed.map((i: any) => [i.id, eorzea.nodeWindow(i.spawns, i.uptime, t)])),
    items: Object.fromEntries(timed.map((i: any) => [i.id, mainEorzea.itemWindow(i, t)])),
    nextEndgame: mainEorzea.nextWindows(items, configs.endgame, t),
    nextV1Default: mainEorzea.nextWindows(items, configs.v1Default, t),
  })),
  etMinToRealMin: Object.fromEntries([0, 1, 2, 17, 60, 119, 120, 121, 180, 240, 1439].map((m) => [m, eorzea.etMinToRealMin(m)])),
});

// ---- D. Market drill-down and retainer advice from live listings. ----
const detail: Record<string, unknown> = {};
for (const id of Object.keys(current)) {
  const d = await marketDetail('Cactuar', Number(id));
  // The detail panel's hour-of-day histogram and peak hours (run with TZ=UTC).
  const buckets = new Array(24).fill(0);
  for (const s of d.sales) buckets[new Date(s.t).getHours()] += s.qty;
  const max = Math.max(...buckets, 1);
  const sorted = [...buckets].sort((a, b) => b - a);
  const hotCut = sorted[2] || Infinity;
  const hours = buckets.map((q, h) => ({ q, pct: Math.max(6, (q / max) * 100), hot: q >= hotCut && q > 0, h }));
  const hot = hours.filter((b) => b.hot).map((b) => `${String(b.h).padStart(2, '0')}:00`);
  detail[id] = { ...d, hours, peakHours: hot.length ? hot.join(', ') : 'not enough sales data' };
}
const advice = await retainerPlan('Cactuar', items.map((i: any) => ({ id: i.id, kind: i.kind })));
write('expected-market.json', { detail, advice });

// ---- E. Digest, history and prune over a stored archive (no seed). ----
const archive = mkdtempSync(join(tmpdir(), 'gs-archive-'));
const snapsDir = join(archive, 'snapshots');
mkdirSync(snapsDir);
const latestAt = Date.UTC(2026, 9, 5, 18, 30, 0);
const wobble = (id: number, day: number) => 1 + 0.18 * Math.sin(id * 0.37 + day * 1.3);
const historyInputs: string[] = [];
const snapshotTimes: [number, string][] = [];
for (let day = 12; day >= 0; day--) snapshotTimes.push([latestAt - day * 86400000, 'Cactuar']);
snapshotTimes.push([latestAt - 2 * 86400000 + 3 * 3600000, 'Cactuar']); // a second sweep on one day
snapshotTimes.push([latestAt - 5 * 86400000 - 2 * 3600000, 'Cactuar']); // and on another
snapshotTimes.push([latestAt - 1 * 86400000, 'Gilgamesh']); // another world is ignored
for (const [t, world] of snapshotTimes) {
  const day = Math.round((latestAt - t) / 86400000);
  const iso = new Date(t).toISOString();
  const rows = snapshot.rows.map((r: any) => ({
    id: r.id,
    name: r.name,
    kind: r.kind,
    avg: Math.round(r.avg * wobble(r.id, day)),
    velDay: +(r.velDay * wobble(r.id + 7, day)).toFixed(1),
    velScope: r.velScope,
  }));
  const file = `sweep-${iso.replace(/[:.]/g, '-')}-${world}.json`;
  const body = JSON.stringify({ date: iso.slice(0, 10), timestamp: iso, world, rows, sbUnknown: [] });
  writeFileSync(join(snapsDir, file), body);
  mkdirSync(join(out, 'archive'), { recursive: true });
  writeFileSync(join(out, 'archive', file), body);
  historyInputs.push(file);
}
const archiveService = new SweepService(noSeedData, snapsDir, loadDemandIndex(noSeedData));
const digest = archiveService.digest('Cactuar');
const historyOnly = archiveService.history('Cactuar');
await archiveService.backfill('Cactuar');
const backfillFile = JSON.parse(readFileSync(join(archive, 'history-backfill-Cactuar.json'), 'utf8'));
const historyWithBackfill = archiveService.history('Cactuar');
const filesBefore = readdirSync(snapsDir).sort();
const prune = archiveService.pruneSnapshots();
const filesAfter = readdirSync(snapsDir).sort();
write('expected-history.json', {
  digest,
  history: historyOnly,
  backfill: backfillFile.series,
  historyWithBackfill,
  prune: { ...prune, kept: filesAfter, deletedFiles: filesBefore.filter((f) => !filesAfter.includes(f)) },
});
console.log('wrote', readdirSync(out).join(', '));
