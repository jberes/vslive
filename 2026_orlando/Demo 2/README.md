# Demo 2: Figma designs to working apps with Ignite UI

This demo shows AI-assisted app building from Figma designs with Infragistics Ignite UI. It contains two small, self-contained front-end apps, each a working implementation of a Figma design:

- A **React** health and wellness tracker ("Healthy") built with Ignite UI for React.
- A **Blazor WebAssembly** team collaboration workspace built with Ignite UI for Blazor Lite.

Neither app calls a backend API. All data is fictional, held in memory in the browser, and reset when you reload the page. You do not need to start anything from `Demo 1` or `Demo 3` to run these apps.

## Apps

| Folder | What it is | Tech | Dev URL | Backend |
| --- | --- | --- | --- | --- |
| [`LiveDemo/livedemo1`](LiveDemo/livedemo1/README.md) | "Healthy" wellness tracker: home dashboard, activity, nutrition, sleep and mental wellness pages | React 19, TypeScript, Vite 8, Ignite UI for React (`igniteui-react` 19.9), ApexCharts | http://localhost:3201 | None (mock data in `src/app/wellness-data.ts` and page components) |
| [`TeamCollaboration`](TeamCollaboration/README.md) | Team collaboration workspace: dashboards gallery and "My Tasks" board with calendar | .NET 10 Blazor WebAssembly (standalone), Ignite UI for Blazor Lite (`IgniteUI.Blazor.Lite` 1.0.1) | http://localhost:5223 (or https://localhost:7016) | None (in-memory `Services/WorkspaceState.cs`) |

[`LiveDemo`](LiveDemo/README.md) itself is not an app. Its `package.json` only installs the Ignite UI CLI (`igniteui-cli`), which was used to scaffold `livedemo1`. See [LiveDemo/README.md](LiveDemo/README.md).

## Prerequisites

| App | You need |
| --- | --- |
| `LiveDemo/livedemo1` | [Node.js](https://nodejs.org/) 22.12 or later (npm is included) and internet access for `npm install` and the Google Fonts stylesheet |
| `TeamCollaboration` | [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and internet access for the first NuGet restore |

## Quick start

Commands are the same on macOS, Linux and Windows unless noted. Run them from this `Demo 2` folder.

### React app (`LiveDemo/livedemo1`)

```sh
cd LiveDemo/livedemo1
npm install
npm start
```

Vite opens http://localhost:3201 in your browser. There is no `npm run dev` script; use `npm start`. Details: [LiveDemo/livedemo1/README.md](LiveDemo/livedemo1/README.md).

### Blazor app (`TeamCollaboration`)

```sh
dotnet run --project TeamCollaboration/TeamCollaboration.csproj
```

On Windows you can also use backslashes: `dotnet run --project TeamCollaboration\TeamCollaboration.csproj`.

The app starts at http://localhost:5223 and opens a browser. For HTTPS, add `--launch-profile https` (serves https://localhost:7016). Details: [TeamCollaboration/README.md](TeamCollaboration/README.md).

You can run both apps at the same time; their ports do not overlap.

## How the apps relate

The two apps share no code, data or services. They are parallel examples of the same workflow on two platforms:

- Each one implements a Figma design (the React app's README links its source design, the Health and Wellness Tracker Figma file; the Blazor app's images live in `TeamCollaboration/wwwroot/assets/figma`).
- Each one uses the Ignite UI component library for its platform (`Igr*` components in React, `Igb*` components in Blazor) with the Material theme.
- Features that the design shows but that have no data behind them are either simulated with demo dialogs (React) or rendered as disabled controls with a "not available in this demo" tooltip (Blazor).

`LiveDemo/livedemo1` also contains AI-assistant configuration from the Ignite UI CLI template: `AGENTS.md` (also copied to `.claude/CLAUDE.md` and `.github/copilot-instructions.md`), Ignite UI for React agent skills under `.agents/skills`, `.claude/skills` and `.github/skills`, and MCP server settings in `.mcp.json` and `.vscode/mcp.json`. These are used by coding assistants, not by the running app.

## Folder layout

```text
Demo 2/
├── Demo 2.code-workspace      VS Code workspace that opens this folder
├── LiveDemo/
│   ├── package.json           Installs igniteui-cli only (scaffolding helper)
│   ├── package-lock.json
│   └── livedemo1/             React + Vite + Ignite UI for React app
└── TeamCollaboration/         Blazor WebAssembly + Ignite UI for Blazor Lite app
```

Open `Demo 2.code-workspace` in VS Code to work with both apps in one window.

## Not committed

The repository `.gitignore` excludes `node_modules/`, `dist/`, `bin/` and `obj/`, so a fresh clone has no installed packages or build output. `npm install` and `dotnet run` (which restores and builds) recreate them.
