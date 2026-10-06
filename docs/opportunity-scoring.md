# Opportunity scoring

Gil Sweep answers **what should I farm for gil right now?** It ranks every item your character can gather by an **opportunity score** from 0 to 100 and shows why. This page explains the score without the source code. The code is `src/GilSweep.Core/Sweep/OpportunityScorer.cs` and `OpportunityBoard.cs`; the tests are `tests/GilSweep.Tests/Core/OpportunityScoringTests.cs`.

## What the score is not

The score is not gil per hour. Gil Sweep doesn't know how many items a node gives, how long it takes to fly there, how fast you gather or how the node cycles, so it doesn't estimate any of them. The score compares markets: a higher score means a better market for a gatherer, reachable sooner. The Farm Session is a queue of opportunities, not a route.

## Which items are ranked

The same items v1 put in its farm tables:

- **You can gather it.** Its expansion is no later than your story progress, and your Miner or Botanist level (the higher of the two for items either can get) is at least the item's level.
- **It is gathered at all.** Vendor flips, Free Company submarine loot and retainer venture loot are tracked so they never show up as farms, however well they sell.
- **It sells on your world.** The sales figure must come from your world. When Universalis only has a figure for your data center or region, the item barely trades on your world and isn't a farm.
- **It isn't a map or a crystal.** Maps have their own pick (one per 18 hours: the most valuable map that sells at least twice a day) and crystals pile up on their own.

Legendary nodes need their expansion's folklore book. As in v1, an item you lack the book for is still ranked, with a "Needs the … folklore book" reason.

## The score

**Score = (market + competition + trend) × node availability**, rounded.

### Market: 0–70 points

How much gil changes hands for the item on your world each day: the average sale price × units sold per day (v1 called this throughput and ranked by it alone). The points grow by 12 for every tenfold increase, from 10 gil a day:

| Gil a day | Points |
| ---: | ---: |
| 1,000 | 24 |
| 10,000 | 36 |
| 100,000 | 48 |
| 1,000,000 | 60 |
| 6,800,000 and up | 70 |

On screen the market points are split into **sale velocity** (the part from units sold: 12 points per tenfold, from 1 a day) and **price** (the rest). Because the split is of one curve over throughput, two items with the same other signals always rank in v1's order.

### Competition: 0–20 points

How long the stock already on the market board would last: units listed ÷ units sold per day. The fewer days, the sooner your listings sell.

| Days of stock | Points |
| --- | ---: |
| under 1 | 20 |
| 1 to 3 | 16 |
| 3 to 7 | 11 |
| 7 to 14 | 6 |
| 14 or more | 2 |
| unknown | 11 |

Listings come from the sweep's listings request. The listed units count every listing on your world (Universalis's stack-size histogram), not only the 20 cheapest it returns. Listings last uploaded to Universalis more than 3 days before the sweep are **stale**: shown, but scored as unknown. The words on screen come from the number of listings: up to 10 is "few sellers", up to 35 "moderate", more "many sellers".

### Trend: 0–10 points

The change in average sale price against a snapshot about a week old: the newest one at least 5 days older than the sweep (the weekly digest's rule), or while your history is shorter, the oldest one at least 20 hours older. Hourly sweeps differ too little from each other to show a trend.

| Change | Points |
| --- | ---: |
| +10% or more | 10 |
| +3% to +10% | 8 |
| between −3% and +3% | 6 |
| −3% to −10% | 3 |
| −10% or more | 0 |

Without a baseline, Saddlebag Exchange's trend state stands in: increasing or spiking 8, decreasing or crashing 3, stable 6. With neither, 6, the same as stable.

### Node availability: the multiplier

Regular nodes and open timed nodes count in full. A closed timed node counts for less the longer you'd wait, using the Eorzea clock at the moment the board is built (it is rebuilt as the clock moves):

| Node | Multiplier |
| --- | ---: |
| regular, or open now | 1 |
| opens within 10 real minutes | 0.9 |
| opens within 30 real minutes | 0.75 |
| opens later | 0.55 |

### Grades

| Score | Grade |
| ---: | --- |
| 75 or more | Excellent |
| 62 to 74 | Good |
| 48 to 61 | Fair |
| under 48 | Weak |

## Worked example

From the recorded Cactuar sweep in the test fixtures (2026-10-05), for a level 100 Miner:

**Darksteel Ore**, an unspoiled node (window at 1:00 ET for 3 Eorzea hours): 574 gil average, 1,846.8 sold a day.

- Market: 574 × 1,846.8 = 1,060,063 gil a day → 12 × log₁₀(106,006) = **60.3** (sale velocity 39.2, price 21.1).
- Competition: 14 listings holding 819 units, 819 ÷ 1,846.8 = 0.44 days of stock → **20**.
- Trend: with no week-old snapshot, Saddlebag's state; "stable" → **6**.
- While the node is open: (60.3 + 20 + 6) × 1 = **86, Excellent**. Two hours after it closes, with an hour to wait: × 0.55 = 47, Weak, and it moves to "Soon".

## What the Sweep screen shows

- **Best farm right now**: the highest score among items you can gather this minute (regular nodes and open timed nodes).
- **More opportunities**: every ranked item, best first; closed timed nodes show their countdown.
- **Available now**: open timed nodes, best first, then the two best regular nodes.
- **Soon**: the next three timed nodes to open.
- **Farm Session** (15 min, 30 min, 1 hour, Chill = 3 hours): now, the best open timed node of at least Fair grade (or the best regular node); then up to two (three for an hour or more) Fair-or-better timed nodes that open within the session; then the best regular node as a fallback.

Every ranked item carries its five signals with points and the facts behind them, and short reasons with a glyph (↑ ↓ → ● ○) so nothing depends on color.

## Changes from v1

v1 ranked by throughput alone, in separate Mining and Botany tables of 12. v2 keeps every v1 rule above and changes only these, on purpose:

1. **Listing depth counts.** A crowded market board (many days of stock) lowers an item; an empty or thin one raises it. v1 only looked at listings for its retainer plan.
2. **Price trend counts**, against a week-old snapshot rather than v1's change since the previous sweep. v1's change since the previous sweep still drives movers and price-spike alerts, unchanged.
3. **Node windows count.** v1 showed spawn timers but ranked closed nodes as if they were open.
4. **Items either gatherer can get are ranked** (aetherial reduction, such as Levinchrome Aethersand). v1 left them out because its tables were split by job.
5. **One list** instead of two capped tables.
6. **The bundled seed snapshot isn't used as prices.** First run sweeps instead.

With competition and trend unknown and every node open, v2 ranks in exactly v1's throughput order. `OpportunityScoringTests` checks this for six characters over the recorded sweep and over v1's seed, and checks that v2's top Miner or Botanist item is v1's top farm.
