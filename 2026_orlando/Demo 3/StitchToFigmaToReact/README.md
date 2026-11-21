# Aura CRM (Stitch → Figma → React)

A React + TypeScript single-page app that implements the two
[Stitch CRM Figma screens](https://www.figma.com/design/JGPVfW9QcUZShVFVBrP3N5/Stitch-CRM?node-id=0-1):
**Customer Intelligence** and **Orders & Fulfillment**. The folder name describes the workflow the demo shows. The screens
were designed in Stitch, refined in Figma, and then implemented as a working React app.

It uses the Indigo.Design UI Kit for Material 2.0 as the component reference, **Ignite UI for React** (including
**Grid Lite**) for the interface, and **ApexCharts for React** for the charts.

> **Standalone app.** It does **not** call the BusinessApp API in [`../Backend`](../Backend/README.md).
> All records and metrics are mock data in [`src/data.ts`](src/data.ts), and you don't need a backend or credentials to run it.

## Prerequisites

* **Node.js 22.12 or newer.** Vite 8 requires `^20.19.0 || >=22.12.0`, and `igniteui-webcomponents` / `igniteui-grid-lite` require `>=22`.
  Check with `node --version`.
* npm (ships with Node.js).

## Install and run

```bash
cd "Demo 3/StitchToFigmaToReact"
npm ci          # installs the exact versions from package-lock.json (or use: npm install)
npm run dev
```

There's no `vite.config.*`. The port is set on the command line in `package.json`: the dev server listens on
**http://localhost:3301**, with `--strictPort`, so Vite stops with an error if the port is taken instead of switching. The commands are the same on macOS, Linux and Windows.

| Script | Command | What it does |
| --- | --- | --- |
| `npm run dev` | `vite --port 3301 --strictPort` | Dev server with hot reload on port 3301 |
| `npm run build` | `tsc && vite build` | Type-checks, then builds to `dist/` |
| `npm run preview` | `vite preview --port 3301 --strictPort` | Serves the built `dist/` locally on port 3301 |

## Using the app

| Route | Screen |
| --- | --- |
| `/` | Redirects to `/customers` |
| `/customers` | Customer Intelligence: KPI metrics, charts, a customer grid with search, filters, pagination and CSV export |
| `/orders` | Orders & Fulfillment: KPI metrics, charts, an order grid with filters, pagination and CSV export |
| any other path | "Page not available" placeholder |

The other sidebar entries (Products & Catalog, Analytics & Reports, Marketing, and so on) come from the design and aren't wired up.
The global search, the "add" dialog and order actions work on in-memory state, so changes reset when you reload.
The headline figures are the illustrative totals from the Figma design. Filters, table-footer counts, pagination and
CSV export use the local sample records.

For static hosting, configure the host to serve `index.html` for client-side routes (it uses `BrowserRouter`).

## Data source

[`src/data.ts`](src/data.ts) holds the mock data:

* `initialCustomers`: Northwind-style accounts (company, contact, location, focus category, status, revenue).
* `initialOrders`: a handful of Northwind-style orders (status, carrier, dates, freight, destination).
* `exportCsv()` and `money` helpers.

To point the screens at real data, you'd replace these arrays with `fetch` calls. The BusinessApp API's
`/api/customers` and `/api/orders` endpoints, and its CORS list, already allow `http://localhost:3301`. That wiring
doesn't exist in this project today.

## Project structure

```
StitchToFigmaToReact/
  index.html            Vite entry HTML (title "Aura CRM")
  package.json          Scripts and dependencies
  tsconfig.json         Strict TS, bundler module resolution, react-jsx
  src/
    main.tsx            App shell: sidebar nav, top bar/search, add dialog, routes; Ignite UI Material light theme
    customers.tsx       Customer Intelligence screen (IgrGridLite, IgrAvatar)
    orders.tsx          Orders & Fulfillment screen (IgrGridLite, IgrAvatar)
    charts.tsx          ApexCharts (react-apexcharts, lazy-loaded) for both screens
    ui.tsx              Shared pieces: Panel, Metric, Chip, Filters, SearchBox, Pagination, Action, Meter, usePaging
    data.ts             Mock data + CSV export
    styles.css          App styles
```

### Main dependencies

| Package | Version range | Used for |
| --- | --- | --- |
| `react`, `react-dom` | `^19.2.0` | UI |
| `react-router-dom` | `^7.0.0` | Routing |
| `igniteui-react` | `^19.11.0` | Navbar, Dialog, Input, Button, Card, Chip, Avatar, and `igniteui-react/grid-lite` |
| `igniteui-grid-lite` | `^0.12.0` | Grid Lite engine |
| `apexcharts`, `react-apexcharts` | `^8.9.0`, `^2.2.1` | Charts |
| `lucide-react` | `^0.577.0` | Icons |
| `vite`, `typescript` | `^8.3.8`, `^6.0.0` | Tooling |

The theme comes from `igniteui-webcomponents/themes/light/material.css` (installed as a dependency of `igniteui-react`)
plus `configureTheme('material', 'light')` in `main.tsx`.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `npm ci` warns `EBADENGINE`, or Vite fails to start | Upgrade to Node.js 22.12+. |
| `npm ci` fails because the lockfile is out of sync | Use `npm install` instead. |
| `Error: Port 3301 is already in use` | An earlier run is still going. Stop it, or run `npx vite --port 3310`. |
| Refreshing `/orders` on a static host gives 404 | Configure an SPA fallback to `index.html`. |
| Components unstyled | Make sure `node_modules/igniteui-webcomponents` was installed. The Material theme CSS is imported in `src/main.tsx`. |
