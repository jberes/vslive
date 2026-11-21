# Dispatcher – Claude Opus build

**Rail Ops Dispatch** is a railway maintenance dispatch console. **Claude Opus** built it from the Demo 1 [design brief](../../brief.md), starting from an Ignite UI CLI React scaffold. It is one of three builds of the same brief. See the [Demo 1 overview](../../README.md) to compare them.

The UI is in English. The primary view follows direction **A. Operations Ledger**, and **Shift Planner** and **Corridor Control** are built as focused views. All three share filters, status language and job details.

## What it does

| Route | Nav label | Screen | Brief journey |
|---|---|---|---|
| `/` | — | Redirects to `/ledger` | |
| `/ledger?region&status&priority&sla&q&job=` | Operations ledger | Risk strip, a "highest-impact problem" callout, the job grid (Grid Lite), and a persistent detail panel. You can assign, reschedule, and change status in place. | 1 Assign · 3 SLA action |
| `/jobs/:id` | — | Full job record: timeline, notes, reschedule history. "Back to ledger" restores your filters and selection. | 1 |
| `/sla` | SLA risk | KPIs, due-by-hour, by priority and region, 14-day trend (ApexCharts), and an at-risk grid with cause and next action | 3 SLA risk |
| `/planner?date&depot&skill` | Shift planner | Crew grid, ApexCharts **rangeBar timeline**, and an exception queue. Reschedule opens with an impact preview (date picker and 24 h time input). | 2 Reschedule |
| `/corridor?line=` | Corridor | Line list → station-ordered jobs → geographic schematic | context |
| `/incident/new` | Report urgent fault | Location, service impact, safety restriction, escalation path, then dispatch qualified responders | 4 Urgent fault |
| `/handover` | Handover | Unresolved faults, restrictions, pending approvals, next actions → named incoming owner, plus acknowledgement | 5 Handover |
| `*` | — | Not found | |

Behaviour that applies across the app:
- **Location context.** The region selector in the top bar applies to every view. It is stored in `localStorage` and the URL.
- **Filters and selection survive navigation.** Ledger state lives in URL search params and is mirrored to `sessionStorage`.
- **Role-based permissions.** You choose a "signed in as" user from `/api/dispatchers`, because the API has no auth. The roles are:
  - **Dispatcher**: assign, reschedule, status changes, notes, and urgent faults. Can only move priority between High, Medium and Low.
  - **Senior dispatcher**: can also set or clear Critical, and reassign a crew's day.
  - **Supervisor**: can also override blocking validation, with a recorded reason.
  - **Blocked actions** offer "Request supervisor approval". The request is recorded as a job note and shown in handover.
- **Conflict detection.** The API has no row versions. Before each save, the app re-reads the job and compares `updatedAt`, status, crew and start time. If anything changed, a conflict dialog shows the differences. Every save ends in an explicit outcome: saved, blocked, conflict, or failed.
- **Freshness.** The operations clock comes from `/api/meta/clock`. Views poll every **30 s**, data older than 90 s is marked **Stale**, and failed requests show a **Disconnected** banner.
- **Status is never color-only.** Each state has an icon shape, text, and color.
- **Charts share the theme.** ApexCharts loads its Ignite UI add-on (`apexcharts/features/ignite-ui`) so the charts read the same Fluent `--ig-*` tokens as the rest of the UI.

[`PLAN.md`](PLAN.md) has the model's full route map, design notes, and known backend gaps.

## Tech

- React 19, TypeScript, Vite 8, React Router 7
- [Ignite UI for React](https://www.infragistics.com/products/ignite-ui-react) `~19.9` (OSS: Navbar, Select, Combo, Dialog, DatePicker, DateTimeInput, Toast, …) and **Grid Lite** (`igniteui-react/grid-lite`, `igniteui-grid-lite` 0.11)
- [ApexCharts](https://apexcharts.com/) via `react-apexcharts`, used for the bar, line, rangeBar timeline, and scatter charts
- Theme: Ignite UI **Fluent** light, with a palette from the theming MCP server in `src/styles/theme.css` (primary `#155dfc`)
- Generated with Ignite UI CLI 16.0.0 (`ig new dispatcher --framework=react --type=igr-ts`)

## Prerequisites

- **Node.js 22.12 or later** and npm. `package.json` has no `engines` field. This minimum comes from the dependencies: Vite 8 needs `^20.19 || >=22.12`, and `igniteui-webcomponents`/`igniteui-grid-lite` need `>=22`.
- The **Dispatch API** running on `http://localhost:5207`. See the [Backend README](../../Backend/README.md). That includes restoring the database first. Without the API, the app stops at a "Cannot reach the dispatch service" screen.

## Run it

```bash
# terminal 1: backend
cd "Demo 1/Backend/DispatchApi"
dotnet run --launch-profile http

# terminal 2: this app
cd "Demo 1/Opus/dispatcher"
npm install
npm start
```

Open **http://localhost:3102**. This build sets `server.open: false`, so the browser does **not** open automatically.

> The script is `npm start`. There is **no `npm run dev`**. You can also use `npx ig start`.

## How it connects to the backend

The browser calls **relative `/api/...` URLs**. The **Vite dev server proxies** them to the API, so the browser never makes a cross-origin request and CORS doesn't apply.

```ts
// vite.config.ts
server: {
  port: 3102,
  strictPort: true,
  proxy: { '/api': { target: process.env.VITE_API_TARGET ?? 'http://localhost:5207', changeOrigin: true } }
}

// src/api/client.ts
const BASE = (import.meta.env.VITE_API_BASE as string | undefined) ?? '/api';
// requests: fetch(`${BASE}/meta/clock`), fetch(`${BASE}/jobs?…`), …
```

**If the API is on a different port**, set `VITE_API_TARGET` in the shell that starts Vite. The config reads it from `process.env`, so a value in a `.env` file is **not** picked up.

```bash
VITE_API_TARGET=http://localhost:5300 npm start            # macOS / Linux / Git Bash
```
```powershell
$env:VITE_API_TARGET = "http://localhost:5300"; npm start  # Windows PowerShell
```

**Bypassing the proxy (optional).** Set `VITE_API_BASE` to a full API base that **includes `/api`**, for example `http://localhost:5207/api`. You can put it in `.env.local`. The browser then calls the API directly, so the app's origin must be in the API's `Cors:Origins`.

`VITE_BASENAME` (optional) sets the router base path.

## Scripts

| Command | What it does |
|---|---|
| `npm start` | Vite dev server on port 3102, with the `/api` proxy |
| `npm run build` | Type-check (`tsc -b`), then production build to `dist/` |
| `npm run preview` | Serve `dist/` on port 3102. Vite's preview server reuses `server.proxy`, so `/api` is still proxied. |
| `npm run lint` | ESLint, with zero warnings allowed |
| `npm test` | Vitest in **browser mode** (Playwright Chromium). Runs `src/app/app.test.tsx`: unit tests for impact ordering, risk-cause text, next action, and role permissions. Use `npm test -- --run` for a single run instead of watch mode. The first time, install the browser with `npx playwright install chromium`. |

A static deployment of `dist/` has no proxy. Build with `VITE_API_BASE=https://your-api/api` and allow that site's origin in the API's CORS list.

## Project structure

```
Opus/dispatcher/
├── index.html
├── styles.css                 # base styles from the scaffold
├── vite.config.ts             # port 3102 (strictPort), /api proxy, Vitest browser config
├── PLAN.md                    # route map + implementation plan written by the model
└── src/
    ├── main.tsx               # theme imports, icon registration, router
    ├── app/
    │   ├── app.tsx            # shell: navbar, region context, ops clock, connection status, "signed in as"
    │   ├── app-routes.tsx     # lazy-loaded routes
    │   └── app.test.tsx       # unit tests
    ├── api/                   # client.ts (all endpoints), types.ts
    ├── routes/                # ledger/, jobs/, sla/, planner/, corridor/, incident/, handover/, NotFound
    ├── components/            # JobPanel, AssignDialog, RescheduleDialog, ConflictDialog, OutcomeTray, Chart, States, Tags, Icon, IssueList
    ├── lib/                   # status/risk logic, permissions, approvals, conflict detection, time formatting, chart colors
    ├── state/                 # session (user/region/lookups), connection, polling, ledger URL state, save outcomes
    └── styles/                # theme.css (Fluent palette), app.css (layout)
```

The other files are AI-assistant configuration used during the build: `AGENTS.md`, `.claude/`, `.agents/skills/`, `.mcp.json` (the `igniteui-cli` and `igniteui-theming` MCP servers), plus `ignite-ui-cli.json` and the CI templates in `.github/` and `.azure/`. You don't need them to run the app.

## Troubleshooting

| Symptom | Fix |
|---|---|
| "Cannot reach the dispatch service … needs the Dispatch API (port 5207)" | Start the API. If it runs on another port, start Vite with `VITE_API_TARGET`. |
| `[vite] http proxy error: /api/... ECONNREFUSED` in the terminal | Same cause: nothing is listening at the proxy target. |
| Requests 404 at `/meta/...` instead of `/api/meta/...` | You set `VITE_API_BASE` without the `/api` suffix. |
| `Error: Port 3102 is already in use` | An earlier run of this app is still running. Stop it, or run on another port with `npm start -- --port <n>`. The proxy means any port works with the API. |
| `npm test` errors about a missing browser | Run `npx playwright install chromium`. |
| Vite or npm errors about the Node version | Upgrade to Node 22.12 or later. |
