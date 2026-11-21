using modern_windows_forms.Client.Models;

namespace modern_windows_forms.Client.Models.Reports;

/// <summary>GET /api/reports/sales-by-year — yearly KPIs plus the month × year chart.</summary>
public sealed record SalesByYear(IReadOnlyList<YearSales> Years, ChartData MonthlyByYear);

public sealed record YearSales(
    int Year, int Orders, int Customers, decimal Sales, decimal Freight,
    decimal AverageOrderValue, decimal? GrowthPercent);

/// <summary>One entry in the hub's report list. Each report keeps its own URL (/reports/{Key}).</summary>
public sealed record ReportDefinition(string Key, string Name, string Was, string Description, string PrimaryChart, string Endpoint);

public enum KpiTone { Up, Down, Neutral }

public sealed record ReportKpi(string Label, string Value, string Sub, string Delta, KpiTone Tone);

/// <summary>A month value for one year's line in the month × year chart.</summary>
public sealed record MonthValue(string Month, decimal Value);

public sealed record YearSeries(int Year, string Color, int Width, IReadOnlyList<MonthValue> Points);

/// <summary>Row of the "Year over year" IgbGrid (preformatted for display).</summary>
public sealed class YearRowVm
{
    public int Year { get; set; }
    public string YearLabel { get; set; } = "";
    public decimal Sales { get; set; }
    public string Revenue { get; set; } = "";
    public string Growth { get; set; } = "";
    public string GrowthTone { get; set; } = "neutral";
    public string Orders { get; set; } = "";
    public string Customers { get; set; } = "";
    public string AverageOrder { get; set; } = "";
}

/// <summary>Row of the month × year table view (one row per year, a column per month).</summary>
public sealed class YearMonthsVm
{
    public int Year { get; set; }
    public string YearLabel { get; set; } = "";
    public string Jan { get; set; } = "";
    public string Feb { get; set; } = "";
    public string Mar { get; set; } = "";
    public string Apr { get; set; } = "";
    public string May { get; set; } = "";
    public string Jun { get; set; } = "";
    public string Jul { get; set; } = "";
    public string Aug { get; set; } = "";
    public string Sep { get; set; } = "";
    public string Oct { get; set; } = "";
    public string Nov { get; set; } = "";
    public string Dec { get; set; } = "";
    public string Total { get; set; } = "";
}
