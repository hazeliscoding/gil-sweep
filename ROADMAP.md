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
- **2026-10-05** — Competition comes from the stack-size histogram of current listings: Universalis's listingsCount and unitsForSale only count the listings a request returns. Current listings are fetched 20 items at a time; larger batches time out (504).
- **2026-10-05** — The trend baseline is the newest snapshot at least 5 days old, else the oldest at least 20 hours old; hourly sweeps are too close together to show a trend. Movers and price alerts keep v1's change since the previous sweep.
- **2026-10-05** — Snapshots from the last 48 hours are all kept, older days keep their newest, and history older than 90 days is deleted (imported v1 history is kept in full). Only the newest snapshot keeps craft margins.
- **2026-10-05** — Node reminders arrive 5 minutes before a watched window opens by default; 0 restores v1's alert on opening.
- **2026-10-05** — Toasts register Gil Sweep's AppUserModelID under HKCU (removed on uninstall), so the Desktop project targets the Windows 10 SDK.
- **2026-10-06** — Craft shows one card per farmed material (its best recipe), since a material like aethersand feeds a hundred recipes and the question is what to do with the material.
- **2026-10-06** — First-run answers default to v1's onboarding (Miner 90, Botanist 90, Dawntrail).
- **2026-10-06** — v1 doesn't update itself to v2; the README tells v1 users to install v2 with Setup, which imports their data.
- **2026-10-06** — Gear stats gate only on the game's own minimums: Perception on node items, Craftsmanship and Control on recipes (XIVAPI's `GatheringItem` and `Recipe` sheets). Level alone overstates what a character can gather or craft. No GP or CP, success-rate estimates or HQ checks: the game data has no minimum for them, and estimating would break the no-estimates rule.
- **2026-10-06** — Perception is entered per gatherer; one Craftsmanship and Control covers every crafter, since crafters share armor and only the tools differ. A blank stat isn't checked, so level-only behavior and v1 parity stay.

## v2.0 — Native rewrite

- [x] **Foundation** — solution, central package versions, DI, logging, config, theme tokens, app shell with the six-item sidebar and Eorzea clock.
  Done when: `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` pass and the window opens.
- [x] **Domain models** — catalog, nodes and spawn windows, snapshots, opportunities, watch entries; persisted shapes round-trip, v1 snapshots read.
  Done when: serialization tests pass against v1 files.
- [x] **Market integrations** — Universalis and Saddlebag behind interfaces, fixtures for normal, sparse, stale, empty and high-volume markets, retries and partial failure.
  Done when: every adapter test runs offline.
- [x] **Sweep engine** — v1 ranking ported with characterization tests against outputs captured from the v1 TypeScript; opportunity score on top.
  Done when: v2 matches v1 on the fixtures except for the documented changes.
- [x] **Eorzea scheduling** — clock, windows, next spawn, countdowns, feeding the score.
  Done when: timed-node tests cover wrap-around and multiple spawns.
- [x] **First vertical slice** — launch → load config → sweep → ranked recommendations with reasons → open an item.
  Done when: it works against live Universalis and against fixtures.
- [x] **Market, Craft, Watchlist, History, Settings** — in that order, each on tested view models.
- [x] **Notifications** — node windows, price spikes and crashes, undercuts; Windows toasts; tray with the spawn clock.
- [x] **Packaging and updates** — Velopack Setup, update check and restart, portable zip, release workflow with checksums.
- [x] **Screenshots** — deterministic screenshots of every screen and state from fake data; README rewritten.
- [x] **Cutover** — gate passed (2026-10-06, see `docs/migration-v2.md`), `legacy/` and the Node files removed.
- [ ] **Release** — merge `rewrite/dotnet-avalonia` into `main`, tag `v2.0.0`, publish the draft release.
  Done when: Setup from the release installs, finds a later test release and updates in place.

## v2.1 — Gear stats

- [ ] **Requirement data** — `items.json` gains each item's minimum Perception and `crafts.json` each recipe's minimum Craftsmanship and Control, from XIVAPI; a script in `scripts/` regenerates them after a patch. Items added in Settings get their Perception the same way. 77 bundled recipes don't match XIVAPI by result item and need their recipe rows found.
  Done when: tests read Dense Aluminum Ore's 5,090 Perception and Gold Ingot's 391 Craftsmanship and 374 Control from recorded XIVAPI responses.
- [ ] **Gear locks in Core** — optional Miner and Botanist Perception and crafter Craftsmanship and Control in settings. An item below its Perception minimum locks ("Needs 5,090 Perception (you have 4,800)"); a recipe below either minimum locks the same way. Listed under "Changes from v1" in `docs/opportunity-scoring.md`.
  Done when: with blank stats every v1 characterization test passes unchanged, and lock tests cover below, at and blank.
- [ ] **Settings and screens** — the stat fields in the Character and Crafters cards, in the mockup's existing style; locked rows and craft cards show the shortfall. First run doesn't ask for stats.
  Done when: the screenshots include a Perception lock and a Craftsmanship lock.
- [ ] **Copy diagnostics** — a Settings button copies the version, Windows version, recent errors and the log folder for a bug report. Nothing is sent anywhere.
  Done when: the copied text names the data folder as `%AppData%\GilSweep` and contains no username or other private path.
- [ ] **Code signing** — the release workflow signs `GilSweep.exe` and Setup through SignPath Foundation (free for open source).
  Done when: both files carry a valid signature and SmartScreen no longer shows "Unknown publisher".
- [ ] **Release** — tag `v2.1.0` and publish. It doubles as the later test release v2.0's Release item waits on.
  Done when: v2.0.0 installed from Setup updates itself to v2.1.0.

## v2.2 — Better answers

- [ ] **Wider catalog** — a weekly scan prices every gatherable item the character can reach, from game data, not only the 104 tracked items; the ones that trade on the world join the ranking. The tracked list stays as the base, along with its trap items.
  Done when: on recorded data, a scan ranks a gatherable missing from `items.json`, with its node and any spawn window, within Universalis's request limits.
- [ ] **Not interested** — hide an item from recommendations from its card; it stays on Market and can be restored in Settings.
  Done when: a hidden item leaves Sweep and the Farm Session, and returns when restored.
- [ ] **Prices after tax** — sale proceeds and craft margins subtract the market tax of the retainers' city (Universalis tax rates). Listed under "Changes from v1".
  Done when: margins on recorded data are net of the recorded tax rate, and the scoring doc's worked example matches.

## Later

- Discord webhooks for price alerts and the weekly summary (the app must be running to send).
- In-game item icons beside names.
- Live watch over the Universalis websocket for faster crash and undercut alerts.
- Gil per scrip for the scrip vendors.
- Sortable columns and CSV export on every table.
- Price Craft at NQ when your stats can't reach max quality (needs a crafting simulator).
- Retainer venture advisor: the best venture for each retainer, from the game's venture quantities by retainer stats.
- Best time to list: the hours an item usually sells on your world.
- Back up and restore the data folder, for moving to a new PC.
- Ctrl+K to jump to any item's Market page.
- An accessibility pass with Narrator and keyboard only.
- A winget package.
- Import job levels from the Lodestone (needs a decision: it adds a network source and scrapes pages that can change).
- A Linux build for Steam Deck and XLCore players (needs a decision: toasts, tray and installs are Windows-only today).

## Not planned

- A web build or hosted backend, accounts, cloud sync, telemetry.
- Discord bot, multi-character profiles, cross-DC arbitrage, macOS builds, localized item names.
- Route planning or gil/hour estimates without yield and travel data.
- Gathering success-rate estimates: the game data has no formula for them.
