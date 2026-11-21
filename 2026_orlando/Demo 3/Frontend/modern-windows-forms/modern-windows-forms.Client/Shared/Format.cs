using System.Globalization;

namespace modern_windows_forms.Client.Shared;

/// <summary>Display formatting used across screens (compact currency per the design system: $41.7M, $492k).</summary>
public static class Format
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static string Money(decimal value) => value.ToString("C2", Us);
    public static string Money0(decimal value) => value.ToString("C0", Us);

    public static string Compact(decimal value) => Math.Abs(value) switch
    {
        >= 1_000_000m => "$" + (value / 1_000_000m).ToString("0.##", Us) + "M",
        >= 1_000m => "$" + (value / 1_000m).ToString("0.#", Us) + "k",
        _ => value.ToString("C0", Us)
    };

    public static string Number(decimal value) => value.ToString("N0", Us);
    public static string Percent(decimal value, int decimals = 1) => value.ToString("N" + decimals, Us) + "%";
    public static string Signed(decimal value, int decimals = 1) => (value >= 0 ? "+" : "−") + Math.Abs(value).ToString("N" + decimals, Us) + "%";

    public static string Date(DateTime? value) => value?.ToString("MMM d, yyyy", Us) ?? "—";
    public static string ShortDate(DateTime? value) => value?.ToString("MMM d", Us) ?? "—";

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1 ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
                                 : string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
    }

    /// <summary>Deterministic design-system avatar trio class (av--blue/rose/green/amber) for a name.</summary>
    public static string AvatarClass(string? name)
    {
        var classes = new[] { "av--blue", "av--rose", "av--green", "av--amber" };
        var hash = 0;
        foreach (var ch in name ?? string.Empty) hash = unchecked(hash * 31 + ch);
        return classes[Math.Abs(hash % classes.Length)];
    }
}
