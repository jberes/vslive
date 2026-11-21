namespace modern_windows_forms.Client.Models.Orders;

/// <summary>GET /api/orders row (grid query). Status is Shipped, Pending or Overdue.</summary>
public sealed class OrderRow
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime RequiredDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public int ShipViaId { get; set; }
    public string? ShipViaName { get; set; }
    public decimal Freight { get; set; }
    public string? ShipName { get; set; }
    public string? ShipAddress { get; set; }
    public string? ShipCity { get; set; }
    public string? ShipRegion { get; set; }
    public string? ShipPostalCode { get; set; }
    public string? ShipCountry { get; set; }
    public int ItemCount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "Pending";

    /// <summary>Employee full name resolved client-side from /api/employees/lookup (the list returns last name only).</summary>
    public string? EmployeeFullName { get; set; }

    public bool CanShip => ShippedDate is null;
}

/// <summary>GET /api/orders/{id}/details line.</summary>
public sealed record OrderLine(
    int OrderId, int ProductId, string ProductName, string? CategoryName, string? QuantityPerUnit,
    decimal UnitPrice, int Quantity, double Discount, decimal LineTotal);

public sealed record Shipper(int Id, string CompanyName, string? Phone, int OrderCount, int PendingShipments);

public sealed record EmployeeLookup(int Id, string Name, string? LastName, string? Title);

public sealed record CountryCount(string Country, int Customers);

public sealed record CustomerLookup(int Id, string Code, string CompanyName, string? City, string? Country)
{
    /// <summary>Combo display text.</summary>
    public string Label => CompanyName;
}

public sealed record CustomerDetail(
    int Id, string Code, string CompanyName, string? ContactName, string? Address, string? City,
    string? Region, string? PostalCode, string? Country, int OrderCount);

public sealed record ProductLookup(int Id, string Name, decimal UnitPrice, int UnitsInStock, int CategoryId, bool Discontinued)
{
    public string Label => Discontinued ? Name + " (discontinued)" : Name;
}

/// <summary>POST /api/orders/{id}/ship body.</summary>
public sealed record ShipOrderInput(DateTime? ShippedDate, int? ShipViaId);

/// <summary>POST /api/orders and PUT /api/orders/{id} body.</summary>
public sealed class OrderInput
{
    public int CustomerId { get; set; }
    public int EmployeeId { get; set; }
    public int ShipViaId { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public decimal Freight { get; set; }
    public string? ShipName { get; set; }
    public string? ShipAddress { get; set; }
    public string? ShipCity { get; set; }
    public string? ShipRegion { get; set; }
    public string? ShipPostalCode { get; set; }
    public string? ShipCountry { get; set; }
    public List<OrderLineInput>? Details { get; set; }
}

public sealed record OrderLineInput(int ProductId, int Quantity, decimal? UnitPrice, double Discount);

public static class BusinessDate
{
    /// <summary>The API takes local business dates without an offset; normalize picker values (which may be UTC) to that.</summary>
    public static DateTime? From(DateTime? value) => value is not { } d ? null
        : DateTime.SpecifyKind((d.Kind == DateTimeKind.Utc ? d.ToLocalTime() : d).Date, DateTimeKind.Unspecified);
}

/// <summary>Removable filter chip on the Orders toolbar; <see cref="Query"/> is a grid-query filter (field:op:value).</summary>
public sealed record OrderFilter(string Key, string Label, string Query);
