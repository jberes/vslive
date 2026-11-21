# Dispatcher – MAI-Code-2 build

**Atlantic Corridor Rail Dispatch Console** is a railway maintenance dispatch console. The **MAI-Code-2** model built it from the Demo 1 [design brief](../../brief.md), starting from an Ignite UI CLI React scaffold. It is one of three builds of the same brief. See the [Demo 1 overview](../../README.md) to compare them.

This build follows the kickoff prompt in [`project-setup.md`](../../project-setup.md), which names this folder as the base app and asks for **ApexGantt** for the schedule UI. It is the only one of the three builds that uses ApexGantt.

## What it does

It is an Operations Ledger React console for the Dispatch API, built with Ignite UI for React, Grid Lite, ApexCharts and ApexGantt on the Fluent theme. The UI is in English.

| Route | Nav label | Screen |
|---|---|---|
| `/` | — | Redirects to `/ledger` |
| `/ledger` | Operations Ledger | Critical-first unresolved work, SLA summary cards (active / at risk / breached / compliance), operational context, and a detail panel for assigning a qualified crew (from `/recommendations`) |
| `/corridor` | Corridor Control | Regional service exposure (ApexCharts), breach concentration, and location risk |
| `/planner` | Shift Planner | **ApexGantt** crew schedule. You drag a job in **15-minute** steps, then review the impact before saving. |
| `/handover` | Handover | Unresolved work, a named incoming owner, and handover notes saved to the job record |
| `*` | — | Redirects to `/ledger` |

Behaviour that applies across the app:
- The UI refreshes operations data every **60 seconds**. There is also a manual **Refresh** button.
- The region filter in the top bar scopes every view.
- Saves are **server-authoritative**. Before it confirms success, the API validates qualifications, shift bounds, overlap, access windows, travel, and SLA impact. Validation messages from a 409 response are shown in the error banner.
- **Placeholder data:** the signed-in dispatcher ("Alex Morgan", shift S2) and the incoming-owner choices on the Handover page are hard-coded demo values. They are not loaded from `/api/dispatchers`. This build has no role permissions and no client-side conflict detection.

## Tech

- React 19, TypeScript, Vite 8, React Router 7
- [Ignite UI for React](https://www.infragistics.com/products/ignite-ui-react) `~19.9` (OSS) and **Grid Lite** (`igniteui-react/grid-lite`, `igniteui-grid-lite` 0.11)
- [ApexCharts](https://apexcharts.com/) via `react-apexcharts`
- **ApexGantt** (`apexgantt` ^3.23), used for the Shift Planner. This is the dependency the other two builds don't have.
- Theme: Ignite UI **Fluent** light (`igniteui-webcomponents/themes/light/fluent.css`), with app styles in `styles.css` (primary `#155dfc`)
- Generated with Ignite UI CLI 16.0.0 (`ig new dispatcher --framework=react --type=igr-ts`)

## Prerequisites

- **Node.js 22.12 or later** and npm. `package.json` has no `engines` field. This minimum comes from the dependencies: Vite 8 needs `^20.19 || >=22.12`, and `igniteui-webcomponents`/`igniteui-grid-lite` need `>=22`.
- The **Dispatch API** running on `http://localhost:5207`. See the [Backend README](../../Backend/README.md). That includes restoring the database first.

## Run it

Start the backend from `Demo 1/Backend/DispatchApi`, then run this app:

```bash
# terminal 1: backend
cd "Demo 1/Backend/DispatchApi"
dotnet run --launch-profile http

# terminal 2: this app
cd "Demo 1/MAI-Code-2/dispatcher"
npm install
npm start
```

Vite serves the client on **http://localhost:3103** and opens the browser automatically (`server.open: true`).

> The script is `npm start` (or `npm run start`). There is **no `npm run dev`**.

## How it connects to the backend

The browser calls **relative `/api/...` URLs**, and the **Vite dev server proxies** them to `http://localhost:5207`. Because the requests are same-origin, CORS doesn't apply.

Both values are **hard-coded**. This build reads no environment variables for the API.

```ts
// vite.config.ts
server: {
  open: true,
  port: 3103,
  strictPort: true,
  proxy: { '/api': 'http://localhost:5207' }
}

// src/app/dispatch-data.ts
export const API_BASE = '/api';
// apiFetch('/jobs?…') → fetch('/api/jobs?…')
```

**If the API runs on a different port**, edit the proxy target in `vite.config.ts`, for example `'/api': 'http://localhost:5300'`, then restart `npm start`.

**Calling the API directly instead (for example from a static build with no proxy).** Change `API_BASE` in `src/app/dispatch-data.ts` to the full URL, including `/api` (for example `http://localhost:5207/api`). Then add the app's origin to `Cors:Origins` in `Backend/DispatchApi/appsettings.json`.

`VITE_BASENAME` (optional) sets the router base path.

## Scripts

| Command | What it does |
|---|---|
| `npm start` | Vite dev server on port 3103, with the `/api` proxy |
| `npm run build` | Type-check (`tsc -b`), then production build to `dist/` |
| `npm run preview` | Serve `dist/` on port 3103. Vite's preview server reuses `server.proxy`, so `/api` is still proxied. |
| `npm run lint` | ESLint, with zero warnings allowed |
| `npm test` | Vitest in **browser mode** (Playwright Chromium). `src/app/app.test.tsx` renders the shell and checks for the "Infrastructure dispatch" heading. Use `npm test -- --run` for a single run. The first time, install the browser with `npx playwright install chromium`. |

To validate the project as the model did:

```bash
npm run build
npm run lint
npm test -- --run
```

## Project structure

This build keeps nearly everything in `src/app/`:

```
MAI-Code-2/dispatcher/
├── index.html
├── styles.css                    # all app styles (layout, Fluent overrides, grid density)
├── vite.config.ts                # port 3103 (strictPort), /api proxy, Vitest browser config
└── src/
    ├── main.tsx                  # theme import, router
    ├── setupTests.ts             # test mocks (ResizeObserver, matchMedia, popover)
    └── app/
        ├── app.tsx               # shell: side nav, region filter, ops clock, refresh, banners
        ├── app-routes.tsx        # /ledger, /corridor, /planner, /handover
        ├── app.test.tsx          # shell render test
        ├── dispatch-data.ts      # types, DispatchContext, apiFetch + API_BASE, formatters
        ├── dispatch-provider.tsx # data loading (60 s refresh), assign / reschedule / note actions
        ├── pages.tsx             # Ledger, Corridor, Planner (ApexGantt), Handover + shared cells
        └── style-utils.ts
```

The other files are AI-assistant configuration used during the build: `AGENTS.md`, `.claude/`, `.agents/skills/`, `.github/skills/`, `.github/copilot-instructions.md`, `.mcp.json`, and `.vscode/mcp.json` (the `igniteui-cli` and `igniteui-theming` MCP servers), plus `ignite-ui-cli.json` and the CI templates in `.github/workflows/` and `.azure/`. You don't need them to run the app.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Red "Connection issue" banner, or `[vite] http proxy error … ECONNREFUSED` in the terminal | The API isn't running at `http://localhost:5207`. Start it, or change the proxy target in `vite.config.ts`. |
| "No crews on shift" on the Shift Planner | Either the API isn't connected, or the selected region has no crews on shift. Try "All regions". |
| `Error: Port 3103 is already in use` | An earlier run of this app is still running. Stop it, or run on another port with `npm start -- --port <n>`. The proxy means any port works with the API. |
| `npm test` errors about a missing browser | Run `npx playwright install chromium`. |
| Vite or npm errors about the Node version | Upgrade to Node 22.12 or later. |
