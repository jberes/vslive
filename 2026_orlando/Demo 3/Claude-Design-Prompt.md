Attached: the 6 screenshots (5 Outlook-inspired Sales screens + the Project Manager Gantt).
Connected: the BusinessApp folder (Desktop app → Add folder).

Design a modern web version of the desktop app in the attached screenshots.
Build it as a Design canvas, one artboard per screen.

DESIGN SYSTEM
Use the ApexCharts design system: https://claude.ai/artifact/YLMiXrDetcMZew3NymaDoZ for the color scheme and design taste.
Read its README and tokens.json first, install it on the canvas, and use its
exact colors, type (Geist headings, Inter body, JetBrains Mono for codes),
spacing, radii, shadows, status colors, chart palette (chart-1…5) and Gantt
track colors. Use Lucide-style outline icons and no emoji. If you can't read
the design system, stop and tell me instead of inventing a look.


DATA
Read /Backend/README.md for the API. Pull realistic sample values
from Backend/BusinessApp.Api/App_Data/businessapp.db (read-only), such as real
customers, orders, products, project tasks, campaigns and leads. Don't invent
figures. Where a value isn't available, use a placeholder or a loading state.

COMPONENTS
Use Ignite UI for Blazor as the component guide - use the  Indigo.Design UI Kit for Bootstrap design system for component definitions.
 NavDrawer, Navbar, GridLite,
Tabs, Combo, Select, DatePicker, Dialog, Toast, Chip, Avatar, ButtonGroup and
LinearProgress. Use Blazor-ApexCharts for charts and ApexGantt for the
schedule. Add a sticky note per section mapping each screen to its components.

SCOPE (light theme only, desktop first, about 11 artboards)
1. App shell + Home dashboard: KPIs with sparklines, monthly revenue,
   sales by category, recent orders, "needs attention", active projects
   and live campaigns.
2. Customers: list with search, filters and paging, plus a detail panel
   (Overview / Orders / Products tabs).
3. Orders: status tabs and filter chips, rows that expand to show order
   lines, and a Ship order dialog with an undo toast.
4. New order: form with customer combo, dates, ship-via cards and
   editable line items. Show server validation errors (400), then a
   success state.
5. Products & categories: category pills, category KPIs, quarterly
   chart, revenue by product, and a stock-vs-reorder grid.
6. Reports hub: one page replacing the 9 report screens, with Sales by
   year fully designed.
7. Project Gantt: a toolbar replacing the ribbon, task grid + ApexGantt
   timeline with dependencies, a today line and milestones, and a task
   detail panel.
8. Leads pipeline board: a drag in progress and a convert-to-customer
   dialog.
9. Campaigns: KPIs, grid with spend vs budget, funnel for the selected
   campaign, spend by channel.
10. States sheet: loading skeletons, empty states, network error,
    409 conflict, 404, field validation, toasts.
11. Tablet (icon rail) and phone (bottom tabs, cards, phase list instead
    of the Gantt).

Make the artboards clickable prototypes: link the nav between screens and
make tabs, row selection, dialogs and toasts work. Use the full window
width (1440) and lay out at phone width too. Don't ask clarifying
questions; state your assumptions at the end.