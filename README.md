# gil-sweep

A local desktop app (Electron) that answers one question: **what should you farm for gil this week in FFXIV**, given your gatherer levels and MSQ progress.

Pulls live Universalis price/velocity aggregates and Saddlebag Exchange trends for a curated database of gatherable items, explains *why* each item sells (recipe consumers, leve turn-ins, GC supply — from Garland Tools data), and re-ranks instantly as you drag your MIN/BTN level and expansion sliders.

## Run it (dev)

```
npm run setup   # once: installs desktop + renderer deps (Node 20+)
npm run dev     # Angular dev server + Electron window
```

## Package (portable Windows exe)

```
npm run package:win   # -> desktop/release/gil-sweep-<version>-portable.exe
```

## Architecture

```
Angular renderer  --IPC-->  Electron main  -->  Universalis / Saddlebag (live prices)
   (sliders re-rank locally)            \-->  JSON snapshots (userData) + bundled item DB
```

- `desktop/src/main/` — Electron main: sweep engine, market API clients, snapshot persistence.
- `desktop/renderer/` — Angular 18 standalone renderer; all ranking/filtering happens client-side over the latest snapshot, so slider changes are instant (no refetch).
- `desktop/data/` — curated item DB and demand signals (recipe consumers, leves, GC supply) shipped with the app.

Snapshots and config live in the OS user-data folder; nothing leaves your machine except the market API calls.
