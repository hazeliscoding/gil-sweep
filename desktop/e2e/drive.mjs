// End-to-end driver for the GilSweep Electron app. Launches the real app
// (dev binary, or the packaged build with `packaged`), walks every page
// against live market APIs, screenshots each stage, prints a JSON verdict,
// and exits non-zero on any failed check. Run headless on Linux via xvfb-run.
// NOTE: expects a CLEAN profile (no gil-sweep userData) — first-run onboarding
// and the auto-sweep checks depend on it.
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const APP_DIR = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const require = createRequire(path.join(APP_DIR, 'package.json'));
const { _electron: electron } = require('playwright-core');
const IS_WIN = process.platform === 'win32';
const SHOT_DIR = process.env.SHOT_DIR || path.join(APP_DIR, 'e2e', 'shots');
fs.mkdirSync(SHOT_DIR, { recursive: true });

const results = {};
let failed = false;
const check = (name, ok, detail) => {
  results[name] = { ok, detail };
  if (!ok) failed = true;
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}: ${detail}`);
};

// "packaged" mode drives the electron-builder output instead of the dev binary —
// same app, but with asar packaging, bundled data paths, and production layout.
// (Packaged mode is Windows-only; CI runs dev mode under xvfb.)
const PACKAGED = process.argv[2] === 'packaged';
const app = await electron.launch({
  executablePath: PACKAGED
    ? path.join(APP_DIR, 'release', 'win-unpacked', 'gil-sweep.exe')
    : path.join(APP_DIR, 'node_modules', 'electron', 'dist', IS_WIN ? 'electron.exe' : 'electron'),
  args: PACKAGED ? [] : ['--no-sandbox', APP_DIR],
  timeout: 30_000,
});
const page = await app.firstWindow();

// Renderer booted + seed snapshot rendered
await page.waitForSelector('.app-header', { timeout: 15_000 });

// First-run onboarding: must appear on a clean profile; answer with the same
// character the rest of the checks expect (Cactuar, MIN 89 / BTN 70, EW).
try {
  await page.waitForSelector('.onboarding', { timeout: 8_000 });
  await page.evaluate(() => {
    const set = (sel, v) => {
      const el = document.querySelector(sel);
      el.value = v;
      el.dispatchEvent(new Event('change', { bubbles: true }));
    };
    set('#ob-min', '89');
    set('#ob-btn', '70');
    set('#ob-msq', 'EW');
    document.querySelector('#ob-save')?.click();
  });
  await page.waitForFunction(() => !document.querySelector('.onboarding'), undefined, { timeout: 5_000 });
  check('onboarding', true, 'shown on first run, saved, dismissed');
} catch {
  check('onboarding', false, 'overlay missing or stuck');
}
await page.waitForSelector('table tbody tr', { timeout: 15_000 });
await new Promise((r) => setTimeout(r, 800)); // let fonts/layout settle

const seedState = await page.evaluate(() => {
  const status = document.querySelector('.header-status')?.textContent?.trim() ?? '';
  const tables = [...document.querySelectorAll('h2')].map((h) => h.textContent?.trim());
  const miningRows = [...document.querySelectorAll('app-market-table')][0]?.querySelectorAll('tbody tr') ?? [];
  const firstMining = miningRows[0]?.querySelector('td')?.textContent?.trim();
  const kpis = [...document.querySelectorAll('.kpi')].map((k) => ({
    label: k.querySelector('.kpi-label')?.textContent?.trim(),
    value: k.querySelector('.kpi-value')?.textContent?.trim(),
  }));
  const whySample = miningRows[0]?.querySelector('td.why')?.textContent?.trim();
  return { status, tables, miningCount: miningRows.length, firstMining, kpis, whySample };
});
check('seed-status', seedState.status.includes('bundled data'), seedState.status);
check('seed-mining-rows', seedState.miningCount > 0, `${seedState.miningCount} rows, top: ${seedState.firstMining}`);
check('seed-top-is-mythrite', !!seedState.firstMining?.includes('Mythrite Sand'), `top mining = ${seedState.firstMining}`);
check('seed-why-column', !!seedState.whySample && seedState.whySample !== '—', `why[0] = ${seedState.whySample}`);
console.log('KPIs:', JSON.stringify(seedState.kpis));
console.log('Sections:', JSON.stringify(seedState.tables));
await page.screenshot({ path: path.join(SHOT_DIR, '01-seed.png') });

// Eorzea clock: present, correctly formatted, and actually ticking (~2.9s/ET-min).
const clock0 = await page.evaluate(() => document.querySelector('.et-clock')?.textContent?.trim());
check('et-clock-present', /^ET \d{2}:\d{2}$/.test(clock0 ?? ''), clock0 ?? '(missing)');
try {
  await page.waitForFunction(
    (c0) => document.querySelector('.et-clock')?.textContent?.trim() !== c0,
    clock0,
    { timeout: 10_000 },
  );
  const clock1 = await page.evaluate(() => document.querySelector('.et-clock')?.textContent?.trim());
  check('et-clock-ticks', true, `${clock0} -> ${clock1}`);
} catch {
  check('et-clock-ticks', false, 'label frozen for 10s');
}

// Spawn windows render for timed nodes with the up-now / next-window format.
const nodeCells = await page.evaluate(() =>
  [...document.querySelectorAll('td.node-window')]
    .map((td) => td.textContent?.trim())
    .filter((t) => t && t !== '—')
    .slice(0, 5),
);
check(
  'spawn-windows',
  nodeCells.length > 0 && nodeCells.every((t) => /up · ends ~\d+m|\d{1,2}:00 · in ~\d+m/.test(t)),
  JSON.stringify(nodeCells),
);

const brand = await page.evaluate(() => document.querySelector('.brand-name')?.textContent?.trim());
check('brand-gilsweep', brand === 'GilSweep', brand ?? '(missing)');

// Folklore tags: default config owns no books, so legendary rows (watchlist
// always has DT legendaries) must carry the tag even on the seed snapshot.
const folkCount = await page.evaluate(() => document.querySelectorAll('.folk-tag').length);
check('folklore-tags', folkCount > 0, `${folkCount} folklore tags on seed`);

// Watch star: toggles on click without opening the detail panel.
await page.evaluate(() => document.querySelector('app-market-table tbody tr .star')?.click());
await new Promise((r) => setTimeout(r, 150));
const starState = await page.evaluate(() => ({
  star: document.querySelector('app-market-table tbody tr .star')?.textContent?.trim(),
  panelOpen: !!document.querySelector('.detail-panel'),
}));
check('watch-star', starState.star === '★' && !starState.panelOpen, JSON.stringify(starState));

// Quick filter: typing narrows the tables instantly.
await page.evaluate(() => {
  const el = document.querySelector('#q');
  el.value = 'darksteel';
  el.dispatchEvent(new Event('input', { bubbles: true }));
});
await new Promise((r) => setTimeout(r, 200));
const filtered = await page.evaluate(() => {
  const rows = [...document.querySelectorAll('app-market-table')][0]?.querySelectorAll('tbody tr') ?? [];
  return { count: rows.length, first: rows[0]?.querySelector('td')?.textContent?.trim() };
});
check('quick-filter', filtered.count === 1 && !!filtered.first?.includes('Darksteel'), `${filtered.count} rows: ${filtered.first}`);
await page.evaluate(() => {
  const el = document.querySelector('#q');
  el.value = '';
  el.dispatchEvent(new Event('input', { bubbles: true }));
});

// Slider re-rank: drop MIN to 40 — Mythrite Sand (needs 55) must vanish, instantly.
const t0 = Date.now();
await page.evaluate(() => {
  const el = document.querySelector('#min');
  el.value = '40';
  el.dispatchEvent(new Event('input', { bubbles: true }));
});
await new Promise((r) => setTimeout(r, 300)); // one CD pass is plenty; margin for safety
const after = await page.evaluate(() => {
  const miningRows = [...document.querySelectorAll('app-market-table')][0]?.querySelectorAll('tbody tr') ?? [];
  return {
    minLabel: document.querySelector('#min')?.parentElement?.querySelector('b')?.textContent?.trim(),
    firstMining: miningRows[0]?.querySelector('td')?.textContent?.trim(),
    miningCount: miningRows.length,
  };
});
check('slider-rerank', after.firstMining !== seedState.firstMining && after.minLabel === '40',
  `MIN 40 -> top mining = ${after.firstMining} (${after.miningCount} rows), took ${Date.now() - t0}ms incl. settle`);
await page.screenshot({ path: path.join(SHOT_DIR, '02-slider-min40.png') });

// Restore MIN 89
await page.evaluate(() => {
  const el = document.querySelector('#min');
  el.value = '89';
  el.dispatchEvent(new Event('input', { bubbles: true }));
});

// The world select must show the configured world once options load (regression check).
const worldShown = await page.evaluate(() => {
  const sel = document.querySelector('.header-right select');
  return sel && sel.options.length > 1 ? sel.options[sel.selectedIndex]?.text : '(no options)';
});
check('world-select-display', worldShown === 'Cactuar', `header world select shows ${worldShown}`);

// Auto-sweep on stale boot: the bundled seed counts as stale, so on a
// clean-room run the live sweep must land with NO clicks at all.
try {
  await page.waitForFunction(
    () => document.querySelector('.header-status')?.textContent?.includes('swept just now'),
    undefined,
    { timeout: 120_000 },
  );
  // Make sure we're on the Sweep page before inspecting it (a human at the
  // machine can navigate the visible window mid-run — it happened).
  await page.evaluate(() => {
    [...document.querySelectorAll('.sidebar a')].find((a) => a.textContent?.trim() === 'Sweep')?.click();
  });
  await page.waitForSelector('app-market-table tbody tr', { timeout: 10_000 });
  const live = await page.evaluate(() => {
    const status = document.querySelector('.header-status')?.textContent?.trim();
    const miningRows = [...document.querySelectorAll('app-market-table')][0]?.querySelectorAll('tbody tr') ?? [];
    const firstCells = [...(miningRows[0]?.querySelectorAll('td') ?? [])].map((td) => td.textContent?.trim());
    const movers = [...document.querySelectorAll('.kpi')].find((k) =>
      k.querySelector('.kpi-label')?.textContent?.includes('Movers'))?.querySelector('.kpi-value')?.textContent?.trim();
    return { status, firstCells, movers };
  });
  check('auto-sweep-no-clicks', !!live.status?.includes('swept just now'), live.status ?? '(no status)');
  check('live-delta-present', (live.firstCells?.[3] ?? '').includes('%'), `top row Δ% cell = ${live.firstCells?.[3]}`);
  console.log('Live top mining row:', JSON.stringify(live.firstCells));
  console.log('Movers count:', live.movers);

  // Sparklines: seed + auto-sweep = 2 history points, enough to draw.
  const sparkCount = await page.evaluate(() => document.querySelectorAll('svg.spark').length);
  check('sparklines', sparkCount > 0, `${sparkCount} sparklines after live sweep`);

  // Sweep-page crafts digest: appears once a live sweep computed margins.
  const digest = await page.evaluate(() => {
    const h2 = [...document.querySelectorAll('h2')].find((h) => h.textContent?.includes('Process before selling'));
    const rows = h2 ? (h2.parentElement?.querySelectorAll('tbody tr').length ?? 0) : 0;
    const first = h2 ? h2.parentElement?.querySelector('tbody tr td')?.textContent?.trim() : null;
    const kpi = [...document.querySelectorAll('.kpi')]
      .find((k) => k.querySelector('.kpi-label')?.textContent?.includes('Top craft'))
      ?.querySelector('.kpi-value')?.textContent?.trim();
    return { rows, first, kpi };
  });
  check(
    'sweep-crafts-digest',
    digest.rows > 0 && !!digest.kpi && digest.kpi !== '—',
    `${digest.rows} digest rows, top: ${digest.first}; KPI: ${digest.kpi}`,
  );

  // Crafting page: margins from the fresh snapshot, filtered to farmable mats.
  await page.evaluate(() => {
    [...document.querySelectorAll('.sidebar a')].find((a) => a.textContent?.trim() === 'Crafting')?.click();
  });
  await page.waitForSelector('app-crafting tbody tr', { timeout: 10_000 });
  const craft = await page.evaluate(() => {
    const rows = [...document.querySelectorAll('app-crafting tbody tr')];
    const first = [...(rows[0]?.querySelectorAll('td') ?? [])].map((td) => td.textContent?.trim());
    return { count: rows.length, first };
  });
  check(
    'crafting-rows',
    craft.count > 3 && !!craft.first?.[5]?.match(/\d/),
    `${craft.count} crafts; top: ${craft.first?.[0]} margin ${craft.first?.[5]} (${craft.first?.[6]}) uses ${craft.first?.[7]}`,
  );
  await page.screenshot({ path: path.join(SHOT_DIR, '05-crafting.png') });

  // Crafter-level gating: dropping CRP to 1 must remove every CRP recipe
  // (the row count can stay flat — the top-40 refills from the larger pool).
  const beforeGate = await page.evaluate(() => {
    const rows = [...document.querySelectorAll('app-crafting tbody tr')];
    return { count: rows.length, hasCRP: rows.some((r) => /CRP \d/.test(r.textContent)) };
  });
  await page.evaluate(() => {
    [...document.querySelectorAll('.sidebar a')].find((a) => a.textContent?.trim() === 'Settings')?.click();
  });
  await page.waitForSelector('#cr-CRP', { timeout: 10_000 });
  await page.evaluate(() => {
    const el = document.querySelector('#cr-CRP');
    el.value = '1';
    el.dispatchEvent(new Event('change', { bubbles: true }));
  });
  await page.evaluate(() => {
    [...document.querySelectorAll('.sidebar a')].find((a) => a.textContent?.trim() === 'Crafting')?.click();
  });
  await page.waitForSelector('app-crafting tbody tr', { timeout: 10_000 });
  const afterGate = await page.evaluate(() => {
    const rows = [...document.querySelectorAll('app-crafting tbody tr')];
    return { count: rows.length, hasCRP: rows.some((r) => /CRP \d/.test(r.textContent)) };
  });
  check(
    'crafter-gating',
    beforeGate.hasCRP && !afterGate.hasCRP,
    `CRP rows before: ${beforeGate.hasCRP} (${beforeGate.count}) -> after CRP=1: ${afterGate.hasCRP} (${afterGate.count})`,
  );
  await page.evaluate(() => {
    [...document.querySelectorAll('.sidebar a')].find((a) => a.textContent?.trim() === 'Settings')?.click();
  });
  await page.waitForSelector('#cr-CRP', { timeout: 10_000 });
  await page.evaluate(() => {
    const el = document.querySelector('#cr-CRP');
    el.value = '100';
    el.dispatchEvent(new Event('change', { bubbles: true }));
  });

  // Trends page: digest table + charts render from local history.
  await page.evaluate(() => {
    [...document.querySelectorAll('.sidebar a')].find((a) => a.textContent?.trim() === 'Trends')?.click();
  });
  await page.waitForSelector('app-trend-chart svg', { timeout: 10_000 });
  const trends = await page.evaluate(() => ({
    charts: document.querySelectorAll('app-trend-chart svg').length,
    chartPoints: document.querySelectorAll('app-trend-chart svg circle').length,
    digestRows: document.querySelectorAll('app-trends .section table tbody tr').length,
    sinceHeader: [...document.querySelectorAll('h2')].some((h) => h.textContent?.includes('Since')),
  }));
  check(
    'trends-page',
    trends.charts === 2 && trends.chartPoints >= 4 && trends.sinceHeader && trends.digestRows > 0,
    JSON.stringify(trends),
  );
  await page.screenshot({ path: path.join(SHOT_DIR, '07-trends.png') });

  // Snapshot housekeeping: stats render; prune is a no-op-safe click (keeps newest per day).
  await page.evaluate(() => {
    [...document.querySelectorAll('.sidebar a')].find((a) => a.textContent?.trim() === 'Settings')?.click();
  });
  await page.waitForSelector('#snap-stats', { timeout: 10_000 });
  await new Promise((r) => setTimeout(r, 400));
  const snapStats = await page.evaluate(() => document.querySelector('#snap-stats')?.textContent?.trim());
  check('snapshot-stats', !!snapStats?.match(/\d+ snapshots/), snapStats ?? '(missing)');

  // Verify & track: a real gatherable gets Garland-verified and added.
  await page.evaluate(() => {
    const el = document.querySelector('#add-item');
    el.value = 'Electrum Ore';
    el.dispatchEvent(new Event('input', { bubbles: true }));
  });
  await page.evaluate(() => document.querySelector('#btn-track')?.click());
  await page.waitForSelector('.track-result', { timeout: 30_000 });
  const tr = await page.evaluate(() => ({
    text: document.querySelector('.track-result')?.textContent?.trim(),
    custom: [...document.querySelectorAll('#custom-items tbody tr')].map((r) => r.querySelector('td')?.textContent?.trim()),
  }));
  check(
    'verify-track',
    !!tr.custom.some((n) => n?.includes('Electrum Ore')),
    `${tr.text} · custom list: ${tr.custom.join(', ') || '(empty)'}`,
  );

  // Detail panel: back to Sweep, click the top mining row, wait for live data.
  await page.evaluate(() => {
    [...document.querySelectorAll('.sidebar a')].find((a) => a.textContent?.trim() === 'Sweep')?.click();
  });
  await page.waitForSelector('app-market-table tbody tr', { timeout: 10_000 });
  await page.evaluate(() =>
    document.querySelector('app-market-table tbody tr')?.dispatchEvent(new MouseEvent('click', { bubbles: true })),
  );
  await page.waitForFunction(
    () => {
      const p = document.querySelector('.detail-panel');
      return !!p && !p.textContent?.includes('Fetching');
    },
    undefined,
    { timeout: 30_000 },
  );
  const detail = await page.evaluate(() => {
    const p = document.querySelector('.detail-panel');
    const listings = p?.querySelectorAll('table tbody tr').length ?? 0;
    const kpis = [...(p?.querySelectorAll('.kpi-value') ?? [])].map((k) => k.textContent?.trim());
    const peak = [...(p?.querySelectorAll('.meta') ?? [])]
      .map((m) => m.textContent?.trim())
      .find((t) => t?.includes('Peak'));
    return { name: p?.querySelector('h3')?.textContent?.trim(), listings, kpis, peak };
  });
  check(
    'detail-panel',
    !!detail.name && detail.listings > 0 && !!detail.kpis?.[0]?.match(/\d/),
    `${detail.name}: min ${detail.kpis?.[0]}, stock ${detail.kpis?.[1]} · ${detail.peak}`,
  );
  await page.screenshot({ path: path.join(SHOT_DIR, '06-detail.png') });
} catch (e) {
  const err = await page.evaluate(() => document.querySelector('.status.error')?.textContent?.trim() ?? '(none)');
  check('live-phase', false, `live-data phase aborted (${e.message}); error banner: ${err}`);
}
await page.screenshot({ path: path.join(SHOT_DIR, '03-live.png') });

// Retainers page: live listings advice for the current top farmables.
await page.evaluate(() => {
  [...document.querySelectorAll('.sidebar a')].find((a) => a.textContent?.trim() === 'Retainers')?.click();
});
try {
  await page.waitForFunction(
    () => {
      const rows = [...document.querySelectorAll('app-retainers tbody tr')];
      return rows.length >= 5 && !rows.some((r) => r.textContent?.includes('Fetching'));
    },
    undefined,
    { timeout: 45_000 },
  );
  const ret = await page.evaluate(() => {
    const rows = [...document.querySelectorAll('app-retainers tbody tr')];
    const first = [...(rows[0]?.querySelectorAll('td') ?? [])].map((td) => td.textContent?.trim());
    const verdicts = rows.map((r) => r.querySelector('td:last-child span')?.textContent?.trim());
    return { count: rows.length, first, verdicts: [...new Set(verdicts)] };
  });
  check('retainers-rows', ret.count >= 5, `${ret.count} plan rows`);
  check('retainers-advice', !!ret.first?.[4]?.match(/\d/) && !!ret.first?.[8],
    `first row: list at ${ret.first?.[4]}, stack ${ret.first?.[5]}, verdict "${ret.first?.[8]}"`);
  console.log('Verdicts seen:', JSON.stringify(ret.verdicts));
} catch {
  check('retainers-rows', false, 'timed out waiting for live listings advice');
}
// Retainer click-through: rows open the same drill-down panel.
try {
  await page.evaluate(() =>
    document.querySelector('app-retainers tbody tr')?.dispatchEvent(new MouseEvent('click', { bubbles: true })),
  );
  await page.waitForFunction(
    () => {
      const p = document.querySelector('.detail-panel');
      return !!p && !p.textContent?.includes('Fetching');
    },
    undefined,
    { timeout: 30_000 },
  );
  const rName = await page.evaluate(() => document.querySelector('.detail-panel h3')?.textContent?.trim());
  check('retainer-click-detail', !!rName, `panel opened: ${rName}`);
} catch {
  check('retainer-click-detail', false, 'panel never loaded');
}
await page.screenshot({ path: path.join(SHOT_DIR, '04-retainers.png') });

// Tray + close-to-tray: closing the window hides it; the app (and tray) live on.
try {
  const trayState = await app.evaluate(async ({ BrowserWindow }) => {
    const w = BrowserWindow.getAllWindows()[0];
    w.close();
    await new Promise((r) => setTimeout(r, 600));
    return {
      tray: globalThis.__gilsweepTray === true,
      windows: BrowserWindow.getAllWindows().length,
      visible: w.isDestroyed() ? 'destroyed' : w.isVisible(),
    };
  });
  // Headless Linux has no system tray; the guard in main.ts degrades gracefully,
  // so only assert tray creation on Windows. Hide-on-close must work everywhere.
  check(
    'tray-close-to-tray',
    (IS_WIN ? trayState.tray : true) && trayState.windows === 1 && trayState.visible === false,
    JSON.stringify(trayState),
  );
} catch (e) {
  check('tray-close-to-tray', false, e.message);
}

// Real quit (close-to-tray intercepts window closes, so quit explicitly).
await app.evaluate(({ app: eApp }) => eApp.quit()).catch(() => {});
await Promise.race([app.close().catch(() => {}), new Promise((r) => setTimeout(r, 8000))]);
console.log('\nSUMMARY ' + JSON.stringify(results));
process.exit(failed ? 1 : 0);

