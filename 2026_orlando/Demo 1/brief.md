*Planning scenario: I’m answering as a fictional Railways maintenance operations lead. These are design assumptions to validate, rather than statements about fictional Railways actual systems or procedures.*

## One-page brief

**Purpose:** Give 40 dispatchers a shared operational picture of track, station, and supporting infrastructure maintenance. Our objective is full infrastructure availability, with safety governing every assignment and schedule change. Design for React on 1920px desktop screens.

**1. Users and workload**

“My dispatchers coordinate maintenance teams across tracks, switches, signals, station equipment, power, and facilities. They need to understand what has failed, its operational impact, and who can respond.”

Assume each dispatcher coordinates 15–25 field personnel and 30–60 active or planned jobs per shift. Shift supervisors handle escalations, resource conflicts, and handovers. Field technicians supply status updates; dispatchers remain the primary console users. English is the primary interface language, with EST and 24-hour time.

**2. Five essential journeys**

| Journey | Essential outcome |
|---|---|
| Assign a maintenance job | Match the asset fault to a qualified, available crew; check access requirements and confirm ownership. |
| Reschedule a technician or crew | Move work while showing affected commitments, access windows, and downstream conflicts before confirmation. |
| See today’s SLA risk | Identify jobs approaching response or restoration targets, understand the cause, and take action. |
| Coordinate an urgent infrastructure fault | Record location and service impact, escalate through the appropriate operational process, and dispatch qualified responders. |
| Complete a shift handover | Transfer unresolved faults, restrictions, pending approvals, and next actions to a named incoming owner. |

**3. Decisions and data**

“Proximity alone cannot determine an assignment. We need the right qualifications, equipment, availability, and authorized access.”

Required data includes asset identifiers and locations, line/station/track references, fault severity, service impact, job history, crew qualifications, shifts, equipment availability, travel estimates, access windows, dependencies, and response/restoration deadlines.

SLA risk should consider remaining time, estimated work duration, travel, dependencies, and access availability. Separate service targets from safety restrictions. Dispatchers may adjust priorities within their authority; the console must preserve required approvals and record changes.

Integrate with existing maintenance, asset, workforce, and operational-status systems. Their names and interfaces remain unconfirmed. Show timestamps and stale-data indicators; treat live fault and assignment updates as a provisional requirement.

**4. Constraints**

Use React and the supplied component library through its MCP server. **Never hand-roll grids, charts, or date pickers.** Confirm the library, server, and supported scheduling components before implementation.

Use primary **`#155dfc`**, a dense desktop layout, persistent location context, and role-based permissions. Preserve active filters and selection when opening job details. Account for simultaneous edits by 40 dispatchers through conflict detection and explicit save outcomes.

**5. Quality bar**

“During a disruption, I need to see the highest-impact unresolved problem, its owner, and the next action immediatecly.”

- Critical status must be readable without relying on color.
- Routine assignments should require minimal navigation; consequential changes need a clear impact preview.
- Keyboard operation, visible focus, readable English text, and adequate contrast are required.
- Loading, empty, stale, disconnected, and conflicting-update states must be designed.
- Every route must be captured at 1920px, visually inspected, corrected, and captured again before completion.
- Dense grids must still breathe: rows with stacked two-line cells need at least 56px row height, vertical cell padding, spacing between lines, and visible row dividers so no row looks jammed.

**6. Success measures and visual tone**

Success means faster valid assignments, fewer scheduling conflicts, fewer overdue jobs, and complete handovers. Establish current baselines before setting numerical improvement targets.

The preferred tone is a **calm railway operations workspace**: precise, information-rich, and restrained. Blue indicates selection and primary actions; amber and red have explicit operational meanings.

## Three visual directions

| Direction | Layout and visual language | Best fit |
|---|---|---|
| **A. Operations Ledger** | Light neutral surfaces, compact library grid, pinned asset/location/priority columns, slim risk summary, and a persistent details panel. `#155dfc` anchors selection and actions. | Fast job triage and assignment across large queues. |
| **B. Corridor Control** | Line-and-station navigation beside a dense job grid, with a synchronized geographic or schematic context panel. Strong location hierarchy and restrained status markers. | Understanding clustered faults and their impact along a railway corridor. |
| **C. Shift Planner** | Crew list and library-supported scheduling view dominate the screen. An exception queue exposes access-window conflicts, staffing gaps, and deadlines; job details open alongside the schedule. | Rescheduling crews and planning work around available access windows. |

**Recommendation:** Develop **Operations Ledger** as the primary direction, with Corridor Control and Shift Planner as focused views sharing the same filters, status language, and job details.

Let's go with Operations Ledger. Create the route map and implementation plan, check our component library through MCP - we are using Ignite UI React OSS controls, and the React Grid Lite, and ApexCharts React (both UI libraries are installed in the /dispatcher client UI folder w/ the base project running) for any data visualization then build the React console. 

For ApexCharts and Ignite UI - use the Fluent UI theme from theming MCP.

Screenshot every route and fix any issues before calling it done.
