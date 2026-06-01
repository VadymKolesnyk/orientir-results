---
name: run-web
description: Build and launch the React spectator frontend (web/) on the local Vite dev server so you can see live-results changes in a browser. Use when asked to run, start, preview, or check the web frontend / spectator view.
---

# Run the web spectator frontend

The spectator frontend lives in `web/` (React + Vite + TypeScript + Tailwind). To run it locally:

```powershell
cd web
npm install     # only needed first time or after package.json changes
npm run dev
```

Vite prints a local URL (typically `http://localhost:5173`). The app reads live data from Supabase using the **anon** (read-only) key, with a public fallback in `web/src/lib/supabase.ts`, so no secrets are needed to run it.

The route is driven by query params — open with an event and day to see real data, e.g.:

```
http://localhost:5173/orientir-results/?event=<id>&day=<n>
```

Note the `/orientir-results/` path segment: it comes from Vite `base` in `web/vite.config.ts` (matches the GitHub Pages subdirectory). The dev server serves under that base too.

To verify a production build instead of dev:

```powershell
cd web
npm run build    # tsc -b && vite build
npm run preview
```

UI strings are in Ukrainian — keep that when editing.
