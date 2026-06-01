# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Live results system for orienteering races. Pipeline: legacy "Ориентир" **DBF files → .NET publisher (computes standings) → Supabase → React spectator frontend (GitHub Pages)**. Codebase, UI text, and commit messages are in **Ukrainian** — match that when editing UI strings or writing commits.

## Layout (monorepo)

- **`Orientir/`** — the .NET 9 solution (`Orientir.sln`): `Orientir.Core` (all business logic, EF Core/SQLite), `Orientir.Console` (terminal UI), `Orientir.Desktop` (WPF GUI, **the primary app to run**). See **`Orientir/CLAUDE.md`** for backend detail before working in here.
- **`web/`** — React + Vite + TypeScript + Tailwind spectator frontend. Auto-deployed to GitHub Pages via `.github/workflows/deploy-pages.yml` on every push to `master`. Vite `base` is `/orientir-results/`. Edit `web/` (not `online/`) for spectator-facing changes.
- **`online/`** — legacy single-file static frontend + `schema.sql` (Supabase tables). Reference/backup only; no longer the Pages source.

## Build & run

```powershell
dotnet build                          # whole .NET solution (run from Orientir/)
dotnet run --project Orientir.Desktop # primary app (WPF, Windows-only)
cd web; npm install; npm run dev      # spectator frontend
```

No automated .NET tests — verify by running the app. Only CI is the Pages deploy for `web/`.

## Gotchas — do not break these

- **Windows-1251 DBF.** Source DBF files are Cyrillic. `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` must run before any DBF read — don't assume UTF-8.
- **Secrets stay local.** The Supabase **service-role** (write) key lives only in the gitignored SQLite settings DB / `appsettings.json`. Never commit keys or `.db` files. Only the **anon** read key belongs in the frontend.
- **Places & points are recalculated every tick** from finish times in `ResultsReader.cs` — NOT read from the DBF `M_1` field. Ties share a place; `points = 100·(2 − t/t_winner)` clamped to `[0,100]`.
- C# style is enforced by `Orientir/.editorconfig` (file-scoped namespaces, `_camelCase` private fields) and auto-applied by the `format-cs.ps1` PostToolUse hook. Commit only when explicitly asked.
