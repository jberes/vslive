namespace modern_windows_forms.Client.Layout;

public enum BadgeTone { Neutral, Error }

public sealed record NavItem(string Key, string Label, string Href, string Icon, string Section);

public sealed record NavGroup(string Title, IReadOnlyList<NavItem> Items);

/// <summary>Navigation structure from the design's shared AppNav board.</summary>
public static class NavModel
{
    public static readonly IReadOnlyList<NavGroup> Groups =
    [
        new("", [new("home", "Home", "", "home", "Home")]),
        new("Sales",
        [
            new("customers", "Customers", "customers", "users", "Sales"),
            new("orders", "Orders", "orders", "orders", "Sales"),
            new("neworder", "New order", "orders/new", "plus-circle", "Sales"),
            new("products", "Products", "products", "package", "Sales"),
            new("reports", "Reports", "reports", "bar-chart", "Sales"),
        ]),
        new("Projects", [new("projects", "Project schedule", "projects", "gantt", "Projects")]),
        new("Marketing",
        [
            new("leads", "Leads", "leads", "kanban", "Marketing"),
            new("campaigns", "Campaigns", "campaigns", "megaphone", "Marketing"),
        ]),
    ];

    public static readonly IReadOnlyList<NavItem> Prototype =
    [
        new("states", "States sheet", "states", "layers", "Prototype"),
    ];

    /// <summary>Phone bottom tabs (Home / Orders / Projects / Leads / More).</summary>
    public static readonly IReadOnlyList<NavItem> PhoneTabs =
    [
        new("home", "Home", "", "home", "Home"),
        new("orders", "Orders", "orders", "orders", "Sales"),
        new("projects", "Projects", "projects", "gantt", "Projects"),
        new("leads", "Leads", "leads", "kanban", "Marketing"),
        new("more", "More", "more", "menu", "More"),
    ];

    public static IEnumerable<NavItem> All => Groups.SelectMany(g => g.Items).Concat(Prototype);

    /// <summary>Longest href match wins so "orders/new" beats "orders".</summary>
    public static NavItem? Match(string relativePath)
    {
        var path = relativePath.Split('?', '#')[0].Trim('/');
        return All.Where(i => i.Href.Length == 0 ? path.Length == 0 : path == i.Href || path.StartsWith(i.Href + "/"))
                  .OrderByDescending(i => i.Href.Length)
                  .FirstOrDefault();
    }
}
