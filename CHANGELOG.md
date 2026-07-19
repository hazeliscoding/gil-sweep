# Changelog

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
