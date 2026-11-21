using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Marketing;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public static class MarketingEndpoints
{
    public static void MapMarketingEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/marketing").WithTags("Marketing");

        g.MapGet("/dashboard", async (int? months, AppDbContext db, CancellationToken ct) =>
        {
            var count = Math.Clamp(months ?? 12, 1, 60);
            var since = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(count - 1));

            var byStatus = await db.Leads.GroupBy(l => l.Status).Select(g => new { g.Key, Count = g.Count(), Value = g.Sum(l => (double)l.EstimatedValue) }).ToListAsync(ct);
            var bySource = await db.Leads.GroupBy(l => l.Source).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
            var monthly = await db.Leads.Where(l => l.CreatedAt >= since)
                .GroupBy(l => new { l.CreatedAt.Year, l.CreatedAt.Month, Won = l.Status == LeadStatus.Won })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Won, Count = g.Count() }).ToListAsync(ct);
            var byChannel = await db.Campaigns.GroupBy(c => c.Channel).Select(g => new
            {
                Channel = g.Key,
                Campaigns = g.Count(),
                Cost = g.Sum(c => (double)c.ActualCost),
                Leads = g.Sum(c => c.Leads.Count),
                Won = g.Sum(c => c.Leads.Count(l => l.Status == LeadStatus.Won)),
                WonValue = g.Sum(c => c.Leads.Where(l => l.Status == LeadStatus.Won).Sum(l => (double)l.EstimatedValue)),
            }).ToListAsync(ct);
            var topCampaigns = await CampaignEndpoints.Project(db.Campaigns).OrderByDescending(c => c.WonValue).Take(5).ToListAsync(ct);
            var topOwners = await db.Leads.Where(l => l.Status == LeadStatus.Won && l.Owner != null)
                .GroupBy(l => l.Owner!.FirstName + " " + l.Owner.LastName)
                .Select(g => new { g.Key, Value = g.Sum(l => (double)l.EstimatedValue) })
                .OrderByDescending(x => x.Value).Take(5).ToListAsync(ct);

            int StatusCount(params LeadStatus[] s) => byStatus.Where(x => s.Contains(x.Key)).Sum(x => x.Count);
            var total = byStatus.Sum(x => x.Count);
            var won = StatusCount(LeadStatus.Won);
            var closed = won + StatusCount(LeadStatus.Lost);
            var monthLabels = Enumerable.Range(0, count).Select(i => since.AddMonths(i).ToString("yyyy-MM")).ToList();

            return new
            {
                kpis = new
                {
                    totalLeads = total,
                    openLeads = total - closed,
                    wonLeads = won,
                    winRatePercent = closed == 0 ? 0 : Math.Round(won * 100.0 / closed, 1),
                    pipelineValue = Money.Round(byStatus.Where(x => x.Key is not (LeadStatus.Won or LeadStatus.Lost)).Sum(x => x.Value)),
                    wonValue = Money.Round(byStatus.Where(x => x.Key == LeadStatus.Won).Sum(x => x.Value)),
                    activeCampaigns = await db.Campaigns.CountAsync(c => c.Status == CampaignStatus.Active, ct),
                    totalSpend = Money.Round(byChannel.Sum(c => c.Cost)),
                },
                // Cumulative funnel: a lead in Proposal has also been Contacted and Qualified.
                funnel = new[]
                {
                    new NameValue("Leads", total),
                    new NameValue("Contacted", StatusCount(LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Proposal, LeadStatus.Won)),
                    new NameValue("Qualified", StatusCount(LeadStatus.Qualified, LeadStatus.Proposal, LeadStatus.Won)),
                    new NameValue("Proposal", StatusCount(LeadStatus.Proposal, LeadStatus.Won)),
                    new NameValue("Won", won),
                },
                leadsByStatus = Enum.GetValues<LeadStatus>().Select(s => new NameValue(s.ToString(), StatusCount(s))),
                leadsBySource = bySource.OrderByDescending(x => x.Count).Select(x => new NameValue(x.Key.ToString(), x.Count)),
                leadsByMonth = ChartData.From(monthLabels, ["Created", "Won"],
                    monthly.SelectMany(m => m.Won
                        ? new[] { (Label: $"{m.Year:0000}-{m.Month:00}", Series: "Created", m.Count), (Label: $"{m.Year:0000}-{m.Month:00}", Series: "Won", m.Count) }
                        : new[] { (Label: $"{m.Year:0000}-{m.Month:00}", Series: "Created", m.Count) }),
                    r => r.Label, r => r.Series, r => r.Count),
                roiByChannel = byChannel.OrderByDescending(c => c.WonValue).Select(c => new
                {
                    channel = c.Channel,
                    campaigns = c.Campaigns,
                    cost = Money.Round(c.Cost),
                    leads = c.Leads,
                    won = c.Won,
                    wonValue = Money.Round(c.WonValue),
                    costPerLead = c.Leads == 0 ? 0 : Money.Round(c.Cost / c.Leads),
                    roiPercent = c.Cost == 0 ? 0 : Math.Round((c.WonValue - c.Cost) / c.Cost * 100, 1),
                }),
                topCampaigns,
                topOwners = topOwners.Select(x => new NameValue(x.Key, Money.Round(x.Value))),
            };
        });
    }
}
