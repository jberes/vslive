namespace modern_windows_forms.Client.Models.Customers;

/// <summary>Row of GET /api/customers (grid query). A class with settable properties so IgbGrid can bind it.</summary>
public sealed class CustomerRow
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactTitle { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
    public DateTime? LastOrderDate { get; set; }
    public string? AvatarUrl { get; set; }
}

/// <summary>GET /api/customers/countries item.</summary>
public sealed record CountryCount(string Country, int Customers);

/// <summary>GET /api/customers/{id}/dashboard.</summary>
public sealed record CustomerDashboard(
    CustomerRow Customer,
    CustomerKpis Kpis,
    IReadOnlyList<CustomerTopProduct> TopProducts,
    IReadOnlyList<NameValue> SalesByCategory,
    ChartData TopCategoriesByYear,
    IReadOnlyList<CustomerOrder> RecentOrders);

public sealed record CustomerKpis(
    decimal TotalSales, int OrderCount, decimal AverageOrderValue,
    DateTime? FirstOrderDate, DateTime? LastOrderDate, int OpenOrders);

public sealed record CustomerTopProduct(int ProductId, string Name, string Category, int Units, int Orders, decimal Sales);

/// <summary>Order summary as returned by /api/customers/{id}/orders, /api/orders and the dashboard's recentOrders.</summary>
public sealed record CustomerOrder(
    int Id, int CustomerId, string CustomerName, string? EmployeeName,
    DateTime OrderDate, DateTime? RequiredDate, DateTime? ShippedDate,
    string? ShipViaName, decimal Freight, int ItemCount, decimal Subtotal, decimal Total, string Status);

/// <summary>Bar point for the "Revenue by year" chart.</summary>
public sealed record YearRevenue(string Year, decimal Revenue);
