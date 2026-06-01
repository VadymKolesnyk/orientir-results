# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Live results publisher for orientation (orienteering) races. Reads legacy "Ориентир" DBF files, computes standings, and upserts them to Supabase; a React frontend (GitHub Pages) reads them back for spectators. Codebase, UI text, and commit messages are in **Ukrainian** — match that when editing UI strings or writing commits.

## Solution layout (`Orientir.sln`)

- **Orientir.Core** — `net9.0` class library. All business logic: DBF parsing (`Dbf.cs`), results/standings computation (`Services/ResultsReader.cs`), Supabase publishing (`Services/SupabasePublisher.cs`), the polling loop (`Services/PublisherLoop.cs`), and SQLite-backed settings (`Services/SettingsService.cs`). EF Core 9 (SQLite).
- **Orientir.Console** — `net9.0` terminal UI. Custom console framework under `UI/` (no external TUI library).
- **Orientir.Desktop** — `net9.0-windows` WPF GUI. **The primary app to run/test.** Windows-only.

Console and Desktop are interchangeable front-ends over the same Core.

### Spectator frontend (lives in the **parent** directory, not in this repo dir)

- **`../web/`** — the live spectator frontend: **React + Vite + TypeScript + Tailwind**. This is what GitHub Pages serves. Deployed automatically via `../.github/workflows/deploy-pages.yml` on every push to `master` (official Pages artifact flow). Vite `base` is `/orientir-results/`, so the site URL is `https://vadymkolesnyk.github.io/orientir-results/?event=<id>&day=<n>`. After the first merge, Pages source must be set once to **GitHub Actions** in repo Settings. Reads Supabase with the **anon** key (RLS = SELECT only); key has an env-var override (`VITE_SUPABASE_*`) with a public fallback in `web/src/lib/supabase.ts`.
- **`../online/results.html`** — the **legacy** single-file static frontend (vanilla JS + Supabase CDN) that `web/` was ported from 1:1. Kept as a reference/backup; **no longer the Pages source.** `../online/schema.sql` holds the Supabase table definitions.

When changing spectator-facing behavior, edit `web/` (the React app), not `online/results.html`.

## Build & run

```powershell
dotnet build                          # whole solution
dotnet run --project Orientir.Desktop # primary app (WPF, Windows)
dotnet run --project Orientir.Console # terminal UI
```

There are **no tests** for the .NET solution. The only CI is the Pages deploy for `../web/` (see below). Verify .NET changes by running the app; verify frontend changes by running `web/` (`cd ../web && npm install && npm run dev`).

### Дистрибутив (один exe без .NET)

```powershell
dotnet publish Orientir.Desktop -c Release -r win-x64 --self-contained true -o publish
```

Дає єдиний `publish/Orientir.Desktop.exe` (~60 МБ) із вбудованим .NET runtime + WPF +
EF Core/SQLite. Його можна скопіювати на будь-який Windows x64 (де .NET не встановлено)
і запустити. При старті застосунок створює поруч із exe папку `data/` і пише туди
`orientir-settings.db` (див. `AppPaths.cs`). `.pdb` копіювати не обов'язково.

## Gotchas — do not break these

- **Windows-1251 DBF.** Source DBF files are Cyrillic (Visual FoxPro / "Ориентир"). The CodePages encoding provider must be registered (`Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`) before any DBF read. Don't assume UTF-8.
- **Secrets stay local.** The Supabase **service-role** (write) key lives in the SQLite settings DB / legacy `appsettings.json` — both gitignored. Never commit keys or the `.db` files. Only the **anon** read key belongs in the spectator frontend (`web/`, `online/results.html`).
- **Places & points are recalculated every tick** from finish times in `ResultsReader.cs` — they are NOT read from the DBF `M_1` field. This is deliberate (lets results go live before the operator assigns places). Ties share a place; points = `100·(2 − t/t_winner)` clamped to `[0,100]`.
- **Shared settings DB.** Console and Desktop both read/write the same `orientir-settings.db` (in `AppContext.BaseDirectory`) and can run at the same time.

## Conventions

- Nullable reference types and implicit usings are **enabled** in all projects.
- Commit only when explicitly asked. Match the Ukrainian commit-message style of the existing history.
