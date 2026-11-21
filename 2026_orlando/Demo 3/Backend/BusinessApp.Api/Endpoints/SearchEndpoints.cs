using BusinessApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed record SearchHit(string Type, int Id, string Title, string? Subtitle, string Route, string? AvatarUrl = null);

public sealed record SearchGroup(string Type, string Label, IReadOnlyList<SearchHit> Items);

/// <summary>Global (ribbon / title bar) search across every module.</summary>
public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/search", async (string? q, int? limit, AppDbContext db, CancellationToken ct) =>
        {
            var term = q?.Trim().ToLower();
            if (string.IsNullOrEmpty(term) || term.Length < 2)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["q"] = ["Enter at least 2 characters."] });
            var take = Math.Clamp(limit ?? 5, 1, 25);
            var orderId = int.TryParse(term, out var n) ? n : -1;

            var groups = new List<SearchGroup>
            {
                new("customers", "Customers", await db.Customers
                    .Where(c => c.CompanyName.ToLower().Contains(term) || c.Code.ToLower().Contains(term) || (c.ContactName ?? "").ToLower().Contains(term))
                    .OrderBy(c => c.CompanyName).Take(take)
                    .Select(c => new SearchHit("customer", c.Id, c.CompanyName, c.ContactName + " · " + c.City + ", " + c.Country, "/customers/" + c.Id, "/api/avatars/customers/" + c.Id + ".svg"))
                    .ToListAsync(ct)),
                new("products", "Products", await db.Products
                    .Where(p => p.Name.ToLower().Contains(term))
                    .OrderBy(p => p.Name).Take(take)
                    .Select(p => new SearchHit("product", p.Id, p.Name, p.Category!.Name + " · " + p.Supplier!.CompanyName, "/products/" + p.Id, null))
                    .ToListAsync(ct)),
                new("orders", "Orders", await db.Orders
                    .Where(o => o.Id == orderId || o.Customer!.CompanyName.ToLower().Contains(term) || (o.ShipName ?? "").ToLower().Contains(term))
                    .OrderByDescending(o => o.Id == orderId).ThenByDescending(o => o.OrderDate).Take(take)
                    .Select(o => new SearchHit("order", o.Id, "Order #" + o.Id, o.Customer!.CompanyName + " · " + o.OrderDate.ToString("yyyy-MM-dd"), "/orders/" + o.Id, null))
                    .ToListAsync(ct)),
                new("suppliers", "Suppliers", await db.Suppliers
                    .Where(s => s.CompanyName.ToLower().Contains(term) || (s.ContactName ?? "").ToLower().Contains(term))
                    .OrderBy(s => s.CompanyName).Take(take)
                    .Select(s => new SearchHit("supplier", s.Id, s.CompanyName, s.City + ", " + s.Country, "/suppliers/" + s.Id, null))
                    .ToListAsync(ct)),
                new("employees", "Employees", await db.Employees
                    .Where(e => (e.FirstName + " " + e.LastName).ToLower().Contains(term) || (e.Title ?? "").ToLower().Contains(term))
                    .OrderBy(e => e.LastName).Take(take)
                    .Select(e => new SearchHit("employee", e.Id, e.FirstName + " " + e.LastName, e.Title, "/employees/" + e.Id, "/api/avatars/employees/" + e.Id + ".svg"))
                    .ToListAsync(ct)),
                new("leads", "Leads", await db.Leads
                    .Where(l => (l.FirstName + " " + l.LastName).ToLower().Contains(term) || l.Company.ToLower().Contains(term) || (l.Email ?? "").ToLower().Contains(term))
                    .OrderByDescending(l => l.CreatedAt).Take(take)
                    .Select(l => new SearchHit("lead", l.Id, l.FirstName + " " + l.LastName, l.Company + " · " + l.Status.ToString(), "/leads/" + l.Id, "/api/avatars/leads/" + l.Id + ".svg"))
                    .ToListAsync(ct)),
                new("campaigns", "Campaigns", await db.Campaigns
                    .Where(c => c.Name.ToLower().Contains(term))
                    .OrderByDescending(c => c.StartDate).Take(take)
                    .Select(c => new SearchHit("campaign", c.Id, c.Name, c.Channel.ToString() + " · " + c.Status.ToString(), "/campaigns/" + c.Id, null))
                    .ToListAsync(ct)),
                new("projects", "Projects", await db.Projects
                    .Where(p => p.Name.ToLower().Contains(term))
                    .OrderBy(p => p.Name).Take(take)
                    .Select(p => new SearchHit("project", p.Id, p.Name, p.Status.ToString(), "/projects/" + p.Id, null))
                    .ToListAsync(ct)),
                new("tasks", "Tasks", await db.ProjectTasks
                    .Where(t => t.Name.ToLower().Contains(term))
                    .OrderBy(t => t.Start).Take(take)
                    .Select(t => new SearchHit("task", t.Id, t.Name, t.Project!.Name, "/projects/" + t.ProjectId + "?task=" + t.Id, null))
                    .ToListAsync(ct)),
            };

            var nonEmpty = groups.Where(g => g.Items.Count > 0).ToList();
            return Results.Ok(new { query = q!.Trim(), total = nonEmpty.Sum(g => g.Items.Count), groups = nonEmpty });
        }).WithTags("Search");
    }
}
