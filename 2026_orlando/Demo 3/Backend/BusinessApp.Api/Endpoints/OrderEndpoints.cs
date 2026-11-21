using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class OrderDto
{
    public int Id { get; init; }
    public int CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public int EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public DateTime OrderDate { get; init; }
    public DateTime RequiredDate { get; init; }
    public DateTime? ShippedDate { get; init; }
    public int ShipViaId { get; init; }
    public string? ShipViaName { get; init; }
    public decimal Freight { get; init; }
    public string? ShipName { get; init; }
    public string? ShipAddress { get; init; }
    public string? ShipCity { get; init; }
    public string? ShipRegion { get; init; }
    public string? ShipPostalCode { get; init; }
    public string? ShipCountry { get; init; }
    public int ItemCount { get; init; }
    public double Subtotal { get; init; }
    public double Total { get; init; }
    /// <summary>Shipped, Pending or Overdue.</summary>
    public required string Status { get; init; }
}

public sealed class OrderLineDto
{
    public int OrderId { get; init; }
    public int ProductId { get; init; }
    public required string ProductName { get; init; }
    public string? CategoryName { get; init; }
    public string? QuantityPerUnit { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public double Discount { get; init; }
    public double LineTotal { get; init; }
    public DateTime OrderDate { get; init; }
    public int CustomerId { get; init; }
    public string? CustomerName { get; init; }
}

public sealed class OrderLineInput
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, 10_000)] public int Quantity { get; set; }
    /// <summary>Defaults to the product's current list price.</summary>
    [Range(0, 1_000_000)] public decimal? UnitPrice { get; set; }
    [Range(0, 1)] public double Discount { get; set; }
}

public sealed class OrderInput
{
    [Range(1, int.MaxValue)] public int CustomerId { get; set; }
    [Range(1, int.MaxValue)] public int EmployeeId { get; set; }
    [Range(1, int.MaxValue)] public int ShipViaId { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    [Range(0, 1_000_000)] public decimal Freight { get; set; }
    /// <summary>Ship-to fields default to the customer's address when omitted.</summary>
    [StringLength(128)] public string? ShipName { get; set; }
    [StringLength(128)] public string? ShipAddress { get; set; }
    [StringLength(64)] public string? ShipCity { get; set; }
    [StringLength(64)] public string? ShipRegion { get; set; }
    [StringLength(16)] public string? ShipPostalCode { get; set; }
    [StringLength(64)] public string? ShipCountry { get; set; }
    /// <summary>Required on create; ignored on update (use the details endpoints).</summary>
    public List<OrderLineInput>? Details { get; set; }
}

public sealed record ShipOrderInput(DateTime? ShippedDate, int? ShipViaId);

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/orders").WithTags("Orders");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Orders).ToPagedResultAsync(q, Search, "-orderDate", ct));

        g.MapGet("/{id:int}", GetOrder);
        g.MapGet("/{id:int}/dashboard", Dashboard);

        g.MapPost("/", async (OrderInput input, AppDbContext db, CancellationToken ct) =>
        {
            if (input.Details is not { Count: > 0 })
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["details"] = ["An order needs at least one line."] });
            if (await ValidateHeader(input, db, ct) is { } problem) return problem;

            var customer = (await db.Customers.FindAsync([input.CustomerId], ct))!;
            var order = new Order { Id = await NextOrderId(db, ct) };
            ApplyHeader(order, input, customer);
            db.Orders.Add(order);

            var lineResult = await ApplyLines(order, input.Details, db, ct);
            if (lineResult is not null) return lineResult;

            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/orders/{order.Id}", await LoadOrder(order.Id, db, ct));
        });

        g.MapPut("/{id:int}", async (int id, OrderInput input, AppDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders.FindAsync([id], ct);
            if (order is null) return Results.NotFound();
            if (await ValidateHeader(input, db, ct) is { } problem) return problem;
            var customer = (await db.Customers.FindAsync([input.CustomerId], ct))!;
            ApplyHeader(order, input, customer);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadOrder(id, db, ct));
        });

        g.MapPost("/{id:int}/ship", async (int id, ShipOrderInput? input, AppDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders.FindAsync([id], ct);
            if (order is null) return Results.NotFound();
            if (order.ShippedDate is not null) return Results.Problem("Order has already shipped.", statusCode: 409);
            if (input?.ShipViaId is int shipVia)
            {
                if (!await db.Shippers.AnyAsync(s => s.Id == shipVia, ct))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["shipViaId"] = ["Shipper not found."] });
                order.ShipViaId = shipVia;
            }
            order.ShippedDate = input?.ShippedDate ?? DateTime.Now;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadOrder(id, db, ct));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders.Include(o => o.Details).FirstOrDefaultAsync(o => o.Id == id, ct);
            if (order is null) return Results.NotFound();
            if (order.ShippedDate is null) await RestockAsync(order.Details, db, ct);
            db.Orders.Remove(order);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ----- Order lines -----
        g.MapGet("/{id:int}/details", async (int id, AppDbContext db, CancellationToken ct) =>
            await ProjectLines(db.OrderDetails.Where(d => d.OrderId == id)).OrderBy(d => d.ProductName).ToListAsync(ct));

        g.MapPost("/{id:int}/details", async (int id, OrderLineInput line, AppDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders.Include(o => o.Details).FirstOrDefaultAsync(o => o.Id == id, ct);
            if (order is null) return Results.NotFound();
            if (order.Details.Any(d => d.ProductId == line.ProductId))
                return Results.Problem("Product is already on this order. Update the existing line instead.", statusCode: 409);
            if (await ApplyLines(order, [line], db, ct) is { } problem) return problem;
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/orders/{id}/details/{line.ProductId}",
                await ProjectLines(db.OrderDetails.Where(d => d.OrderId == id && d.ProductId == line.ProductId)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}/details/{productId:int}", async (int id, int productId, OrderLineInput line, AppDbContext db, CancellationToken ct) =>
        {
            var detail = await db.OrderDetails.Include(d => d.Product).Include(d => d.Order)
                .FirstOrDefaultAsync(d => d.OrderId == id && d.ProductId == productId, ct);
            if (detail is null) return Results.NotFound();
            if (detail.Order!.ShippedDate is null) detail.Product!.UnitsInStock -= line.Quantity - detail.Quantity;
            detail.Quantity = line.Quantity;
            detail.UnitPrice = line.UnitPrice ?? detail.UnitPrice;
            detail.Discount = line.Discount;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await ProjectLines(db.OrderDetails.Where(d => d.OrderId == id && d.ProductId == productId)).FirstAsync(ct));
        });

        g.MapDelete("/{id:int}/details/{productId:int}", async (int id, int productId, AppDbContext db, CancellationToken ct) =>
        {
            var detail = await db.OrderDetails.Include(d => d.Order).FirstOrDefaultAsync(d => d.OrderId == id && d.ProductId == productId, ct);
            if (detail is null) return Results.NotFound();
            if (await db.OrderDetails.CountAsync(d => d.OrderId == id, ct) == 1)
                return Results.Problem("Cannot remove the last line; delete the order instead.", statusCode: 409);
            if (detail.Order!.ShippedDate is null) await RestockAsync([detail], db, ct);
            db.OrderDetails.Remove(detail);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Flat list of all order lines for the "Order Details" grid (group by orderId client-side).
        api.MapGet("/order-details", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await ProjectLines(db.OrderDetails).ToPagedResultAsync(q, s => d =>
                d.ProductName.ToLower().Contains(s) || (d.CustomerName ?? "").ToLower().Contains(s) ||
                (d.CategoryName ?? "").ToLower().Contains(s) || d.OrderId.ToString() == s,
                "-orderId,productName", ct)).WithTags("Orders");
    }

    internal static Expression<Func<OrderDto, bool>> Search(string s) => o =>
        o.Id.ToString() == s || (o.CustomerName ?? "").ToLower().Contains(s) || (o.EmployeeName ?? "").ToLower().Contains(s) ||
        (o.ShipCity ?? "").ToLower().Contains(s) || (o.ShipCountry ?? "").ToLower().Contains(s) || (o.ShipName ?? "").ToLower().Contains(s);

    internal static IQueryable<OrderDto> Project(IQueryable<Order> q)
    {
        var today = DateTime.Today;
        return q.Select(o => new OrderDto
        {
            Id = o.Id,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer!.CompanyName,
            EmployeeId = o.EmployeeId,
            EmployeeName = o.Employee!.LastName,
            OrderDate = o.OrderDate,
            RequiredDate = o.RequiredDate,
            ShippedDate = o.ShippedDate,
            ShipViaId = o.ShipViaId,
            ShipViaName = o.ShipVia!.CompanyName,
            Freight = o.Freight,
            ShipName = o.ShipName,
            ShipAddress = o.ShipAddress,
            ShipCity = o.ShipCity,
            ShipRegion = o.ShipRegion,
            ShipPostalCode = o.ShipPostalCode,
            ShipCountry = o.ShipCountry,
            ItemCount = o.Details.Count,
            Subtotal = Math.Round(o.Details.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
            Total = Math.Round(o.Details.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) + (double)o.Freight, 2),
            Status = o.ShippedDate != null ? "Shipped" : o.RequiredDate < today ? "Overdue" : "Pending",
        });
    }

    internal static IQueryable<OrderLineDto> ProjectLines(IQueryable<OrderDetail> q) => q.Select(d => new OrderLineDto
    {
        OrderId = d.OrderId,
        ProductId = d.ProductId,
        ProductName = d.Product!.Name,
        CategoryName = d.Product.Category!.Name,
        QuantityPerUnit = d.Product.QuantityPerUnit,
        UnitPrice = d.UnitPrice,
        Quantity = d.Quantity,
        Discount = d.Discount,
        LineTotal = Math.Round((double)d.UnitPrice * d.Quantity * (1 - d.Discount), 2),
        OrderDate = d.Order!.OrderDate,
        CustomerId = d.Order.CustomerId,
        CustomerName = d.Order.Customer!.CompanyName,
    });

    private static async Task<IResult> GetOrder(int id, AppDbContext db, CancellationToken ct) =>
        await LoadOrder(id, db, ct) is { } order ? Results.Ok(order) : Results.NotFound();

    private static async Task<object?> LoadOrder(int id, AppDbContext db, CancellationToken ct)
    {
        var header = await Project(db.Orders.Where(o => o.Id == id)).FirstOrDefaultAsync(ct);
        if (header is null) return null;
        var details = await ProjectLines(db.OrderDetails.Where(d => d.OrderId == id)).OrderBy(d => d.ProductName).ToListAsync(ct);
        return new { order = header, details };
    }

    private static async Task<int> NextOrderId(AppDbContext db, CancellationToken ct) =>
        (await db.Orders.MaxAsync(o => (int?)o.Id, ct) ?? 10247) + 1;

    private static async Task<IResult?> ValidateHeader(OrderInput i, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!await db.Customers.AnyAsync(c => c.Id == i.CustomerId, ct)) errors["customerId"] = ["Customer not found."];
        if (!await db.Employees.AnyAsync(e => e.Id == i.EmployeeId, ct)) errors["employeeId"] = ["Employee not found."];
        if (!await db.Shippers.AnyAsync(s => s.Id == i.ShipViaId, ct)) errors["shipViaId"] = ["Shipper not found."];
        if (i.RequiredDate is { } req && i.OrderDate is { } od && req < od) errors["requiredDate"] = ["Required date must be on or after the order date."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static void ApplyHeader(Order o, OrderInput i, Customer c)
    {
        o.CustomerId = i.CustomerId;
        o.EmployeeId = i.EmployeeId;
        o.ShipViaId = i.ShipViaId;
        o.OrderDate = i.OrderDate ?? (o.OrderDate == default ? DateTime.Now : o.OrderDate);
        o.RequiredDate = i.RequiredDate ?? (o.RequiredDate == default ? o.OrderDate.Date.AddDays(28) : o.RequiredDate);
        o.ShippedDate = i.ShippedDate ?? o.ShippedDate;
        o.Freight = i.Freight;
        o.ShipName = i.ShipName ?? c.CompanyName;
        o.ShipAddress = i.ShipAddress ?? c.Address;
        o.ShipCity = i.ShipCity ?? c.City;
        o.ShipRegion = i.ShipRegion ?? c.Region;
        o.ShipPostalCode = i.ShipPostalCode ?? c.PostalCode;
        o.ShipCountry = i.ShipCountry ?? c.Country;
    }

    /// <summary>Adds lines to an order and allocates stock (UnitsInStock may go negative = backorder).</summary>
    private static async Task<IResult?> ApplyLines(Order order, List<OrderLineInput> lines, AppDbContext db, CancellationToken ct)
    {
        if (lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["details"] = ["Each product may appear only once per order."] });

        var ids = lines.Select(l => l.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var missing = ids.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["details"] = [$"Unknown product id(s): {string.Join(", ", missing)}."] });
        var discontinued = products.Values.Where(p => p.Discontinued).Select(p => p.Name).ToList();
        if (discontinued.Count > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["details"] = [$"Discontinued product(s): {string.Join(", ", discontinued)}."] });

        foreach (var l in lines)
        {
            var p = products[l.ProductId];
            order.Details.Add(new OrderDetail { ProductId = p.Id, Quantity = l.Quantity, UnitPrice = l.UnitPrice ?? p.UnitPrice, Discount = l.Discount });
            if (order.ShippedDate is null) p.UnitsInStock -= l.Quantity;
        }
        return null;
    }

    private static async Task RestockAsync(IEnumerable<OrderDetail> lines, AppDbContext db, CancellationToken ct)
    {
        foreach (var l in lines)
            if (await db.Products.FindAsync([l.ProductId], ct) is { } p) p.UnitsInStock += l.Quantity;
    }

    /// <summary>"Customer Orders" / "Order Details" charts: how this order compares to the customer's and all other orders.</summary>
    private static async Task<IResult> Dashboard(int id, AppDbContext db, CancellationToken ct)
    {
        var order = await Project(db.Orders.Where(o => o.Id == id)).FirstOrDefaultAsync(ct);
        if (order is null) return Results.NotFound();

        var lines = await ProjectLines(db.OrderDetails.Where(d => d.OrderId == id)).ToListAsync(ct);

        var orderTotals = db.Orders.Select(o => new
        {
            o.Id, o.CustomerId, o.OrderDate,
            Subtotal = o.Details.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)),
            Items = o.Details.Sum(d => d.Quantity),
        });

        var overall = await orderTotals.GroupBy(_ => 1)
            .Select(g => new { Avg = g.Average(x => x.Subtotal), Max = g.Max(x => x.Subtotal), AvgItems = g.Average(x => (double)x.Items) })
            .FirstAsync(ct);
        var smallerCount = await orderTotals.CountAsync(x => x.Subtotal < order.Subtotal, ct);
        var totalCount = await db.Orders.CountAsync(ct);

        var customerHistory = await orderTotals.Where(x => x.CustomerId == order.CustomerId && x.OrderDate <= order.OrderDate)
            .OrderByDescending(x => x.OrderDate).Take(12)
            .Select(x => new { x.Id, x.OrderDate, x.Subtotal, x.Items }).ToListAsync(ct);
        var customerAvg = await orderTotals.Where(x => x.CustomerId == order.CustomerId).AverageAsync(x => (double?)x.Subtotal, ct) ?? 0;

        // How each product on this order usually sells (avg quantity per order line across all orders).
        var productIds = lines.Select(l => l.ProductId).ToList();
        var productNorms = await db.OrderDetails.Where(d => productIds.Contains(d.ProductId))
            .GroupBy(d => d.ProductId)
            .Select(g => new { ProductId = g.Key, AvgQty = g.Average(d => (double)d.Quantity), AvgPrice = g.Average(d => (double)d.UnitPrice) })
            .ToDictionaryAsync(x => x.ProductId, ct);

        return Results.Ok(new
        {
            order,
            details = lines,
            comparison = new
            {
                orderSubtotal = order.Subtotal,
                averageOrderSubtotal = Money.Round(overall.Avg),
                customerAverageOrderSubtotal = Money.Round(customerAvg),
                largestOrderSubtotal = Money.Round(overall.Max),
                percentile = totalCount == 0 ? 0 : Math.Round(smallerCount * 100.0 / totalCount, 1),
                averageItemsPerOrder = Math.Round(overall.AvgItems, 1),
            },
            lineShare = lines.Select(l => new NameValue(l.ProductName, l.LineTotal)),
            linesVsTypical = lines.Select(l => new
            {
                productName = l.ProductName,
                quantity = l.Quantity,
                typicalQuantity = Math.Round(productNorms.GetValueOrDefault(l.ProductId)?.AvgQty ?? 0, 1),
                unitPrice = l.UnitPrice,
                typicalUnitPrice = Money.Round(productNorms.GetValueOrDefault(l.ProductId)?.AvgPrice ?? 0),
            }),
            customerOrderHistory = customerHistory.OrderBy(x => x.OrderDate)
                .Select(x => new { orderId = x.Id, orderDate = x.OrderDate, subtotal = Money.Round(x.Subtotal), items = x.Items, isSelected = x.Id == id }),
        });
    }
}
