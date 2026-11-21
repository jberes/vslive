using System.Globalization;
using System.Text;

namespace BusinessApp.Api.Endpoints;

/// <summary>
/// Deterministic cartoon avatars (like the contact pictures in the WinForms app), generated as SVG
/// so no binary assets are stored in the database. The same kind/id always yields the same face.
/// </summary>
public static class AvatarEndpoints
{
    private static readonly string[] Kinds = ["customers", "employees", "leads", "resources", "suppliers"];
    private static readonly string[] Backgrounds = ["#E3F2FD", "#FCE4EC", "#E8F5E9", "#FFF3E0", "#EDE7F6", "#E0F7FA", "#FFFDE7", "#F3E5F5", "#ECEFF1"];
    private static readonly string[] Skins = ["#FFDBB4", "#EDB98A", "#D08B5B", "#AE5D29", "#614335", "#F8D25C", "#FFE0BD"];
    private static readonly string[] Hairs = ["#2C1B18", "#4A312C", "#724133", "#A55728", "#B58143", "#D6B370", "#C93305", "#E8E1E1", "#6A6A6A"];
    private static readonly string[] Shirts = ["#1976D2", "#388E3C", "#D32F2F", "#7B1FA2", "#F57C00", "#0097A7", "#455A64", "#C2185B", "#5D4037"];

    public static void MapAvatarEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/avatars/{kind}/{file}", (string kind, string file, HttpContext http) =>
        {
            var name = file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
            if (!Kinds.Contains(kind, StringComparer.OrdinalIgnoreCase) || !int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                return Results.NotFound();

            http.Response.Headers.CacheControl = "public, max-age=604800, immutable";
            return Results.Text(Render(kind.ToLowerInvariant(), id), "image/svg+xml", Encoding.UTF8);
        }).WithTags("Avatars");
    }

    internal static string Render(string kind, int id)
    {
        var rng = new Random(StableHash($"{kind}:{id}"));
        string Pick(string[] a) => a[rng.Next(a.Length)];

        var bg = Pick(Backgrounds);
        var skin = Pick(Skins);
        var hair = Pick(Hairs);
        var shirt = Pick(Shirts);
        var longHair = rng.Next(2) == 0;
        var glasses = rng.Next(4) == 0;
        var beard = !longHair && rng.Next(5) == 0;
        var smile = rng.Next(3);
        var bald = !longHair && rng.Next(8) == 0;

        var sb = new StringBuilder();
        sb.Append("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" width="128" height="128">""");
        sb.Append($"""<rect width="128" height="128" rx="64" fill="{bg}"/>""");
        sb.Append("""<clipPath id="c"><rect width="128" height="128" rx="64"/></clipPath><g clip-path="url(#c)">""");
        if (longHair) sb.Append($"""<path d="M30 60 Q28 112 44 118 L84 118 Q100 112 98 60 Z" fill="{hair}"/>""");
        sb.Append($"""<path d="M22 128 Q24 98 64 94 Q104 98 106 128 Z" fill="{shirt}"/>""");
        sb.Append($"""<rect x="56" y="80" width="16" height="18" rx="6" fill="{skin}"/>""");
        sb.Append($"""<ellipse cx="64" cy="60" rx="28" ry="32" fill="{skin}"/>""");
        sb.Append($"""<ellipse cx="36" cy="62" rx="5" ry="7" fill="{skin}"/><ellipse cx="92" cy="62" rx="5" ry="7" fill="{skin}"/>""");
        if (!bald)
            sb.Append(longHair
                ? $"""<path d="M34 58 Q32 26 64 24 Q96 26 94 58 Q86 40 64 38 Q44 38 34 58 Z" fill="{hair}"/>"""
                : $"""<path d="M35 52 Q34 26 64 26 Q94 26 93 52 Q88 38 72 38 Q52 34 40 44 Z" fill="{hair}"/>""");
        if (beard) sb.Append($"""<path d="M38 66 Q40 96 64 96 Q88 96 90 66 Q84 82 64 82 Q44 82 38 66 Z" fill="{hair}"/>""");
        sb.Append("""<ellipse cx="52" cy="60" rx="3.5" ry="4.2" fill="#2B2B2B"/><ellipse cx="76" cy="60" rx="3.5" ry="4.2" fill="#2B2B2B"/>""");
        sb.Append($"""<path d="M46 50 Q52 47 58 50" stroke="{hair}" stroke-width="2.5" fill="none" stroke-linecap="round"/>""");
        sb.Append($"""<path d="M70 50 Q76 47 82 50" stroke="{hair}" stroke-width="2.5" fill="none" stroke-linecap="round"/>""");
        sb.Append(smile switch
        {
            0 => """<path d="M54 76 Q64 84 74 76" stroke="#8B3A3A" stroke-width="3" fill="none" stroke-linecap="round"/>""",
            1 => """<path d="M52 74 Q64 88 76 74 Z" fill="#8B3A3A"/>""",
            _ => """<path d="M56 78 L72 78" stroke="#8B3A3A" stroke-width="3" stroke-linecap="round"/>""",
        });
        if (glasses)
            sb.Append("""<g stroke="#333" stroke-width="2.5" fill="none"><circle cx="52" cy="60" r="9"/><circle cx="76" cy="60" r="9"/><path d="M61 60 L67 60"/></g>""");
        sb.Append("</g></svg>");
        return sb.ToString();
    }

    // string.GetHashCode is randomized per process, so use FNV-1a for stable output.
    private static int StableHash(string s)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in s) h = (h ^ c) * 16777619u;
            return (int)(h & 0x7FFFFFFF);
        }
    }
}
