# Roadmap

Gil Sweep answers one question: **what should I farm for gil right now?** v1 (Electron + Angular, `v1.0.0`) shipped everything in `CHANGELOG.md`. v2 is a native rewrite; the plan and the inventory of what moves where are in `docs/migration-v2.md`. This file holds the decisions already made, what shipped and what's out of scope. Open work is in GitHub issues; **Next** says where.

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
- **2026-10-21** — Open work moves to GitHub after 2.0, as in Prompuff (the owner's choice), so people using Gil Sweep can see what's planned and comment on it where they report bugs. Each release is a milestone whose description says what it brings and when it's done, each checklist item is an issue, and the public [Gil Sweep project](https://github.com/users/hazeliscoding/projects/11) shows them all. This file keeps the decisions, what shipped and what's not planned; the checklists as they stood are in `git show v2.0.1:ROADMAP.md`. v2.0.2 to v2.2 became #17 to #31, one issue per item plus one to cut each release, so v2.2 gains a release item and v2.1's now updates from v2.0.2. Later became #32 to #40, labelled `later`, each saying what has to be settled first. v2.1's Code signing item has no issue, because it shipped with v2.0.1. This gives up changing the plan in the same pull request as the code, and agents read it through `gh` instead of from this file.
- **2026-10-24** — Releases are signed in a job of their own. The build job runs the tests and the build, and every package they load, so it can't get a token; the package job takes the built files and only packs and signs them with vpk. A compromised package could otherwise take the token and sign files of its own with the owner's certificate, unseen in any release. A package that changes the build is still signed with it. This gives up a single job: a release starts a second Windows runner and passes the build between them.

## Shipped

Each release's notes are in `CHANGELOG.md`, v1's included, and the decisions above say why.

| Version | Released | What it brought |
|---|---|---|
| [2.0.1: Signed build](https://github.com/hazeliscoding/gil-sweep/releases/tag/v2.0.1) | 2026-10-20 | 2.0.0 rebuilt and code-signed, and the first update in place from Setup |
| [2.0: Native rewrite](https://github.com/hazeliscoding/gil-sweep/releases/tag/v2.0.0) | 2026-10-06 | .NET 10 and Avalonia 12 in place of Electron: the six screens, notifications, Setup with in-app updates, and v1's data imported |

## Next

Open work is in [GitHub issues](https://github.com/hazeliscoding/gil-sweep/issues), and the [Gil Sweep project](https://github.com/users/hazeliscoding/projects/11) shows it on one board. The current milestone is [v2.0.2](https://github.com/hazeliscoding/gil-sweep/milestone/1). The milestones come in this order:

| Milestone | What it brings |
|---|---|
| [v2.0.2: Accessibility and polish](https://github.com/hazeliscoding/gil-sweep/milestone/1) | Keyboard, Narrator, contrast and fidelity to the approved mockup, with no new features |
| [v2.1: Gear stats](https://github.com/hazeliscoding/gil-sweep/milestone/2) | Locks for the items and recipes your Perception, Craftsmanship and Control can't reach yet, and diagnostics to copy into a bug report |
| [v2.2: Better answers](https://github.com/hazeliscoding/gil-sweep/milestone/3) | Every gatherable the character can reach, Not interested, prices after tax, and workshop crafts |

Issues labelled [`later`](https://github.com/hazeliscoding/gil-sweep/labels/later) are ideas without a milestone, and each one says what has to be settled first. Issues labelled [`good first issue`](https://github.com/hazeliscoding/gil-sweep/labels/good%20first%20issue) are kept for contributors.

## Not planned

- A web build or hosted backend, accounts, cloud sync, telemetry.
- Discord bot, multi-character profiles, cross-DC arbitrage, macOS builds, localized item names.
- Route planning or gil/hour estimates without yield and travel data.
- Gathering success-rate estimates: the game data has no formula for them.
- Discord webhooks: Universalis alerts already post price alerts to Discord per item and world, without Gil Sweep running.
- A live Universalis websocket watch: Saddlebag Exchange and the Allagan Market plugin already alert on undercuts within minutes.
- A retainer venture advisor: Teamcraft's Retainer Ventures page ranks ventures by retainer stats and price.
