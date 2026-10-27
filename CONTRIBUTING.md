# Contributing to Gil Sweep

Thanks for helping. Gil Sweep answers one question for FINAL FANTASY XIV gatherers: what should I farm for gil right now? It stays small on purpose, so this page is short.

## Where to start

- Issues labelled [good first issue](https://github.com/hazeliscoding/gil-sweep/labels/good%20first%20issue) are small, self-contained and checkable offline. Comment on one before you start, so two people don't take the same one.
- [README.md](README.md) says what Gil Sweep does. [ROADMAP.md](ROADMAP.md) holds the decisions already made, what shipped, the planned releases and what's not planned. Check it before proposing a feature: decisions stand unless the owner reopens them.
- [docs/architecture.md](docs/architecture.md) shows how the pieces fit, and [docs/opportunity-scoring.md](docs/opportunity-scoring.md) explains how recommendations are ranked.
- For anything bigger than a fix, open an issue first, so nobody builds something that's already decided against.

## Build, run and test

You need Windows 10 or 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download). `global.json` asks for 10.0.300 or a later feature band.

```powershell
dotnet build
dotnet test --project tests/GilSweep.Tests
dotnet format --verify-no-changes
```

The tests are offline. They answer from recorded Universalis and Saddlebag Exchange responses and run on a fixed clock in temp folders.

Run the app against a scratch data folder, so development never touches your own profile in `%AppData%\GilSweep`:

```powershell
$env:GIL_SWEEP_HOME = "$env:TEMP\gil-sweep-dev"
dotnet run --project src/GilSweep.Desktop
```

After a UI change, render every screen to PNG from recorded data and look at them:

```powershell
dotnet run --project tools/GilSweep.Screenshots -- artifacts/screenshots
```

Attach before and after screenshots to the pull request.

## Project layout

| Project | What it holds |
|---|---|
| `src/GilSweep.Core` | All behavior: the item catalog, market clients, sweeps and ranking, Eorzea time, alerts, history and settings. No Avalonia. |
| `src/GilSweep.Desktop` | Avalonia views and view models that present Core, and the Windows services: toasts, tray and updates. |
| `tests/GilSweep.Tests` | xUnit v3 tests for both, with recorded responses in `Fixtures/`. |
| `tools/GilSweep.Screenshots` | Renders every screen headlessly for review. |

Package versions live in `Directory.Packages.props`.

## Hard rules

These come from [AGENTS.md](AGENTS.md), and reviews hold every change to them.

- All behavior lives in Core. Views and view models only present it: no market math, ranking or file formats there, and no logic in code-behind.
- Universalis and Saddlebag Exchange response shapes stay inside `Core/Market/Universalis` and `Core/Market/Saddlebag`, and market requests go through `MarketHttp`.
- Ranking keeps v1's rules. The characterization tests against `tests/GilSweep.Tests/Fixtures/V1` pin v1's output, so when one fails, behavior changed: say so in the pull request instead of updating the fixture. A ranking change is listed under "Changes from v1" in `docs/opportunity-scoring.md` and covered by a test.
- Never show gil per hour or estimate yield, travel time or gathering speed.
- Time comes from `TimeProvider` and files from `IAppEnvironment`.
- Saved settings, snapshots and the alert log use `{ get; set; }` with defaults, not `init`, and keep v1's JSON names.
- No telemetry, accounts or hosted services.
- Tests come first for Core. New fixtures are real recorded responses, trimmed to the fields Gil Sweep reads.

## UI and copy

- The screens follow an approved design that isn't public. Match the existing screens' spacing, type and colors and don't redesign them; the screenshots in your pull request are how a change gets reviewed.
- Copy is calm, short and declarative, with no exclamation marks or emoji. Numbers carry units, changes are signed, and color never carries meaning alone: pair it with an arrow, a dot shape or a word.
- Avalonia 12 ignores a `RenderTransform` set by a style unless the element already has one, so give it `RenderTransform="none"` or set it inline.

## Code style

Match the code around you. `.editorconfig` sets the style, `.gitattributes` keeps LF line endings, and CI runs `dotnet format --verify-no-changes`. Comments explain why, not what: no boilerplate XML docs and no commented-out code.

## Commits and pull requests

- Use [Conventional Commits](https://www.conventionalcommits.org/), with a scope when it helps, such as `fix(formatting): …`. Keep each commit to one logical change.
- Commit messages read like a person wrote them: no `Co-Authored-By` trailers or "Generated with" lines for AI tools.
- Work on a branch and open a pull request into `main`, which takes no direct pushes. It merges once CI's `build` check passes with the branch up to date with `main`, and it lands by rebase, so your commits end up on `main` as you wrote them.
- Bugs and requests go in [GitHub Issues](https://github.com/hazeliscoding/gil-sweep/issues). Security problems don't: see [SECURITY.md](SECURITY.md).

[CI](.github/workflows/ci.yml) builds, tests, checks formatting and renders the screenshots on every pull request, and uploads them as an artifact.

## How releases are cut

The owner cuts releases. A pull request adds the version's entry to `CHANGELOG.md`, and a `v*` tag on the merged commit starts [the release workflow](.github/workflows/release.yml). It takes the version from the tag, signs the Windows build and opens a draft GitHub Release with `SHA256SUMS`, which the owner checks and publishes.
