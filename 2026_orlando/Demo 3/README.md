# Demo 3: Modernizing apps with AI + Ignite UI

This VSLive demo uses AI coding assistants with Infragistics **Ignite UI** to turn existing designs and apps into modern web
apps. It shows two workflows:

1. **Windows Forms → modern web (Blazor).** A WinForms "Outlook-inspired" business app (Sales, Project Manager Gantt,
   Marketing) is redesigned with Claude Design and rebuilt as a Blazor WebAssembly app with Ignite UI for Blazor,
   Blazor-ApexCharts and ApexGantt. An ASP.NET Core API with SQLite provides the data.
2. **Stitch → Figma → React.** CRM screens designed in Stitch and refined in Figma are implemented as a React app with
   Ignite UI for React (Grid Lite) and ApexCharts.

![The original WinForms Project Manager Gantt screen](Windows%20Forms%20App/Gantt-App.png)

## The story and the prompts

| File / folder | What it is |
| --- | --- |
| [`Windows Forms App/`](Windows%20Forms%20App) | Screenshots of the original WinForms app: five Outlook-style Sales screens and the Project Manager Gantt (`Gantt-App.png`). This is the "before". |
| [`Claude-Design-Prompt.md`](Claude-Design-Prompt.md) | The Claude Design prompt that turns those screenshots into a modern design canvas of about 11 artboards (Home, Customers, Orders, New order, Products, Reports, Project Gantt, Leads, Campaigns, States, tablet and phone). It uses the ApexCharts design system, Ignite UI for Blazor components, Blazor-ApexCharts and ApexGantt. It takes real sample values from the backend's SQLite database. |
| [`setup.md`](setup.md) | The presenter's setup notes: installing the Ignite UI CLI (`npm install -g igniteui-cli`, `ig ai-config`), scaffolding commands, the ApexGantt CDN script, and the build prompt that asks the AI to implement the Gantt, Customers and Orders screens from the design, against the BusinessApp API. |

Notes on those files:

* `setup.md` mentions the scaffold path from rehearsal (`fontend/claude-sonnet/bclient3`) and the `IgniteUI.Blazor.GridLite`
  package. The finished app lives in [`Frontend/modern-windows-forms`](Frontend/modern-windows-forms) and uses
  `IgniteUI.Blazor.Trial` (the full product, trial build).
* `Claude-Design-Prompt.md` reads `Backend/BusinessApp.Api/App_Data/businessapp.db`. That file isn't in the repo. Run
  the API once to generate it (see below).

## Apps in this folder

| Folder | What | Tech | URL (default) | Talks to |
| --- | --- | --- | --- | --- |
| [`Backend/`](Backend/README.md) | **BusinessApp API**: Sales, Projects (Gantt) and Marketing REST API | ASP.NET Core Minimal API, EF Core, SQLite, **.NET 11** (`net11.0`) | `http://localhost:5080` (https profile adds `https://localhost:7080`) | Local SQLite file `App_Data/businessapp.db`, created and seeded on first run |
| [`Frontend/modern-windows-forms/`](Frontend/modern-windows-forms/README.md) | **Modernized WinForms app**: Blazor Web App, InteractiveWebAssembly, no prerender | Blazor, Ignite UI for Blazor (Trial), Blazor-ApexCharts, ApexGantt, **.NET 10** (`net10.0`) | `http://localhost:5148` (https profile: `https://localhost:7102`) | **BusinessApp API** at `http://localhost:5080/`, called directly from the browser |
| [`StitchToFigmaToReact/`](StitchToFigmaToReact/README.md) | **Aura CRM**: Customers and Orders screens from the Stitch CRM Figma file | React 19, TypeScript, Vite 8, Ignite UI for React + Grid Lite, ApexCharts | `http://localhost:3301` | **Nothing.** Uses mock data in `src/data.ts` |

### Which client talks to which server

```
Browser ── http://localhost:5148 ──▶ modern-windows-forms (Blazor host: serves the WASM app only)
   │
   └──── http://localhost:5080/api/... ──▶ Backend/BusinessApp.Api ──▶ App_Data/businessapp.db (SQLite)
         (fetch from WebAssembly; CORS allows :5148 and :7102)

Browser ── http://localhost:3301 ──▶ StitchToFigmaToReact (Vite dev server, mock data, no API calls)
```

* The Blazor client's API URL is `ApiBaseUrl` in `modern-windows-forms.Client/wwwroot/appsettings.json`
  (`http://localhost:5080/`; `Program.cs` falls back to the same value). See [Connecting to the backend](Frontend/modern-windows-forms/README.md#connecting-to-the-backend).
* The API's allowed browser origins are `Cors:AllowedOrigins` in `Backend/BusinessApp.Api/appsettings.json`. See
  [CORS](Backend/README.md#cors).

## Prerequisites

| Tool | Needed for |
| --- | --- |
| [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0), 11.0.100-rc.3 or later 11.0.x (pinned by `Backend/global.json`) | Backend API |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (or at least the ASP.NET Core 10 runtime alongside the .NET 11 SDK) | Blazor client |
| [Node.js](https://nodejs.org/) 22.12+ | React app (and the optional Ignite UI MCP servers / CLI) |
| Internet access | NuGet/npm restore, plus Google Fonts and the ApexGantt CDN script used by the Blazor app |

## Quick start: WinForms → Blazor

Open two terminals.

**Terminal 1: API**

```bash
cd "Demo 3/Backend/BusinessApp.Api"
dotnet run
```

Wait for `Now listening on: http://localhost:5080`. On the very first run it also creates and seeds
`App_Data/businessapp.db`, which takes a few seconds. Check <http://localhost:5080/health> or browse the route catalog at
<http://localhost:5080/api>.

**Terminal 2: Blazor client**

```bash
cd "Demo 3/Frontend/modern-windows-forms/modern-windows-forms"
dotnet run
```

A browser opens at **http://localhost:5148**. The commands are the same in Windows PowerShell; use `\` in paths if you prefer.

To start over with fresh demo data at any time:

```bash
# macOS / Linux
curl -X POST http://localhost:5080/api/admin/reset-database
```

```powershell
# Windows PowerShell
Invoke-RestMethod -Method Post http://localhost:5080/api/admin/reset-database
```

Or stop the API, delete `Backend/BusinessApp.Api/App_Data/businessapp.db*`, and start it again. The data is deterministic
(fixed random seed) but its dates are anchored to the day it was generated. Details are in the
[backend README](Backend/README.md#database-creation-seeding-and-reset).

## Quick start: Stitch → Figma → React

```bash
cd "Demo 3/StitchToFigmaToReact"
npm ci
npm run dev
```

Open **http://localhost:3301**. You don't need the API for this one.

## Folder layout

```
Demo 3/
  README.md                     This file
  Claude-Design-Prompt.md       Claude Design prompt (WinForms screenshots → modern design canvas)
  setup.md                      Presenter setup notes and build prompt
  Windows Forms App/            Screenshots of the original WinForms app
  Backend/                      BusinessApp API (.NET 11, SQLite)            → Backend/README.md
  Frontend/modern-windows-forms Blazor + Ignite UI client (.NET 10)          → Frontend/modern-windows-forms/README.md
  StitchToFigmaToReact/         Aura CRM React app (mock data)               → StitchToFigmaToReact/README.md
```

## Common problems

| Symptom | Where to look |
| --- | --- |
| Blazor screens say "Can't reach the server" | API not running on `http://localhost:5080`, or `ApiBaseUrl` mismatch. See the [Blazor README](Frontend/modern-windows-forms/README.md#troubleshooting). |
| `blocked by CORS policy` in the browser console | Client origin missing from `Cors:AllowedOrigins`. See the [backend README](Backend/README.md#cors). |
| `global.json` / SDK not found when running the API | Install the .NET 11 SDK (RC 3 or later). |
| Blazor app won't start: framework `Microsoft.AspNetCore.App 10.0.x` not found | Install the .NET 10 SDK or runtime. |
| React app fails on install or start | Use Node.js 22.12+. |
