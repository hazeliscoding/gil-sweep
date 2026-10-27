# Roadmap

Gil Sweep answers one question: **what should I farm for gil right now?** v1 (Electron + Angular, `v1.0.0`) shipped everything in `CHANGELOG.md`. v2 is a native rewrite; the plan and the inventory of what moves where are in `docs/migration-v2.md`. This file holds the decisions already made, what shipped, the releases still to build and what's out of scope.

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
- **2026-10-08** — Audited against Dalamud plugins and out-of-game tools (Teamcraft, Universalis, Saddlebag Exchange, Garland Bell). Every shipped feature stays: Gil Sweep is the option without client mods, with alerts outside the game and a gatherer score built on days of stock, node windows and gating. Undercut alerts overlap Saddlebag Exchange and Allagan Market the most, so they get no further work. Ideas another tool already covers are Not planned.
- **2026-10-08** — Reuse before building: the wider catalog's nodes and spawn windows come from Teamcraft's open data (MIT); any crafting solver comes from Raphael.Net or Craftimizer's solver (both MIT). The Universalis client stays our own: the official `Universalis.Client` targets .NET 8 with Newtonsoft.Json and would replace tested, source-generated code for nothing.
- **2026-10-11** — The next release, v2.0.1, is accessibility and UI polish only: keyboard, Narrator, contrast and fidelity to the approved mockup. No new features; Gear stats follows it. *Superseded 2026-10-20: this work moves to v2.0.2.*
- **2026-10-11** — Craft separates solo recipes from Free Company workshop projects. Not everyone is in an FC with a workshop, so a workshop project only counts when Settings says you have access; otherwise it shows locked.
- **2026-10-11** — Releases are signed with Azure Trusted Signing, the owner's existing account, instead of SignPath Foundation.
- **2026-10-20** — The release workflow signs in to Artifact Signing through OIDC from a `release` environment that only `v*` tags can deploy to, so no secret is stored and manual runs or forks build unsigned. Signing lands with v2.0.1, ahead of its v2.1 item, which stays open until a signed tag meets its "Done when".
- **2026-10-20** — `main` takes changes only by rebase-merged pull request with CI green, and release tags can't be moved or deleted, as in Prompuff. This gives up pushing small doc commits straight to `main`.
- **2026-10-20** — v2.0.1 is 2.0.0 rebuilt and signed, with nothing else changed, so the first signed release and the self-update test v2.0's Release item waits on don't wait for the accessibility work. Accessibility and polish move to v2.0.2. This gives up a first point release users can see a difference in.
- **2026-10-20** — Small, self-contained tasks that can be checked offline become GitHub issues labelled `good first issue`, and the project is listed on Up For Grabs. Planning stays in this file; issues are for contributors and bug reports. Visual UI work stays off that list, because the approved mockup isn't public. *Superseded in part 2026-10-21: planning moves to GitHub issues too.*
- **2026-10-21** — A one-page website in `site/`, on GitHub Pages at hazeliscoding.github.io/gil-sweep, laid out like XIV Vault's: one column of short answers to the questions a gatherer has, in the app's tokens and IBM Plex. The README's screenshots are copied in at deploy time (`scripts/stage-site.sh`), so the site never shows older pictures than the README. Plain HTML and CSS with no script, nothing loaded from another site, and no analytics. It is a launch page, not the web build ruled out under Not planned. This gives up a user guide page and a cropped social preview (the Sweep screenshot stands in), which would need a second page to maintain and ffmpeg in the workflow.
- **2026-10-21** — Open work moves to GitHub after 2.0, as in Prompuff (the owner's choice), so people using Gil Sweep can see what's planned and comment on it where they report bugs. Each release is a milestone whose description says what it brings and when it's done, each checklist item is an issue, and the public [Gil Sweep project](https://github.com/users/hazeliscoding/projects/11) shows them all. This file keeps the decisions, what shipped and what's not planned; the checklists as they stood are in `git show v2.0.1:ROADMAP.md`. v2.0.2 to v2.2 became #17 to #31, one issue per item plus one to cut each release, so v2.2 gains a release item and v2.1's now updates from v2.0.2. Later became #32 to #40, labelled `later`, each saying what has to be settled first. v2.1's Code signing item has no issue, because it shipped with v2.0.1. This gives up changing the plan in the same pull request as the code, and agents read it through `gh` instead of from this file. *Superseded 2026-10-27: open work is back in this file.*
- **2026-10-24** — Releases are signed in a job of their own. The build job runs the tests and the build, and every package they load, so it can't get a token; the package job takes the built files and only packs and signs them with vpk. A compromised package could otherwise take the token and sign files of its own with the owner's certificate, unseen in any release. A package that changes the build is still signed with it. This gives up a single job: a release starts a second Windows runner and passes the build between them.
- **2026-10-27** — Open work moves back into this file (the owner's choice), so the plan changes in the same pull request as the code again and agents read it here instead of through `gh`. v2.0.2 to v2.2 (#17 to #31) and the `later` ideas (#32 to #40 and #42) are folded into the sections below and closed as not planned, each with a comment pointing here. The three milestones and the [Gil Sweep project](https://github.com/users/hazeliscoding/projects/11) are closed, not deleted. Bug reports and `good first issue` issues stay on GitHub, as the 2026-10-20 decision had it. This gives up a public board where people using Gil Sweep could follow the plan and comment on it.

## Shipped

Each release's notes are in `CHANGELOG.md`, v1's included, and the decisions above say why.

| Version | Released | What it brought |
|---|---|---|
| [2.0.1: Signed build](https://github.com/hazeliscoding/gil-sweep/releases/tag/v2.0.1) | 2026-10-20 | 2.0.0 rebuilt and code-signed, and the first update in place from Setup |
| [2.0: Native rewrite](https://github.com/hazeliscoding/gil-sweep/releases/tag/v2.0.0) | 2026-10-06 | .NET 10 and Avalonia 12 in place of Electron: the six screens, notifications, Setup with in-app updates, and v1's data imported |

## v2.0.2 — Accessibility and polish

Keyboard, Narrator, contrast and fidelity to the approved mockup, with no new features.

- [x] **Signing in its own job** — the release workflow signs in a job of its own, so no package the build loads can reach the signing token (2026-10-24).
- [ ] **Keyboard** — every screen and first run work without a mouse: a visible focus ring on every control, a tab order that follows the layout, Escape closes dialogs and the Farm Session, and nothing traps focus. Escape ending the Farm Session is [#7](https://github.com/hazeliscoding/gil-sweep/issues/7), a `good first issue` kept for contributors.
  Done when: each of the six screens and first run can be completed with the keyboard alone.
- [ ] **Narrator** — every control and row has a name Narrator reads in full, with units and signed changes ("Dense Aluminum Ore, 12,400 gil, up 8%"); screens and cards have headings; sweep progress and alerts are announced.
  Done when: a Narrator pass on each screen finds no unnamed control and no number read without its unit.
- [ ] **Contrast and scaling** — text meets WCAG AA (4.5:1, 3:1 for large text, icons and focus rings) on every surface it sits on, and layouts hold from 100% to 200% display scaling.
  Done when: a test checks every text and background token pair, and the screenshots at 200% show no clipped or overlapping text.
- [ ] **Polish** — every screen checked against the approved mockup (the claude.ai design project "Gil Sweep", which isn't public) and drift fixed: spacing, alignment, truncation, empty, loading and error states. No redesign.
  Done when: the screenshots match the mockup, including a worst-case set (longest item name, nine-digit prices, no results).
- [ ] **Release** — tag `v2.0.2` and publish. The owner cuts it, as `CONTRIBUTING.md` describes.
  Done when: v2.0.1 installed from Setup updates itself to v2.0.2.

**Done when:** each of the six screens and first run can be completed with the keyboard alone and with Narrator, every text and background token pair meets WCAG AA, the screenshots at 100% and 200% match the mockup with no clipped text, and v2.0.1 installed from Setup updates itself to v2.0.2.

## v2.1 — Gear stats

Locks for the items and recipes your Perception, Craftsmanship and Control can't reach yet, using the game's own minimums, and diagnostics to copy into a bug report.

- [ ] **Requirement data** — `items.json` gains each item's minimum Perception and `crafts.json` each recipe's minimum Craftsmanship and Control, from XIVAPI's `GatheringItem` and `Recipe` sheets; a script in `scripts/` regenerates them after a patch. Items added in Settings get their Perception the same way. 77 bundled recipes don't match XIVAPI by result item and need their recipe rows found.
  Done when: tests read Dense Aluminum Ore's 5,090 Perception and Gold Ingot's 391 Craftsmanship and 374 Control from recorded XIVAPI responses.
- [ ] **Gear locks in Core** — optional Miner and Botanist Perception and crafter Craftsmanship and Control in settings. An item below its Perception minimum locks ("Needs 5,090 Perception (you have 4,800)"); a recipe below either minimum locks the same way. A blank stat isn't checked. Listed under "Changes from v1" in `docs/opportunity-scoring.md`.
  Done when: with blank stats every v1 characterization test passes unchanged, and lock tests cover below, at and blank.
- [ ] **Settings and screens** — the stat fields in the Character and Crafters cards, in the mockup's existing style; locked rows and craft cards show the shortfall. First run doesn't ask for stats.
  Done when: the screenshots include a Perception lock and a Craftsmanship lock.
- [ ] **Copy diagnostics** — a Settings button copies the version, Windows version, recent errors and the log folder for a bug report. Nothing is sent anywhere.
  Done when: the copied text names the data folder as `%AppData%\GilSweep` and contains no username or other private path.
- [ ] **Release** — tag `v2.1.0` and publish. The owner cuts it.
  Done when: v2.0.2 installed from Setup updates itself to v2.1.0.

**Done when:** with blank stats every v1 characterization test passes unchanged, the screenshots show a Perception lock and a Craftsmanship lock, and v2.0.2 installed from Setup updates itself to v2.1.0.

## v2.2 — Better answers

Every gatherable the character can reach, Not interested, prices after tax, and workshop crafts.

- [ ] **Wider catalog** — a weekly scan prices every gatherable item the character can reach, not only the 104 tracked items; the ones that trade on the world join the ranking. Nodes, spawn windows, levels and stars come from Teamcraft's open data (2026-10-08), bundled and regenerated by a script after a patch. The tracked list stays as the base, along with its trap items.
  Done when: on recorded data, a scan ranks a gatherable missing from `items.json`, with its node and any spawn window, within Universalis's request limits.
- [ ] **Not interested** — hide an item from recommendations from its card; it stays on Market and can be restored in Settings.
  Done when: a hidden item leaves Sweep and the Farm Session, and returns when restored.
- [ ] **Prices after tax** — sale proceeds and craft margins subtract the market tax of the retainers' city (Universalis tax rates). The rates arrive undated from players' uploads, so Market names the city and rate used. Listed under "Changes from v1".
  Done when: margins on recorded data are net of the recorded tax rate, and the scoring doc's worked example matches.
- [ ] **Workshop crafts** — Craft lists Free Company workshop projects (submersible and airship parts, other company crafts) apart from solo recipes, from XIVAPI's `CompanyCraftSequence` sheet, regenerated by the requirement data's script. A Settings switch, off by default, says you have workshop access; without it they show locked ("Needs an FC workshop"), never as the best recipe.
  Done when: on recorded data, a material whose best use is a workshop project shows it locked with the switch off and as its best recipe with it on, and with the switch off Craft matches today.
- [ ] **Release** — tag `v2.2.0` and publish. The owner cuts it.
  Done when: v2.1.0 installed from Setup updates itself to v2.2.0.

**Done when:** on recorded data, a scan ranks a gatherable missing from `items.json`, a hidden item leaves Sweep and the Farm Session, craft margins are net of the recorded tax rate, a workshop project shows locked without workshop access, and v2.1.0 installed from Setup updates itself to v2.2.0.

## Later

Ideas without a release. Each gets one once what it lists is settled in a dated decision above.

- **In-game item icons beside names.** Settle first: where they come from (fetching them from XIVAPI while the app runs adds a network call beyond the market APIs and the update check; bundling them adds to the install and needs a script after each patch), whether Square Enix's game assets can ship in an Apache-2.0 repo at all, and where they sit in the approved mockup without a redesign.
- **Gil per scrip for the scrip vendors**, ranking what they sell by the gil it brings per scrip spent. Settle first: which of the six screens it joins (2026-10-05), where the vendor lists and scrip costs come from and the script that regenerates them, and that it covers spending only, since what a turn-in earns depends on collectability, which Gil Sweep can't know without estimating.
- **Sortable columns and CSV export on every table.** Settle first: which tables, whether a sort is remembered between runs (a new setting, with a default for older settings files), and how CSV fits the export already in the screens (2026-10-05), with its number formatting and where the units go.
- **Price Craft at NQ when your stats can't reach max quality**, using Raphael.Net or Craftimizer's solver rather than our own (2026-10-08). Settle first: CP and HQ, since a solver needs CP and the 2026-10-06 decision rules out GP, CP and HQ checks; whether a solver's answer is an estimate, which Gil Sweep never makes; and which solver, both MIT. Needs v2.1's crafter stats.
- **Back up and restore the data folder**, for moving to a new PC. Settle first: what a backup holds (settings, snapshots and the alert log, and whether up to 90 days of history belongs in it), and what a restore does to the running app and to a backup from a newer version.
- **Ctrl+K to jump to any item's Market page.** Settle first: whether it searches the tracked items or v2.2's wider catalog too, and its place in the approved mockup, since the UI isn't redesigned in code.
- **A winget package.** Setup is signed since v2.0.1. Settle first: who updates the install, since Velopack updates Gil Sweep in place and a winget upgrade must not fight it, and an installer entry whose type and silent switches match Velopack's per-user Setup.
- **Import job levels from the Lodestone** instead of typing them in Settings. Settle first: a new network source, when the only calls today are the market APIs and the update check, and scraping, since the Lodestone has no API and its pages can change without notice.
- **A Linux build for Steam Deck and XLCore players.** Settle first: what's Windows-only today (toasts register an AppUserModelID under HKCU, so the Desktop project targets the Windows 10 SDK, and the tray and Velopack's Setup are Windows builds), and whether it's checked on CI only or by hand too.
- **Copy the Farm Session for GatherBuddy**: a button that copies the session's items as a [GatherBuddy](https://github.com/Ottermandias/GatherBuddy) Gather Window preset to import in game. Clipboard only: no plugin, no game client, no network. The format is GatherBuddy's [`GatherWindowPreset.Config`](https://github.com/Ottermandias/GatherBuddy/blob/main/GatherBuddy/GatherHelper/GatherWindowPreset.cs): version byte `1`, then JSON, gzipped and Base64-encoded. Settle first: whether naming a Dalamud plugin in the UI fits the 2026-10-08 decision that Gil Sweep is the option without client mods, and where the button and its confirmation go in the approved mockup. It reuses Copy diagnostics' clipboard. Done when a Core encoder is tested against a preset string recorded from GatherBuddy, items GatherBuddy can't import (such as aetherial reduction results) are left out and named in the confirmation, and one copied session imports in game with the expected items.

Issues labelled [`good first issue`](https://github.com/hazeliscoding/gil-sweep/labels/good%20first%20issue) are small tasks kept for contributors.

## Not planned

- A web build or hosted backend, accounts, cloud sync, telemetry.
- Discord bot, multi-character profiles, cross-DC arbitrage, macOS builds, localized item names.
- Route planning or gil/hour estimates without yield and travel data.
- Gathering success-rate estimates: the game data has no formula for them.
- Discord webhooks: Universalis alerts already post price alerts to Discord per item and world, without Gil Sweep running.
- A live Universalis websocket watch: Saddlebag Exchange and the Allagan Market plugin already alert on undercuts within minutes.
- A retainer venture advisor: Teamcraft's Retainer Ventures page ranks ventures by retainer stats and price.
