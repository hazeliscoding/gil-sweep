# Changelog

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
