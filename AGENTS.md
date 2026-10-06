# AGENTS.md

These are the working rules for agents in this repo. Gil Sweep (Apache-2.0) is a Windows desktop app that answers one question for FINAL FANTASY XIV gatherers: **what should I farm for gil right now?** It is .NET 10 + Avalonia 12, a sibling of [XIV Vault](https://github.com/hazeliscoding/xiv-vault).

## Sources of truth

- `README.md`: what Gil Sweep does and how to use it.
- `ROADMAP.md`: decisions already made, the milestones, and what is out of scope. Work from the next unchecked item, tick it off when done, and record new decisions there, dated. Respect existing decisions unless the owner reopens them.
- `docs/opportunity-scoring.md`: how recommendations are ranked. Any change to ranking is listed under "Changes from v1" and covered by a test.
- `docs/migration-v2.md`: what moved from the Electron app (v1) and the cutover gate.
- The desktop UI implements the approved mockup (claude.ai design project "Gil Sweep"). Don't redesign it.

## Architecture (hard rules)

- All behavior lives in `src/GilSweep.Core`. `GilSweep.Desktop` only presents it: no market math, ranking or file formats in view models or views.
- Universalis and Saddlebag Exchange response shapes stay inside `Core/Market/Universalis` and `Core/Market/Saddlebag`. Everything else uses the Core models.
- Market requests go through `MarketHttp`: retries for 429, 5xx and timeouts, a message for the user on failure. A provider that fails part-way leaves a warning on the snapshot, not a failed sweep, unless the tracked items themselves couldn't be priced.
- Ranking keeps v1's rules (`Core/Sweep/Ranking.cs`). They are characterized against v1's own output in `tests/GilSweep.Tests/Fixtures/V1`; a failing characterization test means behavior changed, not that the fixture is stale.
- Never show gil per hour or estimate yield, travel time or gathering speed. The Farm Session is an opportunity queue, not a route.
- Persisted files (settings, snapshots, alert log) use `{ get; set; }` with defaults, not `init`: source-generated System.Text.Json sets a missing init property to its type's default, so older files would lose new defaults. Settings and snapshots keep v1's JSON names.
- Time comes from `TimeProvider` and files from `IAppEnvironment`, so tests run on a fixed clock in temp folders. `GIL_SWEEP_HOME` points a real run at another data folder.
- Desktop view models take interfaces only (`IDialogService`, `IShellService`, `IFilePicker`, `INotifier`, `IAppUpdater`, `ITicker`). No logic in code-behind.
- Avalonia 12 ignores a `RenderTransform` set by a style unless the element already has one, so give it `RenderTransform="none"` (or set it inline).
- Updates never interrupt a sweep and never install at startup. No telemetry, accounts or hosted services; the only network calls are the market APIs and the update check.

## Commands

- `dotnet build` and `dotnet test --project tests/GilSweep.Tests` from the repo root. Tests are offline.
- `dotnet test --project tests/GilSweep.Tests -- --filter-trait "Category=Live" --explicit only` runs one sweep against the live APIs.
- `dotnet format --verify-no-changes` is the CI style check. Run `dotnet format` before committing.
- `dotnet run --project src/GilSweep.Desktop` runs the app. Set `GIL_SWEEP_HOME` to keep your own profile out of it.
- `dotnet run --project tools/GilSweep.Screenshots -- <folder>` renders every screen to PNG from recorded data and a fixed clock. Check UI changes with it.
- `dotnet run scripts/make-icons.cs` rebuilds `docs/brand/gil-sweep.ico`; `scripts/make-lucide-icons.mjs` regenerates the icon geometry (see its header).

## Working style

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/), atomic, with a scope when it helps (`feat(sweep): …`).
- **No AI attribution** in commits or PRs: no `Co-Authored-By` trailers, no "Generated with" lines, no session links.
- **Tests first for Core.** New fixtures are real recorded responses, trimmed to the fields Gil Sweep reads.
- **Docs:** short. Prefer editing `ROADMAP.md` over new planning documents.
- **Code comments** explain why, not what. No boilerplate XML docs, no commented-out code.
- **UI copy:** calm, short, declarative. No exclamation marks or emoji. Numbers carry units; changes are signed; color never carries meaning alone (pair it with an arrow, a dot shape or a word).
