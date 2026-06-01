---
name: publish-desktop
description: Build the self-contained single-file Windows .exe distributable of the Orientir.Desktop WPF app (no .NET install needed on the target machine). Use when asked to publish, package, build a release exe, or make a distributable of the desktop app.
disable-model-invocation: true
---

# Publish the desktop app as a standalone .exe

Produces a single ~60 MB `Orientir.Desktop.exe` with the .NET runtime + WPF + EF Core/SQLite embedded, runnable on any Windows x64 machine without .NET installed.

Run from the `Orientir/` solution directory:

```powershell
cd Orientir
dotnet publish Orientir.Desktop -c Release -r win-x64 --self-contained true -o publish
```

The result is `Orientir/publish/Orientir.Desktop.exe`. Copying the `.pdb` is optional.

## Notes

- On first launch the app creates a `data/` folder next to the exe and writes `orientir-settings.db` there (see `AppPaths.cs`). The Supabase **service-role** key is stored in that local DB — it is **not** baked into the exe, so the distributable contains no secrets.
- This targets `win-x64` only (WPF is Windows-only).
- After publishing, report the output path. Do not commit the `publish/` folder or any `.db` files.
