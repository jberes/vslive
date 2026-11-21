namespace modern_windows_forms.Client.Models.Products;

/// <summary>GET /api/categories item.</summary>
public sealed record CategoryItem(int Id, string Name, string? Description, int ProductCount, int ActiveProductCount, decimal TotalSales);

/// <summary>GET /api/categories/{id}/dashboard?year=.</summary>
public sealed record CategoryDashboard(
    CategoryRef Category, int Year, decimal TotalSales, decimal ShareOfTotalSales,
    IReadOnlyList<ProductSales> SalesByProduct, IReadOnlyList<NameValue> SalesByYear,
    IReadOnlyList<NameValue> SalesByQuarter, IReadOnlyList<NameValue> CategoryVsOthers);

public sealed record CategoryRef(int Id, string Name, string? Description);

public sealed record ProductSales(string Product, decimal Sales, int Units);

/// <summary>GET /api/products item.</summary>
public sealed record ProductItem(
    int Id, string Name, int CategoryId, string CategoryName, int SupplierId, string SupplierName,
    string? QuantityPerUnit, decimal UnitPrice, int UnitsInStock, int UnitsOnOrder, int ReorderLevel,
    bool Discontinued, bool NeedsReorder, decimal InventoryValue, int UnitsSold, decimal TotalSales);

/// <summary>GET /api/order-details line (used to total last year's same-period revenue).</summary>
public sealed record OrderLine(int OrderId, int ProductId, string ProductName, string CategoryName, decimal LineTotal, DateTime OrderDate);

/// <summary>Grid row for the stock-vs-reorder grid (class with settable properties for IgbGrid).</summary>
public sealed class StockRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string QuantityPerUnit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int UnitsInStock { get; set; }
    public int ReorderLevel { get; set; }
    public int UnitsOnOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal SalesYtd { get; set; }
}

/// <summary>Grouped-bar point: one quarter with last year and this year.</summary>
public sealed record QuarterPoint(string Quarter, decimal Value);
