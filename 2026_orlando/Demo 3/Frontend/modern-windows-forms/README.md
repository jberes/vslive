# Modern Windows Forms (Blazor + Ignite UI)

A **Blazor Web App** that modernizes the WinForms "Outlook-inspired" business app (screenshots in
[`../../Windows Forms App`](../../Windows%20Forms%20App)) as a responsive web UI. It's built with **Ignite UI for Blazor**,
**Blazor-ApexCharts** and the **ApexGantt** JavaScript library. All data comes from the
[BusinessApp API](../../Backend/README.md) in `Demo 3/Backend`.

Screens: Home dashboard, Customers (list + detail), Orders (status tabs, expandable order lines, ship dialog),
New order, Products & categories, Reports hub, Project schedule (Gantt), Leads pipeline board, Campaigns, plus a
"States" sheet (loading/empty/error/conflict states) and a phone "More" tab.

## How it's hosted

The solution [`modern-windows-forms.sln`](modern-windows-forms.sln) has two projects:

| Project | SDK / role |
| --- | --- |
| [`modern-windows-forms`](modern-windows-forms) | `Microsoft.NET.Sdk.Web`, the **server/host**. It serves the host page ([`Components/App.razor`](modern-windows-forms/Components/App.razor)), static assets (`wwwroot`) and the WebAssembly bundle. **This is the project you run.** |
| [`modern-windows-forms.Client`](modern-windows-forms.Client) | `Microsoft.NET.Sdk.BlazorWebAssembly`, all pages, layout, models and the API client. It runs **in the browser** on WebAssembly. |

* **Render mode**: `InteractiveWebAssembly` with **prerendering disabled**
  (`new InteractiveWebAssemblyRenderMode(prerender: false)` on `<Routes>` and `<HeadOutlet>` in `App.razor`).
  Every page runs client-side, and the server never renders pages or calls the API.
* **Data access**: The browser calls the BusinessApp API directly over HTTP (`HttpClient` registered in the client's
  [`Program.cs`](modern-windows-forms.Client/Program.cs), wrapped by [`Services/ApiClient.cs`](modern-windows-forms.Client/Services/ApiClient.cs)).
  Because the client and API run on different origins, the API must allow the client's origin via CORS.

## Prerequisites

| Requirement | Details |
| --- | --- |
| .NET 10 SDK | Both projects target `net10.0` (ASP.NET Core packages `10.0.14`). There's no `global.json` here, so the newest installed SDK builds it. The **.NET 10 runtime** must be installed to run it. <https://dotnet.microsoft.com/download/dotnet/10.0> |
| .NET 11 SDK | Only needed for the [backend](../../Backend/README.md), which targets `net11.0`. Installing both side by side is fine. |
| Internet access | The host page loads Google Fonts, plus ApexGantt (`apexgantt@3.22.0` and its Ignite UI add-on) from `cdn.jsdelivr.net`. NuGet packages restore from nuget.org. |
| Running BusinessApp API | Every screen loads data from `http://localhost:5080` by default. |

### NuGet packages

From [`modern-windows-forms.Client.csproj`](modern-windows-forms.Client/modern-windows-forms.Client.csproj):

| Package | Version | Notes |
| --- | --- | --- |
| `IgniteUI.Blazor.Trial` | 26.1.98 | The **trial** build of the full Ignite UI for Blazor product (Grid, Combo, NavDrawer, Dialog, and so on). It's published on nuget.org, so you don't need a custom NuGet feed or `nuget.config` to restore it. Trial terms apply. Licensed Infragistics customers would use the licensed `IgniteUI.Blazor` package from the Infragistics private feed instead. |
| `Blazor-ApexCharts` | 8.0.0 | Charts (`ApexChart`, `ApexPointSeries`) |
| `Microsoft.AspNetCore.Components.WebAssembly` | 10.0.14 | |

The server project references `Microsoft.AspNetCore.Components.WebAssembly.Server` 10.0.14.

ApexGantt has no Blazor wrapper. It's loaded from the CDN in `App.razor` and driven through JS interop in
[`wwwroot/js/gantt-interop.js`](modern-windows-forms/wwwroot/js/gantt-interop.js).

Ignite UI is registered in both `Program.cs` files with `AddIgniteUIBlazor(...)`. The Bootstrap light theme and grid
theme (`_content/IgniteUI.Blazor/themes/light/bootstrap.css`, `.../themes/grid/light/bootstrap.css`) and
`_content/IgniteUI.Blazor/app.bundle.js` are linked in `App.razor`.

## Run it

Start the API first (see the [backend README](../../Backend/README.md)), in its own terminal:

```bash
cd "Demo 3/Backend/BusinessApp.Api"
dotnet run                                   # http://localhost:5080
```

Then start the Blazor host project:

```bash
cd "Demo 3/Frontend/modern-windows-forms/modern-windows-forms"
dotnet run                                   # http://localhost:5148, opens a browser
```

To use HTTPS instead:

```bash
dotnet run --launch-profile https            # https://localhost:7102 and http://localhost:5148
```

In Visual Studio / Rider, open `modern-windows-forms.sln` and set **`modern-windows-forms`** (the server project, not
`.Client`) as the startup project.

| Profile ([`launchSettings.json`](modern-windows-forms/Properties/launchSettings.json)) | URLs | Browser |
| --- | --- | --- |
| `http` (default) | `http://localhost:5148` | Opens automatically |
| `https` | `https://localhost:7102`, `http://localhost:5148` | Opens automatically |

All four client origins (`localhost` and `127.0.0.1` on 5148 and 7102) are already in the API's
`Cors:AllowedOrigins`.

## Connecting to the backend

The API base URL is the `ApiBaseUrl` setting in [`modern-windows-forms.Client/wwwroot/appsettings.json`](modern-windows-forms.Client/wwwroot/appsettings.json). Blazor WebAssembly loads this file in the browser:

```json
{
  "ApiBaseUrl": "http://localhost:5080/"
}
```

[`modern-windows-forms.Client/Program.cs`](modern-windows-forms.Client/Program.cs) reads it, falling back to the same URL if the setting is missing:

```csharp
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080/";
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBase) });
```

`http://localhost:5080/` is the API's `http` profile.

**If the API runs on a different URL**, change `ApiBaseUrl` in that file (for example `"http://localhost:5090/"`) and reload the page.

* Keep the **trailing slash**. Requests use relative paths like `api/customers`, which resolve against the base address.
* The server project's `appsettings.json` is **not** used for this, because the code runs in the browser, not on the server.

**If the client runs on a different port** (for example you changed `launchSettings.json` or used `--urls`), add that
origin to `Cors:AllowedOrigins` in [`Backend/BusinessApp.Api/appsettings.json`](../../Backend/BusinessApp.Api/appsettings.json)
and restart the API. See the [backend README's CORS section](../../Backend/README.md#cors).

**HTTPS note**: With the `https` profile, the page is served from `https://localhost:7102` but still calls
`http://localhost:5080`. Most browsers allow this for `localhost`, but if yours blocks it as mixed content, run the API with
`--launch-profile https` and set `ApiBaseUrl` to `https://localhost:7080/`. Or just use the default `http` profiles for both.

If the API isn't reachable, screens show a "Can't reach the server" error that includes the base URL being used.

## Pages

Routes are defined in [`modern-windows-forms.Client/Pages`](modern-windows-forms.Client/Pages). The navigation structure
is in [`Layout/NavModel.cs`](modern-windows-forms.Client/Layout/NavModel.cs).

| Route | Page | Notes |
| --- | --- | --- |
| `/` | `Home.razor` | KPIs, monthly revenue, sales by category, recent orders, needs attention, active projects, live campaigns |
| `/customers` | `Customers.razor` + `Customers/CustomerDetail.razor` | Grid with search, filters, paging; detail panel |
| `/orders` | `Orders.razor` + `Orders/OrderDetailPanel.razor` | Status tabs, filter chips, order lines, ship order |
| `/orders/new` | `NewOrder.razor` | Customer combo, dates, ship-via, editable lines, server validation errors |
| `/products` | `Products.razor` | Categories, KPIs, quarterly chart, stock vs reorder grid |
| `/reports`, `/reports/{report}` | `Reports.razor` | One hub for the WinForms report windows. Keys: `year`, `quarter`, `category`, `monthly`, `qorders`, `psales`, `topc`, `topp`, `country` |
| `/projects` | `Projects.razor` + `ProjectSchedule/*` | Task grid + ApexGantt timeline, task detail panel, phase list on phones |
| `/leads` | `Leads.razor` + `Marketing/*` | Pipeline board, convert-to-customer dialog |
| `/campaigns` | `Campaigns.razor` | KPIs, campaign grid, funnel, spend by channel |
| `/states` | `States.razor` | Prototype sheet of loading/empty/error/409/404/validation/toast states |
| `/more` | `More.razor` | Phone "More" tab |
| `/not-found` | `NotFound.razor` | 404 page |

## Project structure

```
modern-windows-forms/
  modern-windows-forms.sln
  modern-windows-forms/                 Server/host project (run this one)
    Program.cs                          Razor components + InteractiveWebAssembly, Ignite UI, static assets
    Components/App.razor                Host page: themes, fonts, CDN scripts, render mode (no prerender)
    Components/Pages/Error.razor
    Properties/launchSettings.json      Ports 5148 / 7102
    wwwroot/
      app.css                           App-wide styles
      css/ignite-palette.css            Ignite UI palette mapped to the ApexCharts design system
      css/tokens.css                    Design tokens
      js/gantt-interop.js               ApexGantt JS interop
  modern-windows-forms.Client/          WebAssembly project (all UI)
    Program.cs                          Ignite UI modules, ApexCharts defaults, HttpClient → API base URL
    Routes.razor
    Layout/                             MainLayout (NavDrawer + Navbar, responsive), NavModel
    Pages/                              Screens (see table above) and their sub-components
    Models/                             DTOs per area (Customers, Orders, Products, Projects, Marketing, Reports, Home)
    Services/ApiClient.cs               Typed HttpClient wrapper, ProblemDetails → ApiException
    Services/NotificationService.cs     Toasts / snackbars
    Shared/                             StatusChip, icons, formatting helpers
  AGENTS.md, .claude/, .github/         Instructions for AI coding assistants
  .agents/skills/                       Ignite UI for Blazor agent skills
  .mcp.json, .vscode/mcp.json           Ignite UI MCP servers (igniteui-cli, igniteui-theming via npx)
```

The AI assistant files are what the demo used to generate the app. You don't need them to build or run it. The MCP
servers in `.mcp.json` / `.vscode/mcp.json` start with `npx`, so they need Node.js if you want to use them.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Every screen shows "Can't reach the server" | The API isn't running, or it's on a different URL. Start it (`dotnet run` in `Backend/BusinessApp.Api`) and check `http://localhost:5080/health`. Otherwise set `ApiBaseUrl` (see [above](#connecting-to-the-backend)). |
| Browser console: `blocked by CORS policy` | The origin you're browsing from isn't in the API's `Cors:AllowedOrigins`. Add it and restart the API. Note that `localhost` and `127.0.0.1` are different origins. |
| `You must install or update .NET to run this application` / framework `Microsoft.AspNetCore.App 10.0.x` not found | Install the .NET 10 SDK or runtime. Having only .NET 11 isn't enough to *run* a `net10.0` app. |
| Running the `.Client` project directly fails or does nothing useful | Run the **server** project `modern-windows-forms/`. The client project has no launch profile of its own. |
| Project schedule (Gantt) is empty but other pages work | ApexGantt is loaded from `cdn.jsdelivr.net`. Check network access and the browser console. |
| Components render unstyled | Make sure the `_content/IgniteUI.Blazor/...` CSS and `app.bundle.js` load (Network tab), and that NuGet restore succeeded for `IgniteUI.Blazor.Trial`. |
| Data differs from screenshots or slides | The API's seed data is anchored to the day the database was generated. Reset it with `POST http://localhost:5080/api/admin/reset-database`. |
| `https` certificate warnings | Run `dotnet dev-certs https --trust`, or use the default `http` profile. |
