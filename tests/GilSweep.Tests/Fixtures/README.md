# Test fixtures

Every test runs offline against these files. Nothing here is generated at test time.

## Universalis/ and Saddlebag/

Real responses for **Cactuar**, recorded on 2026-10-05 by `legacy/characterize/capture.py` and trimmed by `trim.py` to the fields Gil Sweep reads. The shapes are the APIs' own.

| File | What it is |
| --- | --- |
| `aggregated-cactuar.json` | Aggregated prices for all 1,283 ids a sweep asks for (1,240 answered; the rest are unmarketable) |
| `current-cactuar.json` | Current listings (20) and recent sales (20) for the 104 tracked items |
| `history-cactuar.json` | Up to 300 sales per tracked item |
| `worlds.json`, `data-centers.json` | World and data center lists |
| `normal-market.json` | Mythrite Sand: 100 listings, ~110 sold a day |
| `sparse-market.json` | Windtea Leaves: one listing |
| `stale-market.json` | Bamboo Shoot: listings last uploaded six days before the recording |
| `no-listings.json` | Hard Water: nothing listed |
| `high-volume.json` | Lightning Cluster: thousands sold a day |
| `Saddlebag/marketshare-cactuar.json` | Saddlebag Exchange market share, 200 rows |
| `Saddlebag/rising.json`, `falling.json`, `stable.json` | Rows from that response grouped by trend state |

## Verify/

Real XIVAPI search and Garland Tools item responses for the verify-and-track tests: Zinc Ore and Cloud Mica (gatherable), Raw Imperial Jade (timed), Mythrite Ingot (crafted, with ingredient nodes only), Distilled Water and Imperial Jade (crafted), Grade 8 Dark Matter (vendor), Darksteel Ore (already tracked), and Garland's zone names for those nodes.

## V1/

What the v1 (Electron/Angular) code produced from the responses above. `legacy/characterize/harness.ts` runs the v1 TypeScript unchanged under Node with `fetch` answering from these files, and writes:

| File | v1 code it captures |
| --- | --- |
| `expected-sweep.json` | `SweepService.run`: rows, craft margins, untracked top sellers, request chunks. Previous snapshot: `seed-snapshot.json` |
| `expected-ranking.json` | Every function in `ranking.ts` (and the dashboard's top farm and the Crafting page list) for six characters, over the sweep and over the seed |
| `expected-eorzea.json` | `eorzea.ts` (renderer and main process) at 187 moments, including each window's opening and closing millisecond |
| `expected-market.json` | `marketDetail`, the drill-down's selling hours (UTC) and `retainerPlan` for all 104 items |
| `expected-history.json` | `digest`, `history`, `backfill` and `pruneSnapshots` over `archive/` |
| `expected-verify.json` | `verifyItem` over `Verify/` (from `legacy/characterize/verify.ts`) |
| `archive/` | The snapshot archive the history outputs were computed from |
| `seed-snapshot.json` | v1's bundled first-boot snapshot (a real Cactuar sweep, 2026-07-19) |

To regenerate after changing a fixture, see the comment at the top of `legacy/characterize/harness.ts`. The harness and the v1 source are removed at cutover; tag `electron-final` keeps them.
