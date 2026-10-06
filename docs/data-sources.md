# Data sources

## Bundled with the app

`src/GilSweep.Core/Data`, carried over unchanged from v1 and embedded in the assembly:

| File | What |
| --- | --- |
| `items.json` | 104 tracked items: name, id, job, level, node location, kind, expansion, and for 18 timed nodes their Eorzea spawn hours and window length. Every node was checked against Garland Tools; window lengths come from Teamcraft's open data. Trap items (vendor flips, Free Company submarine loot, retainer ventures) are listed so they are never recommended. |
| `crafts.json` | 635 recipes that use tracked items or their products: job, level, yield, ingredients. |
| `garland-demand.json` | For 76 items, who uses them: recipes with quantities, leves, Grand Company supply missions, quests. Drives "who buys it". |

Items added in Settings go to `custom-items.json` in the data folder after the same check (see Garland Tools below).

## Universalis

[Universalis](https://universalis.app/) is the community market board API. Gil Sweep sends `User-Agent: GilSweep/<version> (+https://github.com/hazeliscoding/gil-sweep)`.

| Endpoint | Used for | Batch |
| --- | --- | --- |
| `GET /api/v2/aggregated/{world}/{ids}` | Prices and velocities for every tracked item, demand consumer and recipe ingredient: cheapest listing, average sale price and units sold per day, for normal and high quality, each by world, data center and region | 100 ids (13 requests a sweep) |
| `GET /api/v2/{world}/{ids}?listings=20&entries=0` | Listing depth for farmable and watched items, and your retainers' listings for undercuts | 20 ids (5 requests); larger batches time out with 504 |
| `GET /api/v2/{world}/{id}?listings=30&entries=100` | The Market screen's live listings and recent sales | one item, on demand |
| `GET /api/v2/history/{world}/{ids}?entriesToReturn=…` | The Market chart (14 days) and the one-time history backfill | 25 ids |
| `GET /api/v2/worlds` | The world picker | once a launch |

How the figures are read:

- **Scope.** The world's figure is used when it exists, then the data center's, then the region's, and the answering scope is recorded. Only world-scope sales count for farming: a figure that only exists for the data center means the item barely trades on your world.
- **Rounding.** Prices round to whole gil and velocities to one decimal, exactly as v1 did, so v1 and v2 snapshots agree.
- **Listing depth.** `listingsCount` and `unitsForSale` only count the listings a request returns, so Gil Sweep counts listings and units from `stackSizeHistogram`, which covers every listing on the world.
- **Staleness.** Listings last uploaded more than three days before the sweep are shown but scored as unknown.

## Saddlebag Exchange

`POST https://api.saddlebagexchange.com/api/ffxivmarketshare` (one request a sweep, optional): the week's top sellers in ores, logs and reagents with a trend state (spiking, increasing, stable, decreasing, crashing, out of stock). It supplies the trend before you have a week of history, and lists top sellers that aren't tracked. Turn it off in Settings; a failure only leaves a warning.

## Garland Tools and XIVAPI

Only when you add an item in Settings:

1. `GET https://v2.xivapi.com/api/search?sheets=Item&query=Name="…"` finds the item (then `Name~"…"` for a partial match).
2. `GET https://garlandtools.org/db/doc/item/en/3/{id}.json` says whether the item itself has gathering nodes. Node partials alone can belong to a crafted item's ingredients, so only the item's own node list counts. Nodes bring level, job, location, kind and spawn hours.
3. `GET https://garlandtools.org/db/doc/core/en/3/data.json` names the zone, once a session.

Crafted items are left to the Craft screen. Items with no nodes and no recipe are tracked as never-farm traps (vendor, or likely submarine loot).

## Failures

Every request has a 30-second limit. Universalis, XIVAPI and Garland Tools get up to three tries: rate limits (429, honoring `Retry-After`), gateway errors and timeouts are retried with backoff. Saddlebag Exchange gets one, since it is optional. A sweep fails only when the tracked items can't be priced; then the last sweep stays on screen with a banner and Gil Sweep tries again in five minutes. Responses that can't be read become a message, not a crash.

## Recorded responses for tests

`tests/GilSweep.Tests/Fixtures` holds real responses for Cactuar recorded on 2026-10-05, trimmed to the fields Gil Sweep reads, and what v1 produced from them. See its README.
