---
name: run-desktop
description: Build and launch the Orientir.Desktop WPF app (the project's primary front-end) so a change can be seen working. Use when asked to run, start, or test the desktop app.
disable-model-invocation: true
---

Build and launch the WPF desktop app for this orienteering results publisher.

## Steps

1. Build the solution first to surface compile errors clearly:
   ```powershell
   dotnet build
   ```
   If the build fails, report the errors and stop — do not try to launch.

2. Launch the Desktop app (Windows-only, `net9.0-windows`):
   ```powershell
   dotnet run --project Orientir.Desktop
   ```
   This opens a WPF window — it does not return to the terminal until the window is closed. Run it in the background if you need to keep working, and tell the user the window is open.

## Notes

- This shares the `orientir-settings.db` with the Console app; the user may already have settings configured.
- On first run with an empty DB, legacy `appsettings.json` is auto-imported if present.
- Cyrillic data depends on the Windows-1251 encoding provider being registered at startup — if text shows as mojibake, that registration is the first thing to check.
- To run the terminal UI instead: `dotnet run --project Orientir.Console`.
