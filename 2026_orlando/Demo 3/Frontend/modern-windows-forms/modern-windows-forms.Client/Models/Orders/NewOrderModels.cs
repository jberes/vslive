namespace modern_windows_forms.Client.Models.Orders;

/// <summary>An editable line on the New order form.</summary>
public sealed class NewOrderLine
{
    private static int nextKey;

    public int Key { get; } = Interlocked.Increment(ref nextKey);
    public int? ProductId { get; set; }
    public string QuantityText { get; set; } = "1";
    /// <summary>Discount as a fraction string ("0", "0.05", …) bound to an IgbSelect.</summary>
    public string Discount { get; set; } = "0";

    public int Quantity => int.TryParse(QuantityText, out var q) ? q : 0;
    public double DiscountValue => double.TryParse(Discount, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
}

/// <summary>Display error for one validation key from a 400 ProblemDetails.</summary>
public sealed record FieldError(string Field, string Message);
