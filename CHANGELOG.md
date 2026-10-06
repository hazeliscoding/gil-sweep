# Changelog

## [2.0.0] — 2026-10-06

A native rewrite (.NET 10 and Avalonia) around one question: what should I farm for gil right now?

- **Sweep** puts the best farm you can gather this minute at the top, with its price, sales, competition, trend and node, and the reasons it ranks there. More opportunities, what is open now and what opens soon, the daily map pick and a craft worth processing sit below. A **Farm Session** (15 minutes to "Chill") queues what to gather now, the timed nodes opening while you play, and a fallback; travel and yield are never guessed.
- Recommendations are ranked by an **opportunity score**: v1's market throughput, adjusted for days of stock on the market board, the week's price trend and the Eorzea clock. There is no gil per hour. With those adjustments set aside the order is exactly v1's (`docs/opportunity-scoring.md`).
- **Market** replaces the drill-down panel and the Retainers page: live listings and sales, two weeks of prices, selling hours, days of stock, where and when to gather, who buys it, and what to list it at.
- **Craft** answers sell raw or process first, per material you farm, with locked recipes shown against your crafter levels.
- **Watchlist** replaces stars: per item, a reminder before its node opens (5 minutes by default, or on opening as in v1), price spikes and crashes, undercuts of your retainers, and favorites. Alerts are Windows toasts and stay in a short log.
- **History** replaces Trends: past sweeps, the week's movers, a summary, watched items over seven days, CSV export. Sweeps from the last two days are all kept, then one a day, for 90 days by default.
- **Settings** gains retainer names, hourly sweeps, an optional Saddlebag source and history retention; levels and story progress move here from the dashboard sliders.
- Sweeps now read listing depth from current listings, run every hour while the app is open, and survive a provider failing part-way. When Universalis is down, the last sweep stays on screen with a banner and a retry.
- Your v1 settings, watched items, tracked items and sweep history are imported on first start.
- Installed with Velopack: per-user Setup with a Start menu entry, update and restart from Settings (never during a sweep), and a portable zip. v1 doesn't update itself to v2.

## [1.0.0] — 2026-07-19

- NSIS installer alongside the portable exe. Installed builds auto-update from GitHub releases; the portable exe skips update checks (re-download to update).
- End-to-end suite in CI: 28 behavioral checks drive the real app (onboarding, sweep, crafting, retainers, trends, drill-down, tray) under Xvfb on every push.
- Hardening: tray-construction failures degrade gracefully instead of crashing the app.

## [0.9.0] — 2026-07-19

- Verify & track (Settings): add any item by name — Garland's node data decides gatherability and brings level, zone, and spawn hours along. Crafted/vendor/voyage top sellers get classified and remembered as never-farm traps. Custom items live in userData and are removable.
- First-run onboarding: world, gatherer levels, and MSQ progress asked up front instead of assuming defaults.

## [0.8.0] — 2026-07-19

- HQ-aware craft margins: outputs price at HQ when the HQ market out-trades NQ (marked with an HQ tag). Materials stay NQ — gathered mats have no HQ since 6.0.
- min(buy, craft) costing: each ingredient costs whichever is cheaper, the market listing or crafting it from its own mats (one level deep), so nugget→ingot chains cost honestly.
- Crafter levels: per-job levels in Settings gate the Crafting page and the sweep digest.

## [0.7.0] — 2026-07-19

- Trends page: week-over-week digest (latest snapshot vs a ~week-old baseline, ranked by market-throughput change) with prune suggestions for farm-rotation items under ~5 sold/day across the last three sweeps.
- Item history charts: full-size price and velocity charts per item from the local snapshot archive; click a digest row to chart it.
- Snapshot housekeeping: archive stats and a prune-to-one-per-day button in Settings.

## [0.6.0] — 2026-07-19

- Watch stars: star any item in any table. Watched timed nodes fire a desktop notification the moment their window opens (with time remaining; click to open the app), respecting your levels and MSQ progress.
- Price-spike alerts: after each sweep, a batched notification lists watched items that swung ≥25%.
- Public roadmap to v1.0.0 in ROADMAP.md.

## [0.5.0] — 2026-07-19

- Spawn-clock tray: the tray icon shows the Eorzea clock and the next timed-node windows for your character. Closing the window hides to the tray by default (Settings toggle; quit via the tray menu).
- Crafts on the dashboard: a "Top craft value" KPI and a top-5 "Process before selling" table on the Sweep page, linking to the full Crafting page.
- Retainer rows open the market drill-down panel, same as sweep rows.
- Quick filter box on the Sweep page — narrows all tables by item or zone as you type.

## [0.4.0] — 2026-07-19

- Crafting page: craft value-add margins for 635 recipes, computed at sweep time (sale price × yield − ingredient cost at cheapest listings, crystals included). Ranked by margin × daily sales, filtered by default to crafts using what your sliders say you can farm; recipes with vendor-only ingredients are excluded rather than mis-costed.
- Market drill-down: click any row for live listing depth, recent sales, days-of-stock, and an hour-of-day posting-window histogram (local time).
- Sparkline backfill: fresh installs pull one round of Universalis sale history per world (quantity-weighted daily averages), so trend lines have shape from the first minute.

## [0.3.0] — 2026-07-19

- Auto-sweep on launch: when the loaded snapshot is the bundled seed, older than a day, or from another world, a background sweep refreshes it quietly (no spinner; auto-refresh failures stay silent offline).
- Price sparklines: every item name carries an inline trend line built from your accumulated snapshots — they get richer with every sweep.
- Folklore books: per-expansion checkboxes in Settings; legendary nodes without their book show a small "folklore" tag.

## [0.2.0] — 2026-07-19

- Live Eorzea clock in the header (1 ET minute ≈ 2.9 real seconds — it ticks).
- "Node (ET)" column on every market table: up-now / next-window timers, shown in real minutes, for all 18 timed nodes (unspoiled, legendary, ephemeral).
- Spawn data added to the bundled item DB: hours verified against Garland Tools node data, window durations from Teamcraft open data.
- Header wordmark restyled to GilSweep; README polish.

## [0.1.0] — 2026-07-18

Initial public release.

- Sweep dashboard: prices a curated database of ~100 gatherable items on your world (Universalis aggregates), with Saddlebag Exchange trend states and week-over-week price deltas from accumulated snapshots.
- "Why it sells" demand attribution per item: recipe consumers ranked by downstream sale velocity, leve turn-ins, and GC supply missions (Garland Tools data).
- Miner/Botanist level + MSQ progress sliders re-rank everything client-side, instantly — no refetching.
- Daily map pick (one per 18h per character), crystal prices, movers (≥25% price swings), and a locked/watchlist section.
- Retainer selling plan from live listings: undercut price with crashed-floor detection, median-sale stack sizing, and days-of-stock saturation verdicts.
- Ships as a portable Windows exe (unsigned — SmartScreen warns on first run).
