# Demo 1 – One brief, three AI models: a railway dispatch console

This demo is from a VSLive session on **AI-assisted app building with Infragistics Ignite UI**. Three AI coding models were each given the same design brief, the same Ignite UI for React starter project, and the same backend API. Each one built a complete **railway maintenance dispatch console**. The three results sit side by side here so you can run them and compare.

- One shared **ASP.NET Core + SQLite backend** (`Backend/`), with realistic data for a fictional railway, *Atlantic Corridor Rail*.
- Three **React + Vite + Ignite UI** frontends, one per model: `Haiku/`, `Opus/` and `MAI-Code-2/`.

## The story

The prompts and brief used in the session are in this folder. Read them first to see what the models were asked to do:

| File | What it is |
|---|---|
| [`brief.md`](brief.md) | **The main design brief.** A railway maintenance operations lead asks for a console that gives 40 dispatchers on 1920 px screens a shared operational picture. It defines five journeys: assign a job, reschedule a crew, see today's SLA risk, coordinate an urgent fault, and complete a shift handover. It also sets the data and decision rules and three visual directions (Operations Ledger, Corridor Control, Shift Planner), and picks **Operations Ledger** as the primary direction. It has hard constraints: use the component library through its MCP server, never hand-roll grids, charts or date pickers, primary color `#155dfc`, Fluent theme, accessible status that does not rely on color alone, designed loading/stale/disconnected/conflict states, and "screenshot every route and fix what you see before you say done". It names **Ignite UI React OSS + Grid Lite** for UI and grids and **ApexCharts** for charts. |
| [`project-setup.md`](project-setup.md) | The setup commands and kickoff prompt: install `igniteui-cli`, run `ig ai-config`, scaffold with `ig new dispatcher --framework=react --type=igr-ts`, and add `react-apexcharts`/`apexcharts`. The prompt then tells the agent to read the brief, build on the scaffolded app, use `Backend/` as the API on `http://localhost:5207`, and use ApexGantt for any Gantt or schedule UI. |
| [`simple-prompt.md`](simple-prompt.md) | A one-paragraph "minimal prompt" version of the same task (it targets Angular), for contrast with the full brief. **No app in this folder was built from it.** |

The kickoff prompt gives the agent the backend as an existing API. The frontends are the models' output.

## What's in this folder

| Folder | What it is | Built by | Tech | Port |
|---|---|---|---|---|
| [`Backend/`](Backend/README.md) | Dispatch API: jobs, technicians, scheduling, SLA risk, dashboards. SQLite data in `Backend/database/`. | Pre-built for the demo | ASP.NET Core **.NET 10**, EF Core SQLite, OpenAPI + Swagger UI | **5207** (http), 7250 (https) |
| [`Haiku/dispatcher/`](Haiku/dispatcher/README.md) | Dispatch console with a **Japanese** UI: Operations Ledger, crew reschedule, SLA risk, urgent fault, handover | Claude **Haiku** | React 19, Vite 8, Ignite UI for React 19.9, Grid Lite, ApexCharts | **3101** |
| [`Opus/dispatcher/`](Opus/dispatcher/README.md) | Dispatch console with an English UI: Ledger, job record, SLA risk, Shift Planner (ApexCharts timeline), Corridor, urgent fault, handover. Includes role permissions and conflict detection. | Claude **Opus** | React 19, Vite 8, Ignite UI for React 19.9, Grid Lite, ApexCharts | **3102** |
| [`MAI-Code-2/dispatcher/`](MAI-Code-2/dispatcher/README.md) | Dispatch console with an English UI: Ledger, Corridor Control, Shift Planner (**ApexGantt** drag-to-reschedule), Handover | **MAI-Code-2** | React 19, Vite 8, Ignite UI for React 19.9, Grid Lite, ApexCharts, ApexGantt | **3103** |

> **Each frontend has its own port** (Haiku 3101, Opus 3102, MAI-Code-2 3103), so you can run all three side by side against one API and compare them. `strictPort` is set, so Vite fails fast instead of drifting to a port the API's CORS list doesn't allow.

## Which server each client talks to

All three frontends use the **same backend**, the Dispatch API at **`http://localhost:5207`**. They reach it in different ways, and that matters if you change ports:

| Client | How it reaches the API | Where the URL is set | To point it somewhere else |
|---|---|---|---|
| **Haiku** | **Directly from the browser** (cross-origin) to `http://localhost:5207/api/...` | `src/api/client.ts`: `import.meta.env.VITE_API_BASE ?? 'http://localhost:5207'` | Set `VITE_API_BASE=http://localhost:<port>` (origin only) in `.env.local`, **and** make sure the frontend's origin is in the API's `Cors:Origins` |
| **Opus** | Relative `/api/...` → **Vite dev-server proxy** → `http://localhost:5207` | `vite.config.ts`: proxy target `process.env.VITE_API_TARGET ?? 'http://localhost:5207'`. `src/api/client.ts`: base `VITE_API_BASE ?? '/api'` | Start with the env var `VITE_API_TARGET=http://localhost:<port>` (shell env, not `.env`) |
| **MAI-Code-2** | Relative `/api/...` → **Vite dev-server proxy** → `http://localhost:5207` | `vite.config.ts`: `proxy: { '/api': 'http://localhost:5207' }` (hard-coded). `src/app/dispatch-data.ts`: `API_BASE = '/api'` | Edit the proxy target in `vite.config.ts` |

Because Opus and MAI-Code-2 use a proxy, CORS never applies to them. Haiku relies on the API's CORS list, which includes `http://localhost:3101` (3102 and 3103 are listed too). Each frontend README covers this in more detail.

## Quick start (end to end)

### Prerequisites

- **.NET 10 SDK**: [download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Node.js 22.12 or later** (with npm). Vite 8 needs `^20.19 || >=22.12`, and `igniteui-webcomponents` and `igniteui-grid-lite` declare `node >=22`.
- A way to un-gzip a file: `gunzip`, PowerShell, 7-Zip or Python. Python 3.7+ is only needed if you want to *regenerate* the database.

### 1. Restore the database

The 65 MB `dispatch.db` isn't committed. A gzip copy is committed instead.

```bash
# macOS / Linux / Git Bash
cd "Demo 1/Backend/database"
gunzip -k dispatch.db.gz
```

```powershell
# Windows PowerShell
cd "Demo 1\Backend\database"
$in  = [IO.File]::OpenRead("$PWD\dispatch.db.gz"); $out = [IO.File]::Create("$PWD\dispatch.db")
$gz  = New-Object IO.Compression.GZipStream($in, [IO.Compression.CompressionMode]::Decompress)
$gz.CopyTo($out); $gz.Dispose(); $out.Dispose(); $in.Dispose()
```

7-Zip and Python alternatives, plus how to regenerate the database from scratch, are in the [Backend README](Backend/README.md#database).

### 2. Start the API (terminal 1)

```bash
cd "Demo 1/Backend/DispatchApi"
dotnet run --launch-profile http
```

Wait for `Now listening on: http://localhost:5207`. Swagger UI opens at http://localhost:5207/swagger.

### 3. Start one frontend (terminal 2)

```bash
cd "Demo 1/Opus/dispatcher"      # or Haiku/dispatcher, or MAI-Code-2/dispatcher
npm install
npm start                        # all three use "start" (vite). There is no "dev" script.
```

Open **http://localhost:3101** (Haiku), **http://localhost:3102** (Opus) or **http://localhost:3103** (MAI-Code-2). Haiku and MAI-Code-2 open the browser automatically. Opus does not.

To compare the models, start the other frontends in more terminals. All three can run at the same time against the one API.

## How the three builds differ (at a glance)

| | Haiku | Opus | MAI-Code-2 |
|---|---|---|---|
| UI language | Japanese (`src/i18n/ja.ts`) | English | English |
| Routes | `/`, `/jobs/:jobId`, `/schedule`, `/sla`, `/urgent`, `/handover` | `/ledger`, `/jobs/:id`, `/sla`, `/planner`, `/corridor`, `/incident/new`, `/handover` | `/ledger`, `/corridor`, `/planner`, `/handover` |
| Brief journeys covered | All 5 | All 5, plus Corridor and Shift Planner views | Assign, reschedule, SLA risk (on the Ledger and Corridor views), handover. There is no separate urgent-fault screen. |
| Schedule UI | Grid Lite + date/time inputs | ApexCharts `rangeBar` timeline + reschedule dialog | ApexGantt with drag moves + impact review |
| API access | Direct + CORS | Vite proxy (configurable target) | Vite proxy (hard-coded target) |
| Polling | 30 s, stale after 90 s | 30 s, stale after 90 s | 60 s |
| Tests | none | unit tests for ordering, risk text, permissions | shell render test |
| Plan doc | [`PLAN.md`](Haiku/dispatcher/PLAN.md) + [screenshots](Haiku/dispatcher/docs/screenshots) | [`PLAN.md`](Opus/dispatcher/PLAN.md) | (none) |

Every app also contains the AI-assistant setup the agent used during the build: `AGENTS.md`, `.claude/CLAUDE.md`, `.agents/skills/`, and `.mcp.json` in Opus and MAI-Code-2, which registers the `igniteui-cli` and `igniteui-theming` MCP servers. You don't need any of it to *run* the apps.

## Troubleshooting

- **The API won't start with "SQLite database not found".** Do step 1 first. See the [Backend README](Backend/README.md#troubleshooting).
- **The frontend says "Cannot reach the dispatch service" or shows a connection error.** The API isn't running on `http://localhost:5207`. Start it, or point the frontend at the right port (see the table above).
- **`Error: Port 3101 is already in use`** (or 3102/3103). An earlier run of that same app is still running. Stop it with Ctrl+C in its terminal, or find the process with `lsof -i :3101` (macOS/Linux) or `netstat -ano | findstr :3101` (Windows). If you pick a different port with `npm start -- --port <n>`, also add `http://localhost:<n>` to `Backend/DispatchApi/appsettings.json` → `Cors:Origins` for Haiku.
- **`npm install` fails or Vite errors on startup.** Check `node -v`. You need 22.12 or later.
- **You want to reset the data.** Stop the API and run `gunzip -kf dispatch.db.gz` again. The API writes your changes, and its startup date shift, into `dispatch.db`.

## Folder layout

```
Demo 1/
├── README.md               ← you are here
├── brief.md                ← the design brief given to every model
├── project-setup.md        ← scaffolding commands + kickoff prompt
├── simple-prompt.md        ← minimal one-paragraph prompt (for contrast)
├── Backend/                ← ASP.NET Core API + SQLite database
├── Haiku/dispatcher/       ← React app built by Claude Haiku
├── Opus/dispatcher/        ← React app built by Claude Opus
└── MAI-Code-2/dispatcher/  ← React app built by MAI-Code-2
```
