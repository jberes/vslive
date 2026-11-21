# BusinessApp API

ASP.NET Core Minimal API + EF Core + SQLite backend for the modernized version of the WinForms
"Outlook-inspired" business app shown in [`../Windows Forms App`](../Windows%20Forms%20App). The Blazor client in
[`../Frontend/modern-windows-forms`](../Frontend/modern-windows-forms) is the app that calls this API.

It covers three modules:

| Module | Screens it backs |
| --- | --- |
| **Sales** (Northwind-style) | Customers, Products, Categories, Suppliers, Employees, Shippers, Orders/Order Details, Customer/Order/Product dashboards, Quarterly Orders, Product Sales, Sales by Category/Quarter/Year |
| **Projects** | Project Manager Gantt: task grid + chart, WBS, dependencies, resources, ribbon actions (insert, indent/outdent, move, % complete, notes, font, assign resources) |
| **Marketing** | Campaigns, Leads (with activities and lead→customer conversion), marketing dashboard |

## Prerequisites

| Requirement | Details |
| --- | --- |
| .NET 11 SDK | The project targets `net11.0`. [`global.json`](global.json) pins SDK `11.0.100-rc.3.26522.148` with `rollForward: latestFeature` and `allowPrerelease: true`, so you need **.NET 11 SDK 11.0.100-rc.3 or a later 11.0.x SDK** (a release candidate at the time of writing). Download it from <https://dotnet.microsoft.com/download/dotnet/11.0>. |
| Nothing else | SQLite is embedded via `Microsoft.EntityFrameworkCore.Sqlite`. You don't need a database server, and you don't need the `dotnet-ef` tool to run the API. |

Check your SDK from the `Backend` folder (it honors `global.json`):

```bash
cd "Demo 3/Backend"
dotnet --version     # should print 11.0.100-rc.3... or later
```

NuGet packages (from nuget.org): `Microsoft.EntityFrameworkCore.Sqlite` 11.0.0, `Microsoft.EntityFrameworkCore.Design` 11.0.0, `Bogus` 35.6.5.

## Run it

```bash
cd "Demo 3/Backend/BusinessApp.Api"
dotnet run                          # uses the first launch profile, "http"
```

Or pick a launch profile explicitly:

```bash
dotnet run --launch-profile http    # http://localhost:5080
dotnet run --launch-profile https   # https://localhost:7080 and http://localhost:5080
```

You can also open [`BusinessApp.slnx`](BusinessApp.slnx) in Visual Studio / Rider / VS Code (C# Dev Kit) and run the `BusinessApp.Api` project.

| Profile ([`launchSettings.json`](BusinessApp.Api/Properties/launchSettings.json)) | URLs | Environment |
| --- | --- | --- |
| `http` (default) | `http://localhost:5080` | Development |
| `https` | `https://localhost:7080`, `http://localhost:5080` | Development |

Neither profile opens a browser. Once it's running:

* `GET http://localhost:5080/` redirects to `/api`.
* `GET http://localhost:5080/api` returns a live catalog of every route plus the grid-query syntax.
* `GET http://localhost:5080/health` is a health check.

The `https` profile needs a trusted ASP.NET Core development certificate: `dotnet dev-certs https --trust`.

## Database: creation, seeding and reset

The SQLite database file is **not committed** (`*.db` is gitignored at the repo root and in [`Backend/.gitignore`](.gitignore)).
The API creates and fills it on startup.

* **Location**: `ConnectionStrings:BusinessApp` in [`appsettings.json`](BusinessApp.Api/appsettings.json) is
  `Data Source=App_Data/businessapp.db`. The path is relative to the working directory, which is
  `BusinessApp.Api/` when you use `dotnet run` from that folder. The API creates `App_Data/` if it doesn't exist.
* **Schema**: On every start, [`DatabaseInitializer`](BusinessApp.Api/Data/DatabaseInitializer.cs) calls
  `Database.MigrateAsync()`. That applies the EF Core migrations compiled into the app
  ([`Data/Migrations`](BusinessApp.Api/Data/Migrations), currently just `InitialCreate`). It doesn't use `EnsureCreated`. It
  also switches SQLite to WAL mode, so you may see `businessapp.db-wal` / `businessapp.db-shm` next to the database.
* **Seeding**: [`DataSeeder`](BusinessApp.Api/Data/Seed/DataSeeder.cs) runs on every start but only generates data if
  the `Categories` table is empty. On the first run it seeds everything (a few seconds), then recalculates every
  project's Gantt schedule. On later starts it logs `Database already contains data; skipping seed.` and leaves the
  data alone, including any edits you made through the API.
* **Deterministic data**: The data uses Northwind reference data (categories, suppliers, products, employees, shippers) plus
  [Bogus](https://github.com/bchavez/Bogus) with a fixed random seed. The same `Seed` settings always produce the same records.
  Dates are **anchored to the day the database is generated** (`DateTime.Today`), so recent months, overdue orders,
  in-progress projects and live campaigns always exist. A database generated on a different day has the same records
  with shifted dates.

### Seed settings

These live in the `Seed` section of [`appsettings.json`](BusinessApp.Api/appsettings.json):

| Key | Default | Meaning |
| --- | --- | --- |
| `CustomerCount` | `500` | Generated customers |
| `OrderCount` | `5000` | Generated orders (with order lines) |
| `CampaignCount` | `48` | Marketing campaigns |
| `LeadCount` | `2000` | Marketing leads |
| `RandomSeed` | `8675309` | Seed for the random generators. Change it to get a different (still deterministic) data set |

The settings are read once at startup, and they only take effect when the database is (re)generated.
After changing them, restart the API **and** reset the database (see below).
You can also override them without editing the file, using standard ASP.NET Core environment variables:

```bash
# macOS / Linux
Seed__CustomerCount=100 Seed__OrderCount=1000 dotnet run
```

```powershell
# Windows PowerShell
$env:Seed__CustomerCount = 100; $env:Seed__OrderCount = 1000; dotnet run
```

### Resetting the database

**Option 1: reset endpoint (API running).** `POST /api/admin/reset-database` deletes the database
(`EnsureDeleted`), re-applies migrations and re-seeds it. It returns `{ "reset": true, "elapsedMs": ... }`. If a reset
is already running, it returns `409`.

```bash
# macOS / Linux
curl -X POST http://localhost:5080/api/admin/reset-database
```

```powershell
# Windows PowerShell
Invoke-RestMethod -Method Post http://localhost:5080/api/admin/reset-database
```

`GET /api/admin/stats` shows the full path of the database file and the row count of every table.

**Option 2: delete the file (API stopped).** Stop the API, delete the database files, then start the API again
to regenerate them:

```bash
# macOS / Linux (from Backend/BusinessApp.Api)
rm -f App_Data/businessapp.db App_Data/businessapp.db-wal App_Data/businessapp.db-shm
dotnet run
```

```powershell
# Windows PowerShell (from Backend\BusinessApp.Api)
Remove-Item App_Data\businessapp.db* -ErrorAction SilentlyContinue
dotnet run
```

> The admin endpoints have **no authentication**. They're for local demos only, so don't expose this API publicly as-is.

### Changing the schema (optional)

You only need this if you change the entity model. The EF Core CLI is pinned as a local tool in
[`dotnet-tools.json`](dotnet-tools.json) (`dotnet-ef` 11.0.0):

```bash
cd "Demo 3/Backend"
dotnet tool restore
cd BusinessApp.Api
dotnet ef migrations add <Name> -o Data/Migrations
```

The next `dotnet run` applies the new migration automatically.

## CORS

Browsers call this API directly from other origins, so the allowed origins are listed in `Cors:AllowedOrigins` in
[`appsettings.json`](BusinessApp.Api/appsettings.json):

| Origin | Used by |
| --- | --- |
| `http://localhost:5148`, `http://127.0.0.1:5148` | Blazor client, `http` profile |
| `https://localhost:7102`, `https://127.0.0.1:7102` | Blazor client, `https` profile |
| `http://localhost:3301`, `http://127.0.0.1:3301` | `StitchToFigmaToReact` (doesn't call the API today; listed so it can) |
| `http://localhost:5173`, `http://127.0.0.1:5173` | Vite's default port (unused by Demo 3 today) |
| `http://localhost:3000` | Common React dev port (also unused by Demo 3 today) |

If `Cors:AllowedOrigins` is missing, the code falls back to `http://localhost:5173` and `http://127.0.0.1:5173`. The policy
allows any header and method, and exposes `Content-Disposition`.

**To add an origin** (for example if you run the client on a different port), append it to the array and restart the API:

```json
"Cors": {
  "AllowedOrigins": [ "...existing entries...", "http://localhost:5200" ]
}
```

The origin must match exactly: scheme, host and port, with no trailing slash. `localhost` and `127.0.0.1` are different
origins. You can also add one with an environment variable that uses the next free array index, for example
`Cors__AllowedOrigins__7=http://localhost:5200`.

## Conventions

* JSON is camelCase. Enums are strings (`"status": "Active"`).
* Money is returned as numbers rounded to 2 decimals. Dates are ISO-8601 without an offset (local business dates).
* Errors are RFC 7807 `ProblemDetails`: `400` validation (`errors` dictionary), `404`, `409` conflicts
  (e.g. deleting a customer that has orders, circular task dependencies).
* Entities with pictures expose `avatarUrl` → `/api/avatars/{customers|employees|leads|resources|suppliers}/{id}.svg`
  (deterministic cartoon SVG, cacheable).

### Grid query

All list endpoints that return `{ items, totalCount, page, pageSize, totalPages }` accept these parameters:

| Parameter | Example | Notes |
| --- | --- | --- |
| `page`, `pageSize` | `page=2&pageSize=50` | 1-based. `pageSize` default 25, max 1000 |
| `sort` | `sort=-orderDate,customerName` | `-field` or `field:desc` for descending. Any DTO field |
| `search` | `search=alfred` | Case-insensitive across the grid's main text columns |
| `filter` (repeatable) | `filter=unitPrice:gt:20&filter=categoryName:in:Beverages\|Seafood` | ops: `eq ne contains startswith endswith gt ge lt le in null notnull` |

Unknown fields or bad values return `400` with the list of valid fields.

## Endpoints

Each resource has its own file in [`BusinessApp.Api/Endpoints`](BusinessApp.Api/Endpoints). There are ready-to-run sample requests
in [`BusinessApp.Api.http`](BusinessApp.Api/BusinessApp.Api.http) (VS Code REST Client, Visual Studio or Rider; `@host = http://localhost:5080`).
`GET /api` always lists the current set.

### Sales

| Route | Purpose |
| --- | --- |
| `GET/POST /api/customers`, `GET/PUT/DELETE /api/customers/{id}` | Customer grid + CRUD (code auto-generated) |
| `GET /api/customers/lookup?search=`, `/countries` | Combo box sources |
| `GET /api/customers/{id}/orders`, `/dashboard` | Customer orders; Customer Dashboard (top products, by category, yearly trend, recent orders) |
| `POST /api/customers/{id}/duplicate` | Ribbon "Duplicate" |
| `GET/POST /api/products`, `GET/PUT/DELETE /api/products/{id}`, `/lookup`, `/{id}/dashboard` | Products + product dashboard |
| `GET/POST /api/categories`, … `/lookup`, `/{id}/products`, `/{id}/dashboard?year=` | Categories |
| `GET/POST /api/suppliers`, … `/lookup`, `/{id}/products` | Suppliers |
| `GET/POST /api/shippers`, … `/lookup`, `/{id}/orders`, `/{id}/dashboard` | Shippers with freight / days-to-ship stats |
| `GET/POST /api/employees`, … `/lookup`, `/{id}/orders`, `/{id}/dashboard` | Employees (sales reps) |
| `GET/POST /api/orders`, `GET/PUT/DELETE /api/orders/{id}` | Orders (status `Shipped`/`Pending`/`Overdue`). POST takes `details[]` and allocates stock |
| `GET/POST /api/orders/{id}/details`, `PUT/DELETE /api/orders/{id}/details/{productId}` | Order lines |
| `POST /api/orders/{id}/ship`, `GET /api/orders/{id}/dashboard` | Ship an order; Order Dashboard |
| `GET /api/order-details` | Flat order-line grid |

### Reports and dashboards

| Route | Purpose |
| --- | --- |
| `GET /api/dashboard/summary` | Home KPIs across Sales, Projects and Marketing |
| `GET /api/dashboard/sales-overview` | Sales by category, orders by shipper, recent orders, low stock |
| `GET /api/reports/years` | Years with data (for year pickers) |
| `GET /api/reports/quarterly-orders?year=` | Per-customer Q1–Q4 (grid query supported) |
| `GET /api/reports/product-sales?year=` | Per-product Q1–Q4 (grid query supported) |
| `GET /api/reports/sales-by-category?year=` | Category totals with product breakdown |
| `GET /api/reports/sales-by-quarter?year=` | Quarter KPIs + category × quarter chart |
| `GET /api/reports/sales-by-year` | Yearly KPIs with growth + month × year chart |
| `GET /api/reports/sales-by-country`, `/sales-by-employee`, `/top-customers`, `/top-products` (`?year=&top=`) | Rankings |
| `GET /api/reports/monthly-sales?months=24` | Monthly trend |

Multi-series charts use `{ labels: [...], series: [{ name, values: [...] }] }`. Pies and bars use `[{ name, value }]`.

### Projects (Gantt)

| Route | Purpose |
| --- | --- |
| `GET/POST /api/projects`, `GET/PUT/DELETE /api/projects/{id}`, `/lookup` | Projects (moving `startDate` shifts all tasks) |
| `GET /api/projects/{id}/gantt` | Everything the Gantt needs: ordered `tasks` (rowNumber, WBS, level, resources, predecessors like `3,5SS+2 days`, formatting), `dependencies`, `resources` |
| `GET /api/projects/{id}/dashboard` | Task status, workload, estimated cost vs budget, upcoming milestones |
| `POST /api/projects/{id}/recalculate` | Force a reschedule |
| `GET/POST /api/projects/{id}/dependencies`, `PUT/DELETE /api/dependencies/{id}` | Links (FS/SS/FF/SF + lag). Cycles are rejected with 409 |
| `POST /api/projects/{id}/tasks` | Insert task / milestone (`parentId` or `afterTaskId`) |
| `GET/PUT/DELETE /api/tasks/{id}` | Task details / edit / delete including subtree |
| `PATCH /api/tasks/{id}/progress` | Ribbon 0–100% (on a summary task, applies to all its leaves) |
| `POST /api/tasks/{id}/indent`, `/outdent`, `/move` (`{"direction":"up"}`) | Ribbon structure commands |
| `PUT /api/tasks/{id}/notes`, `/format`, `/assignments` | Notes, Font group, Assign Resources |
| `GET/POST /api/resources`, … `/lookup`, `/{id}/workload?from=&to=` | Resources + weekly allocation (over-allocation flag) |

Every task mutation re-runs the scheduler (dependency propagation, summary roll-up) and returns the refreshed Gantt payload,
so the client can simply replace its state.

### Marketing

| Route | Purpose |
| --- | --- |
| `GET/POST /api/campaigns`, `GET/PUT/DELETE /api/campaigns/{id}`, `/lookup` | Campaigns with CTR, leads, conversion, cost per lead, ROI |
| `GET /api/campaigns/{id}/leads`, `/performance` | Campaign leads; funnel, status and weekly lead charts |
| `GET/POST /api/leads`, `GET/PUT/DELETE /api/leads/{id}` | Leads |
| `PATCH /api/leads/{id}/status` | Pipeline drag/drop |
| `POST /api/leads/{id}/convert` | Creates a Sales customer and marks the lead Won |
| `GET/POST /api/leads/{id}/activities`, `DELETE /api/leads/{id}/activities/{activityId}` | Calls, emails, meetings, notes |
| `GET /api/marketing/dashboard?months=12` | KPIs, funnel, leads by source/status/month, ROI by channel, top campaigns and owners |

### Other

| Route | Purpose |
| --- | --- |
| `GET /api/search?q=&limit=5` | Global search grouped by customers, products, orders (by number too), suppliers, employees, leads, campaigns, projects, tasks |
| `GET /api/avatars/{kind}/{id}.svg` | Generated avatar images |
| `GET /api/admin/stats`, `POST /api/admin/reset-database` | DB path + row counts; regenerate the database (dev only, no auth) |
| `GET /api` | Route catalog |
| `GET /health` | Health check |

## Project layout

```
Backend/
  BusinessApp.slnx           Solution (single project)
  global.json                Pins the .NET 11 SDK
  dotnet-tools.json          Local tool manifest (dotnet-ef, only for creating migrations)
  BusinessApp.Api/
    Program.cs               DI, JSON, CORS, ProblemDetails, DB initialization, endpoint registration
    appsettings.json         Connection string, CORS origins, Seed settings
    BusinessApp.Api.http     Sample requests
    Common/                  GridQuery (paging/sort/filter), chart DTOs, exception handler
    Data/                    AppDbContext, DatabaseInitializer, Migrations/
    Data/Seed/               Northwind reference data + Bogus-based generator (DataSeeder)
    Endpoints/               One file per resource (Minimal API route groups + DTOs)
    Models/                  Sales, Projects, Marketing entities
    Services/ScheduleService Gantt scheduling and roll-ups
    App_Data/                businessapp.db (generated at runtime, gitignored)
```

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `A compatible .NET SDK was not found` / `global.json` errors | Install the .NET 11 SDK (11.0.100-rc.3 or later 11.0.x). `dotnet --list-sdks` shows what you have. |
| `Failed to bind to address http://127.0.0.1:5080: address already in use` | Another process (often a previous API instance) is using the port. Stop it, or run on another port with `dotnet run --urls http://localhost:5090` and update `ApiBaseUrl` in the client's `wwwroot/appsettings.json` (see the [Blazor client README](../Frontend/modern-windows-forms/README.md#connecting-to-the-backend)). |
| Browser console shows `has been blocked by CORS policy` | The client's origin isn't in `Cors:AllowedOrigins`. Add the exact origin (see [CORS](#cors)) and restart the API. |
| Data looks stale or you changed `Seed` settings and nothing happened | Seeding is skipped when data already exists. Reset with `POST /api/admin/reset-database`, or stop the API and delete `App_Data/businessapp.db*`. |
| `SQLite Error 5: 'database is locked'` or the file can't be deleted | Another process has the database open (a second API instance, a DB browser). Close it, or stop the API before deleting the file. |
| Database ends up somewhere unexpected | The relative `Data Source` path resolves against the current working directory. Run `dotnet run` from `BusinessApp.Api/`, or check `GET /api/admin/stats` for the full path. |
| `https` profile certificate warnings | Run `dotnet dev-certs https --trust`, or use the default `http` profile. |
