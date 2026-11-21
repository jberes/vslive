# Team Collaboration

A standalone .NET 10 Blazor WebAssembly app based on the Team Collaboration Figma design. It uses the open-source (MIT) [Ignite UI for Blazor Lite](https://www.nuget.org/packages/IgniteUI.Blazor.Lite) components with the Material theme.

Part of [Demo 2](../README.md).

## What the app does

| Route | Page | Features |
| --- | --- | --- |
| `/` (also `/dashboard`) | Dashboards | Dashboard cards with Dashboards / Favorites / Recent / Samples views; star a dashboard to add it to Favorites |
| `/tasks` | My Tasks | Task sections grouped by list, list filter in the sidebar, add and edit tasks in a validated dialog, calendar with week numbers, and a release calendar |
| `/not-found` | Not Found | Shown for unknown routes |

Use the navigation rail on the left or the **Home** menu in the header to move between pages. The **+ Task** button in the header opens the new-task dialog from any page.

Controls for features without a backing service (notifications, chat, sharing, adding dashboards, filtering, organizations, projects, files and list/section/field settings) are disabled and show a "not available in this demo" tooltip.

The Figma Tasks frame overlaps its toolbar and shows an inconsistent calendar date. In this app the toolbar is laid out without overlap, and the calendar starts on May 15, 2023, so its selected weekday and dates are consistent.

### Data source

There is **no backend**. The app makes no HTTP calls; the dashboards and tasks are sample data held in memory by `Services/WorkspaceState.cs`, which is registered as a singleton in `Program.cs`. Favoriting a dashboard and adding or editing a task work during the current session, but changes are reset when the page is reloaded.

## Hosting model

This is a **standalone Blazor WebAssembly** app (`Microsoft.NET.Sdk.BlazorWebAssembly`). The whole app runs in the browser. There is no ASP.NET Core server project: during development, `Microsoft.AspNetCore.Components.WebAssembly.DevServer` serves the static files, and in production the published `wwwroot` can be hosted on any static file host.

## Prerequisites

- **.NET 10 SDK** (the project targets `net10.0`). [Download .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).
- Internet access for the first NuGet restore.
- For the HTTPS profile only: a trusted ASP.NET Core development certificate (see [Troubleshooting](#troubleshooting)).

Check your installed SDKs:

```sh
dotnet --list-sdks
```

### NuGet packages

From `TeamCollaboration.csproj`:

| Package | Version | Purpose |
| --- | --- | --- |
| `IgniteUI.Blazor.Lite` | 1.0.1 | Ignite UI for Blazor Lite components (`IgbButton`, `IgbAvatar`, `IgbCard`, `IgbList`, `IgbCalendar`) and themes |
| `Microsoft.AspNetCore.Components.WebAssembly` | 10.0.14 | Blazor WebAssembly runtime |
| `Microsoft.AspNetCore.Components.WebAssembly.DevServer` | 10.0.14 | Development server used by `dotnet run` |

The Lite package serves its static assets under `_content/IgniteUI.Blazor/`, which is why `wwwroot/index.html` links `_content/IgniteUI.Blazor/themes/light/material.css`.

## Run

From the `Demo 2` folder:

```sh
dotnet run --project TeamCollaboration/TeamCollaboration.csproj
```

Or from this folder:

```sh
dotnet run
```

`dotnet run` restores packages, builds and starts the app using the first profile in `Properties/launchSettings.json` (`http`), then opens a browser.

| Profile | Command | URL(s) |
| --- | --- | --- |
| `http` (default) | `dotnet run` | http://localhost:5223 |
| `https` | `dotnet run --launch-profile https` | https://localhost:7016 and http://localhost:5223 |

The commands are the same on macOS, Linux and Windows (on Windows, `TeamCollaboration\TeamCollaboration.csproj` also works).

For hot reload while editing:

```sh
dotnet watch
```

## Publish

```sh
dotnet publish -c Release
```

The static site is written to `bin/Release/net10.0/publish/wwwroot`. When hosting it, configure the server to fall back to `index.html` for client-side routes such as `/tasks`.

## Project structure

```text
TeamCollaboration/
├── TeamCollaboration.csproj   net10.0, Blazor WebAssembly SDK, package references
├── Program.cs                 Host setup: root components, AddIgniteUIBlazor(), WorkspaceState singleton
├── App.razor                  Router (NotFoundPage = Pages/NotFound)
├── _Imports.razor             Global usings, including IgniteUI.Blazor.Controls
├── Layout/
│   └── MainLayout.razor       Header, Home menu, navigation rail, new/edit task dialog
├── Pages/
│   ├── Home.razor             Dashboards page (/ and /dashboard)
│   ├── Tasks.razor            My Tasks page (/tasks)
│   └── NotFound.razor
├── Shared/
│   └── TaskSection.razor      A task group with its list and "+ Task" button
├── Services/
│   └── WorkspaceState.cs      In-memory dashboards and task groups, change notification
├── Properties/
│   └── launchSettings.json    http / https launch profiles and ports
└── wwwroot/
    ├── index.html             Host page, Ignite UI Material theme link
    ├── css/app.css            App styles
    ├── assets/figma/          Icons, avatars and previews exported from the Figma design
    └── assets/fonts/          Self-hosted Titillium Web (SIL Open Font License, see OFL.txt)
```

## Troubleshooting

**`error NETSDK1045: The current .NET SDK does not support targeting .NET 10.0`.** Install the .NET 10 SDK, then check `dotnet --list-sdks`.

**Port 5223 or 7016 is already in use.** Stop the other process, or change `applicationUrl` in `Properties/launchSettings.json`. To find what is using a port:

```sh
# macOS / Linux
lsof -i :5223
```

```powershell
# Windows
netstat -ano | findstr :5223
```

**The browser warns about the certificate with the `https` profile.** Trust the development certificate once:

```sh
dotnet dev-certs https --trust
```

On Linux, `--trust` is not supported for all browsers; use the `http` profile or follow the [ASP.NET Core HTTPS documentation](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl) for your distribution.

**Components render without styles.** Make sure `wwwroot/index.html` still links `_content/IgniteUI.Blazor/themes/light/material.css` and that `Program.cs` calls `builder.Services.AddIgniteUIBlazor()`.

**Changes disappear after reloading.** This is expected: all data is in memory and resets on reload.

**A route returns 404 after deployment.** Configure the static host to serve `index.html` for unknown paths.
