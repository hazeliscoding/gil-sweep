# Roadmap

Gil Sweep answers one question: **what should I farm for gil right now?** v1 (Electron + Angular, `v1.0.0`) shipped everything in `CHANGELOG.md`. v2 is a native rewrite; the plan and the inventory of what moves where are in `docs/migration-v2.md`.

## Decisions

- **2026-10-05** — v2 is .NET 10 + Avalonia 12 + MVVM (CommunityToolkit.Mvvm), laid out like XIV Vault: `GilSweep.Core`, `GilSweep.Desktop`, `GilSweep.Tests`, `tools/GilSweep.Screenshots`. No Node at runtime and no bridge to the old app.
- **2026-10-05** — The Electron app is frozen at tag `electron-final`. The rewrite lives on `rewrite/dotnet-avalonia`; `main` stays on v1 until the cutover gate in `docs/migration-v2.md` passes.
- **2026-10-05** — Six screens: Sweep, Market, Craft, Watchlist, History, Settings. Export, updates, notifications and webhooks live inside them, never as their own pages.
- **2026-10-05** — No gil/hour. Gil Sweep doesn't know yield, travel time or gathering speed, so it ranks by an explainable opportunity score (`docs/opportunity-scoring.md`). The Farm Session is a queue of opportunities, not a route.
- **2026-10-05** — Ranking keeps v1's rules (gates, trap kinds, world-scope liquidity, throughput as the market measure). Changes are listed in the scoring doc and covered by tests.
- **2026-10-05** — Data lives in `%APPDATA%\GilSweep`. The v1 config, stars and snapshots in `%APPDATA%\gil-sweep` are imported once, so history carries over.
- **2026-10-05** — The bundled seed snapshot is no longer shown as live prices; first run goes straight to a real sweep.
- **2026-10-05** — Typography follows XIV Vault: IBM Plex Sans and IBM Plex Mono, bundled. The approved mockup's Quorum tokens with a gold accent (`#D9A94A`) used sparingly; green and red only for market movement, always with an arrow or word.
- **2026-10-05** — Velopack per-user Setup with in-app update and restart, plus a portable zip. An update never installs during a sweep.
- **2026-10-05** — The old plan for a v2 web build is dropped; see Not planned.

## v2.0 — Native rewrite

- [ ] **Foundation** — solution, central package versions, DI, logging, config, theme tokens, app shell with the six-item sidebar and Eorzea clock.
  Done when: `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` pass and the window opens.
- [ ] **Domain models** — catalog, nodes and spawn windows, snapshots, opportunities, watch entries; persisted shapes round-trip, v1 snapshots read.
  Done when: serialization tests pass against v1 files.
- [ ] **Market integrations** — Universalis and Saddlebag behind interfaces, fixtures for normal, sparse, stale, empty and high-volume markets, retries and partial failure.
  Done when: every adapter test runs offline.
- [ ] **Sweep engine** — v1 ranking ported with characterization tests against outputs captured from the v1 TypeScript; opportunity score on top.
  Done when: v2 matches v1 on the fixtures except for the documented changes.
- [ ] **Eorzea scheduling** — clock, windows, next spawn, countdowns, feeding the score.
  Done when: timed-node tests cover wrap-around and multiple spawns.
- [ ] **First vertical slice** — launch → load config → sweep → ranked recommendations with reasons → open an item.
  Done when: it works against live Universalis and against fixtures.
- [ ] **Market, Craft, Watchlist, History, Settings** — in that order, each on tested view models.
- [ ] **Notifications** — node windows, price spikes and crashes, undercuts; Windows toasts; tray with the spawn clock.
- [ ] **Packaging and updates** — Velopack Setup, update check and restart, portable zip, release workflow with checksums.
- [ ] **Screenshots** — deterministic screenshots of every screen and state from fake data; README rewritten.
- [ ] **Cutover** — gate passed, `legacy/` and Node files removed, branch merged, v2.0.0 tagged.

## Later

- Discord webhooks for price alerts and the weekly summary (the app must be running to send).
- In-game item icons beside names.
- Live watch over the Universalis websocket for faster crash and undercut alerts.
- Gil per scrip for the scrip vendors.
- Sortable columns and CSV export on every table.

## Not planned

- A web build or hosted backend, accounts, cloud sync, telemetry.
- Discord bot, multi-character profiles, cross-DC arbitrage, macOS builds, localized item names.
- Route planning or gil/hour estimates without yield and travel data.
