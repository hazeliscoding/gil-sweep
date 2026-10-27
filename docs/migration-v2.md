# Migration to v2 (.NET + Avalonia)

Gil Sweep v1 is an Electron + Angular app. v2 replaces it with a native .NET 10 / Avalonia 12 app built like [XIV Vault](https://github.com/hazeliscoding/xiv-vault), and redesigns the product around one question: **what should I farm for gil right now?**

- The last Electron build is tagged `electron-final` (it is `v1.0.0` plus documentation). Release `v1.0.0` stays published.
- The rewrite happens on `rewrite/dotnet-avalonia`. `main` keeps the Electron app until the cutover gate below passes.
- During the rewrite the Electron source lived in `legacy/electron/` as porting reference only; nothing in v2 called it. It was removed at cutover, after the gate below passed. Commit f0be844 is the last with it and with the characterization harness (`legacy/characterize`).

## Inventory

Paths on the left are v1 (`desktop/...` at tag `electron-final`).

### Keep

| v1 | v2 |
| --- | --- |
| `data/items.json` (104 curated items, Garland-verified nodes and spawn hours) | `src/GilSweep.Core/Data/items.json`, embedded |
| `data/crafts.json` (635 recipes) | `src/GilSweep.Core/Data/crafts.json`, embedded |
| `data/garland-demand.json` (recipe consumers, leves, GC supply) | `src/GilSweep.Core/Data/garland-demand.json`, embedded |
| `data/seed-snapshot.json` (a real Cactuar sweep, 2026-07-19) | Test fixture for characterization tests; no longer shown as live data |
| Snapshot JSON format (`sweep-<iso>-<world>.json`) | Same field names, read and written by v2, so v1 history imports as-is |
| `LICENSE`, `NOTICE`, `CHANGELOG.md` | Unchanged; v2 entries added |

### Port (same behavior, characterization-tested)

| v1 | What it does | v2 |
| --- | --- | --- |
| `renderer/.../ranking.ts` | Lock reasons, MSQ and level gates, farmable filter (world-scope velocity only, never vendor/submarine/venture), map pick, crystals, movers, locked list, crafter gating, top value crafts, folklore annotation, retainer targets | `Core/Sweep/Ranking.cs` |
| `renderer/.../eorzea.ts`, `main/core/eorzea.ts` | Eorzea clock (1 ET day = 70 real minutes), node windows incl. midnight wrap, next windows for the tray | `Core/Time/EorzeaTime.cs`, `NodeWindows.cs` |
| `main/core/sweep.service.ts` | Pricing pass (items + demand consumers + recipe ingredients), rows sorted by throughput, change vs previous snapshot, Saddlebag join, untracked top sellers, two-pass craft margins with min(buy, craft) one level deep and HQ-aware outputs, digest, prune, backfill, history series | `Core/Sweep/SweepEngine.cs`, `Core/Crafting/CraftCalculator.cs`, `Core/History/*` |
| `main/core/universalis.ts` | Aggregated prices in chunks of 100, world → dc → region fallback with the answering scope recorded | `Core/Market/Universalis/UniversalisClient.cs` |
| `main/core/saddlebag.ts` | Optional trend states; failure never fails a sweep | `Core/Market/Saddlebag/SaddlebagClient.cs` |
| `main/core/demand.ts` | "Why it sells" line and ranked consumers | `Core/Catalog/DemandIndex.cs` |
| `main/core/market-detail.ts` | Live listings and sales: cheapest, sales median, listed quantity, units/day, days of stock | `Core/Market/MarketAnalyzer.cs` |
| `main/core/retainer.service.ts` | List price (undercut by 1, hold on a crashed floor), stack size from median sale quantity, saturation verdict | `Core/Market/MarketAnalyzer.cs` (shown on the Market screen) |
| `main/core/config.service.ts` | World, gatherer levels, MSQ, folklore, crafters, close to tray, Saddlebag query; missing keys keep defaults | `Core/Configuration/*`, plus a one-time import of the v1 config and snapshots |
| `main/core/verify.service.ts` | Track a new item by name: Garland node check, trap classification | `Core/Catalog/ItemVerifier.cs` |
| `main/alerts.ts` | Node-window toast on closed → open, batched price-swing toast (≥ 25%) after a sweep, gated by level and MSQ | `Core/Alerts/AlertService.cs` + Windows toasts in Desktop |
| `main/main.ts` tray | Spawn-clock tray, close to tray | Desktop tray icon |

### Redesign

| v1 | v2 |
| --- | --- |
| Sweep dashboard: sliders, KPI cards, mining and botany tables | **Sweep**: one best farm right now with its reasons, a ranked list, Available now / Soon from the Eorzea clock, an optional Farm Session queue |
| Ranking by market throughput only | **Opportunity score**: throughput-based market strength plus listing depth, trend and node availability. See `docs/opportunity-scoring.md` |
| Market drill-down side panel | **Market** screen for one item, with the reasons it ranks where it does and selling advice |
| Crafting table | **Craft**: sell raw or process first, per recipe, with locked recipes shown |
| Retainers page | Selling advice moves into Market |
| Watch stars (`watched: number[]`) | **Watchlist**: one entry per item with node, spike, crash, undercut and favorite switches. v1 stars import with node + spike + crash on |
| Trends page | **History**: past sweeps, movers, weekly summary, stats, CSV export |
| Settings page, onboarding modal | **Settings** and a three-question first run |
| electron-updater NSIS installer | Velopack Setup with in-app update and restart, plus a portable zip |

### Drop

- Electron main process, preload, IPC channels and `window.api`.
- Angular renderer, signals store, standalone components, CSS.
- npm scripts, `package.json`, lock files, electron-builder config.
- The Playwright end-to-end driver (replaced by view model tests and the screenshot tool).
- Level and MSQ sliders on the dashboard (levels live in Settings; changes still re-rank without a network call).
- Showing the bundled seed snapshot as if it were live data.

### Defer

Not part of v2.0; tracked in `ROADMAP.md` under Later or Not planned.

- Discord webhooks (v1.1 on the old roadmap, never shipped).
- In-game item icons, a crafter-level slider, sortable columns everywhere.
- Live Universalis websocket watch.
- Scrip economy page.
- Web build, Discord bot, multi-character profiles, cross-DC arbitrage, route optimization, gil/hour, localization.

## Cutover gate

Passed on 2026-10-06. `legacy/` was deleted once v2 could, with tests to show it:

- [x] load settings and progression, including a v1 config;
- [x] fetch, or load from fixtures, representative market data;
- [x] normalize it into Core models (no API types outside the adapters);
- [x] rank gathering opportunities;
- [x] respect gatherer levels, MSQ progress and trap items;
- [x] respect node availability from the Eorzea clock;
- [x] show the Sweep screen;
- [x] explain why the top items are recommended;
- [x] pass offline characterization tests against the v1 outputs.

Given the same input, v2 must recommend the same items as v1 or differ only in ways listed in `docs/opportunity-scoring.md` under "Changes from v1".

| Gate | Shown by |
| --- | --- |
| Settings, including a v1 config | `SettingsTests` |
| Market data, live and recorded | `LiveMarketTests` (explicit), `SweepServiceTests`, `MarketScenarioTests` |
| Normalized models | Adapters in `Core/Market/Universalis`, `Core/Market/Saddlebag`; everything else uses Core types |
| Ranking, gates, traps | `RankingCharacterizationTests`, `OpportunityScoringTests` |
| Node availability | `EorzeaCharacterizationTests`, `OpportunityScoringTests` |
| Sweep screen and explanations | `SweepScreenTests`, `docs/screenshots/sweep.png` |
| Characterization against v1 | everything under `tests/GilSweep.Tests/Fixtures/V1` |
