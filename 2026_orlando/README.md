# VSLive: AI-Assisted App Building with Ignite UI

Demo code from a VSLive session on building real business apps with AI coding assistants (Claude and others) and **Infragistics Ignite UI**, for React and Blazor. Each demo folder has its own README with the full story, setup steps and troubleshooting. This page tells you which demo is which and how to run it.

| Demo | What it shows | Apps | Backend |
|---|---|---|---|
| [**Demo 1**](Demo%201/README.md): one brief, three AI models | The same design brief given to three models (Claude Haiku, Claude Opus, MAI-Code-2). Each built a railway maintenance **dispatch console**. | 3 × React + Vite + Ignite UI for React | **Dispatch API** (ASP.NET Core .NET 10 + SQLite) |
| [**Demo 2**](Demo%202/README.md): Figma designs → working apps | Two Figma designs implemented with Ignite UI: a wellness tracker and a team collaboration workspace | React + Ignite UI for React, Blazor WASM + Ignite UI for Blazor Lite | None (in-memory mock data) |
| [**Demo 3**](Demo%203/README.md): modernizing apps | A Windows Forms business app rebuilt as a modern Blazor app, plus a Stitch → Figma → React CRM | Blazor WASM + Ignite UI for Blazor, React + Ignite UI for React | **BusinessApp API** (ASP.NET Core .NET 11 RC + SQLite) |
| [**Demo 4**](Demo%204/README.md): design system | A Claude Design prompt that builds an Ignite UI design system (Bootstrap theme, light and dark) | None (prompt only) | None |

## Which client talks to which server

```
Demo 1   Haiku/dispatcher       ──(direct, CORS)──┐
         Opus/dispatcher        ──(Vite proxy)────┼──▶  Backend/DispatchApi       http://localhost:5207
         MAI-Code-2/dispatcher  ──(Vite proxy)────┘       └─ database/dispatch.db  (restore from .gz first)

Demo 2   LiveDemo/livedemo1     ──▶  (no backend, mock data)
         TeamCollaboration      ──▶  (no backend, mock data)

Demo 3   Frontend/modern-windows-forms  ──(direct, CORS)──▶  Backend/BusinessApp.Api   http://localhost:5080
                                                               └─ App_Data/businessapp.db  (auto-created on first run)
         StitchToFigmaToReact           ──▶  (no backend, mock data)
```

The two backends are separate. Demo 1's frontends only work with the Dispatch API, and Demo 3's Blazor app only works with the BusinessApp API.

## Ports at a glance

| App | Start command | URL |
|---|---|---|
| Demo 1 · Dispatch API | `dotnet run` | http://localhost:5207 (Swagger at `/swagger`) |
| Demo 1 · Haiku dispatcher | `npm start` | http://localhost:3101 |
| Demo 1 · Opus dispatcher | `npm start` | http://localhost:3102 |
| Demo 1 · MAI-Code-2 dispatcher | `npm start` | http://localhost:3103 |
| Demo 2 · livedemo1 | `npm start` | http://localhost:3201 |
| Demo 2 · TeamCollaboration | `dotnet run` | http://localhost:5223 |
| Demo 3 · BusinessApp API | `dotnet run` | http://localhost:5080 |
| Demo 3 · modern-windows-forms (Blazor) | `dotnet run` | http://localhost:5148 |
| Demo 3 · StitchToFigmaToReact | `npm run dev` | http://localhost:3301 |

Every app has its own port, so you can run all of them at once. The React apps follow a simple scheme, **3‹demo›0‹n›**: Demo 1 uses 3101–3103, Demo 2 uses 3201 and Demo 3 uses 3301. Each Vite config sets `strictPort`, so if a port is already taken, Vite stops with an error instead of quietly moving to another port, where the API's CORS rules would block it.

## Prerequisites

| Tool | Version | Needed for |
|---|---|---|
| [Node.js](https://nodejs.org/) | **22.12 or later** (Vite 8 and Ignite UI require it) | Every React app |
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0** | Demo 1 API, Demo 2 TeamCollaboration, Demo 3 Blazor client |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/11.0) | **11.0 RC** (pinned by `Demo 3/Backend/global.json`) | Demo 3 BusinessApp API |
| [Python](https://www.python.org/) | 3.7 or later, standard library only | Only if you regenerate Demo 1's database instead of restoring it |

You also need internet access for the first `npm install` and NuGet restore. The apps also load Google Fonts, and Demo 3's Blazor app loads ApexGantt from a CDN.

## Quick start

### Demo 1: dispatch console (API + one frontend)

```bash
# 1. Restore the database (one time). It's committed compressed.
cd "Demo 1/Backend/database"
gunzip -k dispatch.db.gz          # Windows: see Demo 1/Backend/README.md

# 2. Start the API (terminal 1)
cd ../DispatchApi
dotnet run                        # → http://localhost:5207

# 3. Start one of the frontends (terminal 2)
cd "Demo 1/Opus/dispatcher"       # or Haiku/dispatcher (3101), MAI-Code-2/dispatcher (3103)
npm install
npm start                         # → http://localhost:3102
```

### Demo 2: Figma → apps (no backend)

```bash
cd "Demo 2/LiveDemo/livedemo1" && npm install && npm start     # → http://localhost:3201
cd "Demo 2/TeamCollaboration"  && dotnet run                   # → http://localhost:5223
```

### Demo 3: WinForms → Blazor (API + client)

```bash
# Terminal 1: the API creates and seeds App_Data/businessapp.db on first run
cd "Demo 3/Backend/BusinessApp.Api"
dotnet run                        # → http://localhost:5080

# Terminal 2: the Blazor client
cd "Demo 3/Frontend/modern-windows-forms/modern-windows-forms"
dotnet run                        # → http://localhost:5148

# Optional: the Stitch → Figma → React app (no backend)
cd "Demo 3/StitchToFigmaToReact" && npm install && npm run dev   # → http://localhost:3301
```

## Databases

Neither SQLite database is committed as a plain `.db` file (`*.db` is in `.gitignore`):

| Database | How you get it | Reset |
|---|---|---|
| Demo 1 `dispatch.db` (~65 MB) | Unzip the committed `dispatch.db.gz` (~12 MB), or regenerate it with `python3 generate_db.py` | Unzip the `.gz` again. The API changes the database while it runs (date rebasing, user actions). |
| Demo 3 `businessapp.db` (~3 MB) | Created automatically by EF Core migrations and seeded on the API's first run | `POST /api/admin/reset-database`, or stop the API and delete `App_Data/businessapp.db*` |

See [Demo 1/Backend/README.md](Demo%201/Backend/README.md) and [Demo 3/Backend/README.md](Demo%203/Backend/README.md) for details.

## AI tooling in this repo

These demos were built with AI coding agents. The configuration they used is committed so you can reproduce the workflow:

- **`.mcp.json`** (root and in several app folders): registers the [Ignite UI CLI MCP server](https://www.npmjs.com/package/igniteui-cli) (component docs and API reference) and the Ignite UI Theming MCP server. Both run through `npx`, need no API keys, and are picked up automatically by Claude Code and other MCP-aware agents.
- **`.agents/skills/`, `.claude/skills/`, `.github/skills/`**: Ignite UI agent skills (task-focused instructions for components, grids, theming and so on) for Copilot, Claude Code and other agents.
- **`AGENTS.md` / `CLAUDE.md`**: per-project instructions for the agents.
- The prompts and briefs used on stage are in each demo folder (`brief.md`, `project-setup.md`, `setup.md`, `Claude-Design-Prompt.md`, and so on). Each demo README links them.

## Repository layout

```
VSLive/
├── Demo 1/              Dispatch console: shared API + three model-built React frontends
│   ├── Backend/         DispatchApi (.NET 10) + database/ (dispatch.db.gz, generate_db.py, schema.sql)
│   ├── Haiku/           dispatcher built by Claude Haiku
│   ├── Opus/            dispatcher built by Claude Opus
│   └── MAI-Code-2/      dispatcher built by MAI-Code-2
├── Demo 2/              Figma → apps
│   ├── LiveDemo/        livedemo1 (React wellness tracker) + Ignite UI CLI helper package.json
│   └── TeamCollaboration/   Blazor WASM team workspace
├── Demo 3/              Modernization
│   ├── Backend/         BusinessApp.Api (.NET 11 RC + SQLite)
│   ├── Frontend/        modern-windows-forms (Blazor WASM client)
│   ├── StitchToFigmaToReact/   React CRM screens
│   └── Windows Forms App/      screenshots of the original WinForms app
├── Demo 4/              Design-system prompt
├── .agents/skills/      Ignite UI for React agent skills
├── .mcp.json            Ignite UI MCP servers
└── .gitignore           Ignores bin/, obj/, node_modules/, dist/, *.db, .env, …
```

## Common problems

| Symptom | Fix |
|---|---|
| Demo 1 API fails at startup with a missing-database error | You haven't restored the database yet. Run `gunzip -k dispatch.db.gz` in `Demo 1/Backend/database`. |
| A frontend loads, but every API call fails with a CORS error | The page isn't on a port the API allows. Use the ports in the table above, or add your origin to `Cors` in the API's `appsettings.json` and restart the API. |
| `npm install` fails with engine errors | Upgrade to Node 22.12 or later. |
| Demo 3 Blazor app can't reach the API | Check that the API is on `http://localhost:5080`, or change `ApiBaseUrl` in `modern-windows-forms.Client/wwwroot/appsettings.json`. |
| Demo 3 API: "A compatible .NET SDK was not found" | Install the .NET 11 RC SDK. `Demo 3/Backend/global.json` pins it. |
| `Error: Port 31xx is already in use` | A previous run of that same app is still running. Stop it (Ctrl+C in its terminal), or find it with `lsof -i :31xx` (macOS/Linux) or `netstat -ano \| findstr :31xx` (Windows). |

Each app's README has a fuller troubleshooting section.
