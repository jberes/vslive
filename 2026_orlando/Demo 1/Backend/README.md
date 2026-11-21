# Dispatch Console – Backend (DispatchApi)

ASP.NET Core (.NET 10) Web API backed by a SQLite database, for a railway infrastructure maintenance dispatch console.
The data describes **Atlantic Corridor Rail**, a fictional operator using Northeast US geography.

All three React frontends in this demo use this API. See the [Demo 1 overview](../README.md) for how they fit together.

> **Domain mapping.** The schema and API keep their field-service names. Here is how they map to the railway domain:
> - Regions = territories
> - Depots = maintenance-of-way bases
> - Customers = lines and subdivisions (Tier: Platinum = Tier 1 … Standard = Tier 3)
> - Sites = stations, interlockings, track sections, substations, crossings and bridges (`AccessNotes` holds access and safety requirements)
> - Technicians = maintainers and crews
> - Skills = track, signal, power, station and structures qualifications
>
> The original field-service generator is kept for reference as `database/generate_db.fieldservice.py.bak`. It is not used.

The API supports three core dispatcher journeys: **assign a job**, **reschedule a technician**, and **see today's SLA risk**.

```
Backend/
├── database/
│   ├── schema.sql                        # full DDL (22 tables, FKs, CHECK constraints, indexes)
│   ├── generate_db.py                    # data generator (Python stdlib only)
│   ├── generate_db.fieldservice.py.bak   # original field-service generator, reference only
│   ├── dispatch.db.gz                    # committed, compressed database (~13 MB)
│   └── dispatch.db                       # restored or generated database (~65 MB), git-ignored
└── DispatchApi/                          # Web API: controllers, EF Core, OpenAPI + Swagger UI
    ├── Controllers/                      # Jobs, Dispatch, Sla, Dashboard, Technicians, Shifts, TimeOff, Customers, Sites, Assets, reference data
    ├── Data/                             # DbContext + DatabaseInitializer (DB check + date rebasing)
    ├── Services/                         # JobService, SchedulingService, SlaCalculator, AnalyticsService
    ├── Models/, Dtos/, Infrastructure/   # entities, DTOs, clock, paging, error handling
    ├── DispatchApi.http                  # sample requests
    └── Properties/launchSettings.json    # ports
```

---

## Prerequisites

| Tool | Version | Needed for |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | **10.0** (`<TargetFramework>net10.0</TargetFramework>`; there is no `global.json`, so any installed 10.0+ SDK is used) | Running the API |
| gzip / 7-Zip / PowerShell / Python | any | Restoring `dispatch.db.gz` (pick one, see below) |
| [Python](https://www.python.org/downloads/) | **3.7+** (uses `date.fromisoformat`); **no pip packages**, standard library only | *Optional*: regenerating the database from scratch |

NuGet packages (restored automatically by `dotnet run`):
- `Microsoft.AspNetCore.OpenApi` 10.0.14
- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.14
- `Swashbuckle.AspNetCore.SwaggerUI` 11.0.0

You do **not** need a separate SQLite install. EF Core ships its own native SQLite.

---

## Quick start

```bash
# 1. Restore the database (one time). See "Database" below for Windows.
cd "Demo 1/Backend/database"
gunzip -k dispatch.db.gz          # creates dispatch.db and keeps the .gz

# 2. Run the API
cd ../DispatchApi
dotnet run --launch-profile http
```

- Swagger UI: **http://localhost:5207/swagger**. `/` redirects there, and the `http` profile opens it automatically.
- OpenAPI JSON: http://localhost:5207/openapi/v1.json. You can use it to generate a typed client, for example `npx openapi-typescript http://localhost:5207/openapi/v1.json -o src/api/schema.ts`.
- Health check: http://localhost:5207/health returns `{ "status": "ok" }`.
- Sample requests: [`DispatchApi/DispatchApi.http`](DispatchApi/DispatchApi.http). It works with VS Code REST Client, Visual Studio and Rider.

---

## Database

The API reads a single SQLite file. By default that file is `Backend/database/dispatch.db`.

The raw `.db` is about **65 MB**, so the repo-wide `.gitignore` excludes `*.db`. The repo commits a gzip-compressed copy instead: **`database/dispatch.db.gz`** (~13 MB). After cloning, you must either **restore** that file or **regenerate** the database.

### Option A: Restore the committed snapshot (recommended, takes seconds)

Run these from `Demo 1/Backend/database`.

**macOS / Linux / Git Bash / WSL**

```bash
gunzip -k dispatch.db.gz        # -k keeps the .gz. Add -f to overwrite an existing dispatch.db.
```

**Windows: PowerShell** (Windows PowerShell 5.1 or PowerShell 7, nothing to install)

```powershell
$src = Join-Path $PWD 'dispatch.db.gz'
$dst = Join-Path $PWD 'dispatch.db'
$in  = [System.IO.File]::OpenRead($src)
$out = [System.IO.File]::Create($dst)
$gz  = New-Object System.IO.Compression.GZipStream($in, [System.IO.Compression.CompressionMode]::Decompress)
$gz.CopyTo($out); $gz.Dispose(); $out.Dispose(); $in.Dispose()
```

**Windows: 7-Zip**

```powershell
& "C:\Program Files\7-Zip\7z.exe" x dispatch.db.gz     # or right-click > 7-Zip > Extract Here
```

**Any OS with Python**

```bash
python3 -c "import gzip, shutil; shutil.copyfileobj(gzip.open('dispatch.db.gz', 'rb'), open('dispatch.db', 'wb'))"
```

On Windows, use `py` or `python` in place of `python3`.

> `tar -xzf dispatch.db.gz` does **not** work. The file is a plain gzip stream, not a tar archive. Windows Explorer's built-in "Extract All" also cannot open `.gz` files.

**Reset to a clean state.** The API writes to `dispatch.db` in two ways: assignments and reschedules you make in a frontend, and date rebasing on startup (see [How time works](#how-time-works)). To throw those changes away, stop the API and restore again. Use `gunzip -kf` to overwrite. Also delete any `dispatch.db-wal` / `dispatch.db-shm` files left next to the database.

### Option B: Regenerate from scratch with `generate_db.py`

```bash
cd "Demo 1/Backend/database"
python3 generate_db.py                                  # Windows: py generate_db.py
python3 generate_db.py --anchor 2026-11-11 --seed 42    # explicit anchor day and seed
```

| Argument | Default | Meaning |
|---|---|---|
| `--anchor YYYY-MM-DD` | today's date | The day the data treats as "today" (the simulated clock is 11:30 on that day) |
| `--seed N` | `42` | Random seed |
| `--out PATH` | `dispatch.db` next to the script | Output file |

What the script does:
- **Deletes** any existing file at `--out` and creates a new one. Stop the API first, because a running API holds the file open.
- Creates the tables by executing **`schema.sql`**. It looks for that file in the script's own folder, so you can run the script from any directory.
- Simulates **60 days of history, the anchor day, and 14 days of forward bookings**. (The script's docstring says 120 days, but the code uses `HISTORY_DAYS = 60`.)
- Runs `ANALYZE` and `VACUUM`, then prints row counts per table.
- Needs only the Python standard library: `argparse`, `math`, `os`, `random`, `re`, `sqlite3`, `datetime`. There is nothing to `pip install`.

Runtime depends on your machine. It builds roughly 42k jobs and 240k job events in pure Python, so it takes far longer than restoring the `.gz`.

**Is regeneration deterministic?** Mostly, but not byte-for-byte:
- The RNG is seeded (`--seed`, default 42). However, `--anchor` defaults to *today*, so pass `--anchor` if you want the same dates every time. Because the API rebases dates on startup anyway, the anchor rarely matters.
- One loop iterates over a Python `set` of skill codes (`for sc in set(bundle)`). String hashing is randomized per process, so technician skill and proficiency details, and everything downstream of them, can differ between runs even with the same seed. For repeatable output, also fix the hash seed:
  ```bash
  PYTHONHASHSEED=0 python3 generate_db.py --anchor 2026-11-11 --seed 42
  ```
  ```powershell
  $env:PYTHONHASHSEED = "0"; py generate_db.py --anchor 2026-11-11 --seed 42
  ```
- `SeedMeta.GeneratedAt` always records the wall-clock time.
- Even with a fixed hash seed, a regenerated database is **not guaranteed to match the committed `dispatch.db.gz`**. The specific IDs used in `DispatchApi.http` (job `35760`, technician `148`, and so on) may then point to different records. Restore the `.gz` if you want the exact data used in the demos.

### Where the API looks for the database

`Program.cs` reads `Database:Path` from configuration. The default in `appsettings.json` is `../database/dispatch.db`. A relative path is resolved against the app's **content root**. When you run `dotnet run` from inside `Backend/DispatchApi`, the content root is that folder, so the path resolves to `Backend/database/dispatch.db`.

To use a different file, set the `Database__Path` environment variable or edit `appsettings.json`:

```bash
Database__Path=/absolute/path/to/dispatch.db dotnet run --launch-profile http
```
```powershell
$env:Database__Path = "C:\path\to\dispatch.db"; dotnet run --launch-profile http
```

On startup, `DatabaseInitializer` checks that the file exists, turns on WAL mode and foreign keys, and rebases dates (see below). If the file is missing, the API **fails to start**. See [Troubleshooting](#troubleshooting).

---

## Running the API

```bash
cd "Demo 1/Backend/DispatchApi"
dotnet run --launch-profile http     # http://localhost:5207 (the default profile when --launch-profile is omitted)
dotnet run --launch-profile https    # https://localhost:7250 and http://localhost:5207
```

Ports come from `Properties/launchSettings.json`:

| Profile | URLs | Notes |
|---|---|---|
| `http` | `http://localhost:5207` | **All three frontends expect this URL.** Opens `/swagger`. |
| `https` | `https://localhost:7250`, `http://localhost:5207` | Needs a trusted dev certificate: `dotnet dev-certs https --trust` |

Both profiles set `ASPNETCORE_ENVIRONMENT=Development`.

**Changing the port.** Edit `applicationUrl` in `launchSettings.json`, or pass `--urls`, for example `dotnet run --launch-profile http --urls http://localhost:5300`. If you change it, you must also update the frontends. Each one is configured differently. See the [client ↔ server table](../README.md#which-server-each-client-talks-to).

### Configuration (`appsettings.json`)

| Key | Default | Meaning |
|---|---|---|
| `Database:Path` | `../database/dispatch.db` | SQLite file (relative to the content root) |
| `Database:RebaseDatesToToday` | `true` | Shift all dates so the seed's anchor day becomes the real today |
| `Clock:Mode` | `Simulated` | `Simulated` or `Real` |
| `Clock:SimulatedStartTime` | `11:30:00` | Operations time when the API boots (Simulated mode) |
| `Clock:SpeedFactor` | `1` | How fast simulated time advances |
| `Cors:Origins` | see below | Allowed browser origins |

### CORS

`Cors:Origins` in `appsettings.json` allows:

```
http://localhost:3101   ← Haiku dispatcher (calls the API directly, so it needs this)
http://localhost:3102   ← Opus dispatcher (uses a Vite proxy; listed for direct mode)
http://localhost:3103   ← MAI-Code-2 dispatcher (uses a Vite proxy)
http://localhost:5173   ← default Vite port
http://localhost:3000   ← CRA / Next.js
http://localhost:4200   ← Angular
```

`Program.cs` also allows any **https** origin on `claude.ai`, `claude.com` and `claudeusercontent.com`, including their subdomains. This lets Claude Design canvases use live data.

To allow any origin, set `"Origins": ["*"]`. Only the **Haiku** frontend calls the API cross-origin from the browser, so it is the only one that needs CORS. Opus and MAI-Code-2 go through the Vite dev-server proxy, so CORS does not apply to them.

---

## How time works

The data is a snapshot of an operation at **11:30 on the anchor day**. It includes jobs that are done, on site, en route, and not started.

- **Date rebasing.** On startup the API shifts every date by whole days so that the anchor day becomes the real today. This means the data never goes stale. The shift is written **into `dispatch.db`**. Turn it off with `Database:RebaseDatesToToday`.
- **Simulated clock.** `Clock:Mode = Simulated` (the default) starts "now" at 11:30 today when the API boots, then advances in real time (`SpeedFactor`). Set `Real` to use the wall clock instead. `GET /api/meta/clock` returns the current operations time. A UI should use it instead of the browser clock.
- **No time zones.** All timestamps are operations-local with no time-zone offset, for example `2026-11-11T13:45:00`.

---

## Data volume (seed 42)

| Table | Rows | | Table | Rows |
|---|---:|---|---|---:|
| Regions / Depots | 6 / 18 | | Customers | 3,600 |
| Dispatchers | 40 | | Sites | ~8,300 |
| Technicians | 428 | | Assets | ~25,700 |
| TechnicianSkills | ~1,250 | | Jobs | ~42,000–43,000 |
| Vehicles | 450 | | JobEvents | ~240,000 |
| Shifts | ~21,500 | | JobNotes | ~38,000 |
| TimeOffRequests | ~800 | | JobParts | ~30,000 |
| Skills / JobTypes / SlaPolicies | 16 / 32 / 12 | | Reschedules | ~3,700 |
| Parts / DepotInventory | 107 / ~1,900 | | | |

Jobs are placed on real technician timelines. Placement matches region, skill and proficiency, daily capacity, and travel time.

A typical "today" has about 850 jobs:
- about 115 unassigned
- about 160 technicians on site
- about 140 jobs at risk and 125 breached, including overdue carry-over

---

## API overview

List endpoints that page return `{ items, totalCount, page, pageSize, totalPages }` and accept these parameters:
- `page` and `pageSize` (maximum 500)
- `search` (case-insensitive)
- `sort`: comma-separated fields, with `-` for descending. For example, `sort=-priority,scheduledStart`. Sorting on `priority` orders by severity.

Errors are RFC 7807 problem details. A **409** on assign or reschedule includes the validation issues in `data`.

Every job row includes a computed `sla` object: `{ state: OnTrack|AtRisk|Breached|Met|NotApplicable, stage: Response|Resolution|Closed, dueAt, minutesRemaining, projectedAt, reason }`.

### Journey 1 – Assign a job
| Method | Route | Purpose |
|---|---|---|
| GET | `/api/jobs?date=&status=Unassigned&priority=&regionId=&slaState=` | Unassigned queue |
| GET | `/api/jobs/{id}` · `/api/jobs/by-number/{jobNumber}` | Detail: site, asset, events timeline, notes, parts, reschedule history |
| GET | `/api/jobs/{id}/recommendations?limit=&includeIneligible=&crossRegion=` | Ranked technicians. Ranking uses skill, proficiency, shift, earliest free slot, travel time, workload, and whether the response SLA can be met. |
| GET | `/api/jobs/{id}/validate-assignment?technicianId=&scheduledStart=` | Dry run: blocking issues and warnings |
| POST | `/api/jobs/{id}/assign` | `{ technicianId, scheduledStart?, dispatcherId?, force?, note? }` |
| POST | `/api/jobs/{id}/unassign` | Return the job to the queue |
| POST | `/api/jobs/{id}/status` | Workflow: Assigned → Dispatched → EnRoute → OnSite → Completed (plus OnHold and Cancelled). Invalid transitions return 409. |

**Blocking issues:** `SKILL_MISSING`, `PROFICIENCY_LOW`, `CERT_EXPIRED`, `NO_SHIFT`, `TIME_OFF`, `OVERLAP`, `AFTER_SHIFT`, `NO_FREE_SLOT`, `TECH_NOT_ACTIVE`, `JOB_CLOSED`.

**Warnings:** `OVERTIME` (up to 3 h past shift end), `CAPACITY`, `OUT_OF_REGION`, `SLA_RESPONSE`, `SLA_RESOLUTION`, `OUTSIDE_WINDOW`, `START_IN_PAST`.

### Journey 2 – Reschedule a technician
| Method | Route | Purpose |
|---|---|---|
| GET | `/api/dispatch/board?date=&regionId=&depotId=&skillId=` | Gantt data: technicians on shift with shift bounds, utilization and jobs, plus the unassigned queue |
| GET | `/api/technicians/{id}/schedule?from=&to=` | A technician's shifts, time off and jobs |
| POST | `/api/jobs/{id}/reschedule` | `{ newScheduledStart, technicianId?, reasonCode, comment?, updateWindow, force? }` |
| POST | `/api/dispatch/technicians/{id}/reassign-day` | The technician is out. `mode` is `AutoDistribute`, `ToTechnician` or `Unassign`. Can also mark the shift Absent. |
| POST | `/api/shifts/{id}/move` | Change shift hours and slide that day's jobs by the same amount |
| GET | `/api/reschedules?from=&to=&technicianId=&reason=` | Audit trail |

A reschedule with reason `CustomerRequest` re-baselines the SLA due times. Operational reasons such as `TechnicianUnavailable` or `Overrun` keep the original commitment.

### Journey 3 – Today's SLA risk
| Method | Route | Purpose |
|---|---|---|
| GET | `/api/sla/summary?date=&regionId=` | Counts by state, compliance %, penalty exposure, breakdowns by priority, region and tier, due-by-hour |
| GET | `/api/sla/at-risk?state=&priority=&regionId=&limit=` | At-risk and breached jobs, most urgent first |
| GET | `/api/sla/trend?days=30` | Daily response and resolution compliance |
| GET | `/api/dashboard/kpis` · `jobs-by-hour` · `jobs-by-status` · `jobs-by-category` · `tech-utilization` | Dashboard tiles and charts |

### Supporting resources (full CRUD)
- `/api/technicians`, including `PUT {id}/skills`, `PATCH {id}/field-status`, `GET {id}/schedule` and `GET utilization`
- `/api/shifts`, including `POST {id}/move`
- `/api/time-off`, including `POST {id}/approve` (lists conflicting jobs) and `POST {id}/deny`
- `/api/customers`, including `{id}/sites` and `{id}/jobs`
- `/api/sites`, including `{id}/assets` and `{id}/jobs`
- `/api/assets`
- `/api/jobs`: `POST`, `PATCH`, `DELETE` (which cancels the job), plus `/notes`, `/parts` and `/events`

Read-only:
- `/api/regions`, `/api/depots` (including `{id}/inventory`), `/api/skills`, `/api/job-types`, `/api/sla-policies`, `/api/parts`, `/api/vehicles`
- `/api/dispatchers` (including `{id}/activity`)
- `/api/meta/lookups`, which returns all reference data plus every enum's values in one call
- `/api/meta/clock`, which returns the current operations time

The API has **no authentication**, and it has no handover, approval, or row-version (concurrency) endpoints. The frontends work around these gaps in different ways. See their READMEs.

---

## SLA rules

Each job is linked to an `SlaPolicy` based on the customer tier (Standard, Gold or Platinum) and the job priority.
- **Critical and High jobs are reactive.** The response deadline is the creation time plus the policy's response minutes.
- **Medium and Low jobs are appointments.** The response deadline is the end of the promised window.

A job is **AtRisk** when any of these is true:
- its scheduled or projected time is after the due time
- it is unassigned close to the deadline
- the technician is late, or the job is on hold
- the remaining time is within the policy's `AtRiskThresholdMinutes`

A job is **Breached** when the due time has passed, or when the response SLA was missed.

---

## Troubleshooting

**`FileNotFoundException: SQLite database not found at '…/dispatch.db'` on startup**
You haven't restored the database yet. The error message only suggests running `generate_db.py`, but restoring the `.gz` is faster. See [Option A](#option-a-restore-the-committed-snapshot-recommended-takes-seconds).
If the file exists but the API still can't find it, you probably ran the API from a different directory. Run `dotnet run` from inside `Backend/DispatchApi`, or set `Database__Path` to an absolute path.

**`SQLite Error … database is locked` or the generator can't delete `dispatch.db`**
Stop the API first. It keeps the database open, in WAL mode. If the API crashed, delete any leftover `dispatch.db-wal` and `dispatch.db-shm` files.

**`Failed to bind to address http://127.0.0.1:5207: address already in use`**
Another API instance or another process is using port 5207. Find and stop it:
- macOS/Linux: `lsof -i :5207`
- Windows: `netstat -ano | findstr :5207`, then `taskkill /PID <pid> /F`

If you need a different port, change it as described in [Running the API](#running-the-api) and update the frontends to match.

**The browser console shows `blocked by CORS policy`**
- Only the Haiku frontend calls the API directly. Check that the page origin (scheme, host and port) is exactly one of the origins in `Cors:Origins`.
- `http://127.0.0.1:3101` is a different origin from `http://localhost:3101`.
- If you started a frontend on a custom port (`npm start -- --port <n>`), add that origin to the list.
- After you edit `Cors:Origins`, restart the API.

**`The current .NET SDK does not support targeting .NET 10.0`**
Install the .NET 10 SDK and check it with `dotnet --list-sdks`.

**HTTPS profile errors about the certificate**
Run `dotnet dev-certs https --trust`, or use the `http` profile. All the frontends use http anyway.

**Data looks "wrong" after you click around**
Assignments, reschedules, notes and new jobs are saved to `dispatch.db`. To start fresh, restore the `.gz` again (`gunzip -kf dispatch.db.gz`).
