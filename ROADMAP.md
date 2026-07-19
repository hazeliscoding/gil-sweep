# Roadmap

## v0.6.0 — Alerts — shipped ✅
- Watch (star) items in any table; Windows notification when a watched node's window opens, with time remaining.
- Price-spike notification for watched items after a sweep (≥25% swing).

## v0.7.0 — Trends & weekly digest — shipped ✅
- Trends page: price/velocity charts per item from the local snapshot archive (the drill-down panel's big sibling).
- "This week" digest: movers with context, new watchlist entrants, prune suggestions for farms below ~5 sold/day for 3+ consecutive sweeps.
- Snapshot housekeeping in Settings (view, prune, export).

## v0.8.0 — Crafting depth — shipped ✅
- HQ-aware margins: gear/consumables price at HQ, materials stay NQ (gathered mats have no HQ since 6.0).
- One level of min(buy, craft) costing for intermediate ingredients (nugget → ingot chains).
- Crafter job/level config gating the Crafting page.

## v0.9.0 — Bring your own item DB — shipped ✅
- In-app "track new item": name search → node verification against Garland Tools → crafted/vendor/submarine trap detection, so top sellers that aren't gatherable never get recommended.
- First-run onboarding: pick world and levels instead of inheriting defaults.

## v1.0.0 — Distribution — shipped ✅
- NSIS installer with auto-update alongside the portable exe.
- **Shipping unsigned** — the SmartScreen warning stays documented in the README instead of paying for code signing.
- End-to-end suite in CI: the packaged app driven headlessly (launch, sweep, every page), not just compiled.
- README screenshots; config/snapshot schema migration safety.

## v1.1.0 — Discord webhooks
- Price-spike alerts and the weekly digest posted to a Discord webhook (reaches your phone — the alerts that matter while away from the PC). Node-window alerts stay desktop toasts by default, Discord delivery opt-in (a ~6-minute window isn't actionable from a phone).
- Webhook-only, no bot: a single POST, zero hosting, per-alert-type toggles in Settings. Honest limitation: the app must be running to send.
- Doubles as the notification story for the future web build, which can't do OS toasts.

## v1.2.0 — Visual & input polish
- In-game item icons (~22px) beside names in every table — McMaster tables have product thumbnails; this is ours. Icon ids ride the existing Garland data pipeline; lazy-loaded from Garland's CDN (consider bundling tracked-item icons later for offline).
- Crafter-level slider on the Crafting page toolbar: one slider drags all eight jobs (instant re-rank); per-job fine-tuning stays in Settings for split-level characters.
- Sortable table columns (click a header, re-sort) and CSV export of any table.

## v1.3.0 — Your market (undercut watch)
- Retainer names in config → the app spots *your* listings in the Universalis data it already fetches (listings carry retainer names) and alerts when you've been undercut, with the new floor and your delta. No game integration, pure public data.
- Live watch on starred items via the Universalis websocket (or periodic re-checks): price-crash and undercut alerts in near-real-time instead of sweep cadence. Feeds toasts and Discord alike.

## v1.4.0 — Scrip economy
- Gil-per-scrip page: everything the purple/orange scrip vendors sell (gatherer and crafter), priced live on your world, ranked by gil per scrip — the "what do I spend my weekly scrips on" answer. Needs a curated scrip-item list in the data pipeline (Sphalerite was the prototype).

## v2.0.0 — Hybrid delivery: desktop app + live website
- The renderer already talks to the world through one interface (`window.api`), so a web build swaps that adapter for direct Universalis calls (their CORS is open) plus a thin proxy for the endpoints that need one (Saddlebag). Same Angular app, two targets; the desktop keeps the tray, notifications, and local snapshot archive as its edge.

## Parked (until there's data or demand)
- Discord **bot** (slash commands, price queries from chat) — hosting + token surface; webhooks cover the need for now.
- Multi-character profiles (per-character levels/worlds) — plumbing without a proven need; revisit when alts show up.
- Gil/hour route planner — needs yield-per-node data no API provides.
- Cross-DC arbitrage; macOS builds; localized item names (DE/FR/JP).
- Bundled item icons for full offline rendering (if the CDN hotlinking ever chafes).
