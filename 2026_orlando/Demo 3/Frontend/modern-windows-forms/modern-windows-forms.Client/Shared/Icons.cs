namespace modern_windows_forms.Client.Shared;

/// <summary>
/// Lucide outline icons (24px grid, stroke 2, round caps/joins, currentColor) — the icon style the
/// ApexCharts design system specifies. Registered once into the Ignite UI icon registry under
/// <see cref="Collection"/> by <c>IconRegistrar</c>; use with <c>&lt;IgbIcon IconName="…" Collection="lucide" /&gt;</c>.
/// </summary>
public static class Icons
{
    public const string Collection = "lucide";

    private static readonly Dictionary<string, string> Paths = new()
    {
        ["home"] = "<rect x='3' y='3' width='7' height='9' rx='1'/><rect x='14' y='3' width='7' height='5' rx='1'/><rect x='14' y='12' width='7' height='9' rx='1'/><rect x='3' y='16' width='7' height='5' rx='1'/>",
        ["users"] = "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M22 21v-2a4 4 0 0 0-3-3.87'/><path d='M16 3.13a4 4 0 0 1 0 7.75'/>",
        ["orders"] = "<rect x='8' y='2' width='8' height='4' rx='1'/><path d='M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2'/><path d='M12 11h4'/><path d='M12 16h4'/><path d='M8 11h.01'/><path d='M8 16h.01'/>",
        ["plus-circle"] = "<circle cx='12' cy='12' r='10'/><path d='M8 12h8'/><path d='M12 8v8'/>",
        ["package"] = "<path d='M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z'/><path d='M3.27 6.96 12 12.01l8.73-5.05'/><path d='M12 22.08V12'/>",
        ["bar-chart"] = "<path d='M3 3v18h18'/><path d='M18 17V9'/><path d='M13 17V5'/><path d='M8 17v-3'/>",
        ["gantt"] = "<path d='M3 3v16a2 2 0 0 0 2 2h16'/><path d='M7 7h7'/><path d='M10 12h8'/><path d='M8 17h5'/>",
        ["kanban"] = "<path d='M6 5v11'/><path d='M12 5v6'/><path d='M18 5v14'/>",
        ["megaphone"] = "<path d='m3 11 18-5v12L3 14v-3z'/><path d='M11.6 16.8a3 3 0 1 1-5.8-1.6'/>",
        ["layers"] = "<path d='m12 2 10 5-10 5L2 7z'/><path d='m2 17 10 5 10-5'/><path d='m2 12 10 5 10-5'/>",
        ["tablet"] = "<rect x='4' y='2' width='16' height='20' rx='2'/><path d='M12 18h.01'/>",
        ["phone"] = "<rect x='5' y='2' width='14' height='20' rx='2'/><path d='M12 18h.01'/>",
        ["search"] = "<circle cx='11' cy='11' r='8'/><path d='m21 21-4.3-4.3'/>",
        ["bell"] = "<path d='M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9'/><path d='M10.3 21a1.94 1.94 0 0 0 3.4 0'/>",
        ["menu"] = "<path d='M4 6h16'/><path d='M4 12h16'/><path d='M4 18h16'/>",
        ["plus"] = "<path d='M12 5v14'/><path d='M5 12h14'/>",
        ["minus"] = "<path d='M5 12h14'/>",
        ["x"] = "<path d='M18 6 6 18'/><path d='m6 6 12 12'/>",
        ["check"] = "<path d='M20 6 9 17l-5-5'/>",
        ["chevron-right"] = "<path d='m9 18 6-6-6-6'/>",
        ["chevron-left"] = "<path d='m15 18-6-6 6-6'/>",
        ["chevron-down"] = "<path d='m6 9 6 6 6-6'/>",
        ["chevron-up"] = "<path d='m18 15-6-6-6 6'/>",
        ["arrow-up"] = "<path d='m5 12 7-7 7 7'/><path d='M12 19V5'/>",
        ["arrow-down"] = "<path d='M12 5v14'/><path d='m19 12-7 7-7-7'/>",
        ["arrow-right"] = "<path d='M5 12h14'/><path d='m12 5 7 7-7 7'/>",
        ["trending-up"] = "<path d='m22 7-8.5 8.5-5-5L2 17'/><path d='M16 7h6v6'/>",
        ["trending-down"] = "<path d='m22 17-8.5-8.5-5 5L2 7'/><path d='M16 17h6v-6'/>",
        ["truck"] = "<path d='M14 18V6a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v11a1 1 0 0 0 1 1h2'/><path d='M15 18H9'/><path d='M19 18h2a1 1 0 0 0 1-1v-3.65a1 1 0 0 0-.22-.62L18.3 9.38a1 1 0 0 0-.78-.38H14'/><circle cx='17' cy='18' r='2'/><circle cx='7' cy='18' r='2'/>",
        ["calendar"] = "<rect x='3' y='4' width='18' height='18' rx='2'/><path d='M16 2v4'/><path d='M8 2v4'/><path d='M3 10h18'/>",
        ["clock"] = "<circle cx='12' cy='12' r='10'/><path d='M12 6v6l4 2'/>",
        ["filter"] = "<path d='M22 3H2l8 9.46V19l4 2v-8.54L22 3z'/>",
        ["download"] = "<path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/><path d='m7 10 5 5 5-5'/><path d='M12 15V3'/>",
        ["more"] = "<circle cx='12' cy='12' r='1'/><circle cx='19' cy='12' r='1'/><circle cx='5' cy='12' r='1'/>",
        ["alert-triangle"] = "<path d='m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3'/><path d='M12 9v4'/><path d='M12 17h.01'/>",
        ["alert-circle"] = "<circle cx='12' cy='12' r='10'/><path d='M12 8v4'/><path d='M12 16h.01'/>",
        ["info"] = "<circle cx='12' cy='12' r='10'/><path d='M12 16v-4'/><path d='M12 8h.01'/>",
        ["check-circle"] = "<path d='M22 11.08V12a10 10 0 1 1-5.93-9.14'/><path d='m9 11 3 3L22 4'/>",
        ["x-circle"] = "<circle cx='12' cy='12' r='10'/><path d='m15 9-6 6'/><path d='m9 9 6 6'/>",
        ["refresh"] = "<path d='M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8'/><path d='M21 3v5h-5'/><path d='M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16'/><path d='M8 16H3v5'/>",
        ["wifi-off"] = "<path d='M12 20h.01'/><path d='M8.5 16.43a5 5 0 0 1 7 0'/><path d='M2 8.82a15 15 0 0 1 4.17-2.65'/><path d='M10.66 5c4.01-.36 8.14.9 11.34 3.76'/><path d='M16.85 11.25a10 10 0 0 1 2.22 1.68'/><path d='M5 13a10 10 0 0 1 5.24-2.76'/><path d='m2 2 20 20'/>",
        ["mail"] = "<rect x='2' y='4' width='20' height='16' rx='2'/><path d='m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7'/>",
        ["phone-call"] = "<path d='M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z'/>",
        ["map-pin"] = "<path d='M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z'/><circle cx='12' cy='10' r='3'/>",
        ["edit"] = "<path d='M12 20h9'/><path d='M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z'/>",
        ["trash"] = "<path d='M3 6h18'/><path d='M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6'/><path d='M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2'/>",
        ["undo"] = "<path d='M3 7v6h6'/><path d='M21 17a9 9 0 0 0-9-9 9 9 0 0 0-6 2.3L3 13'/>",
        ["user-plus"] = "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M19 8v6'/><path d='M22 11h-6'/>",
        ["indent"] = "<path d='m3 8 4 4-4 4'/><path d='M21 12H11'/><path d='M21 6H11'/><path d='M21 18H11'/>",
        ["outdent"] = "<path d='m7 8-4 4 4 4'/><path d='M21 12H11'/><path d='M21 6H11'/><path d='M21 18H11'/>",
        ["zoom-in"] = "<circle cx='11' cy='11' r='8'/><path d='m21 21-4.3-4.3'/><path d='M11 8v6'/><path d='M8 11h6'/>",
        ["zoom-out"] = "<circle cx='11' cy='11' r='8'/><path d='m21 21-4.3-4.3'/><path d='M8 11h6'/>",
        ["flag"] = "<path d='M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z'/><path d='M4 22v-7'/>",
        ["diamond"] = "<path d='M2.7 10.3a2.41 2.41 0 0 0 0 3.41l7.59 7.59a2.41 2.41 0 0 0 3.41 0l7.59-7.59a2.41 2.41 0 0 0 0-3.41l-7.59-7.59a2.41 2.41 0 0 0-3.41 0Z'/>",
        ["link"] = "<path d='M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71'/><path d='M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71'/>",
        ["percent"] = "<path d='M19 5 5 19'/><circle cx='6.5' cy='6.5' r='2.5'/><circle cx='17.5' cy='17.5' r='2.5'/>",
        ["file-text"] = "<path d='M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z'/><path d='M14 2v4a2 2 0 0 0 2 2h4'/><path d='M10 9H8'/><path d='M16 13H8'/><path d='M16 17H8'/>",
        ["grip"] = "<circle cx='9' cy='12' r='1'/><circle cx='9' cy='5' r='1'/><circle cx='9' cy='19' r='1'/><circle cx='15' cy='12' r='1'/><circle cx='15' cy='5' r='1'/><circle cx='15' cy='19' r='1'/>",
        ["star"] = "<path d='M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z'/>",
        ["dollar"] = "<path d='M12 2v20'/><path d='M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6'/>",
        ["cart"] = "<circle cx='8' cy='21' r='1'/><circle cx='19' cy='21' r='1'/><path d='M2.05 2.05h2l2.66 12.42a2 2 0 0 0 2 1.58h9.78a2 2 0 0 0 1.95-1.57l1.65-7.43H5.12'/>",
        ["table"] = "<path d='M12 3v18'/><rect x='3' y='3' width='18' height='18' rx='2'/><path d='M3 9h18'/><path d='M3 15h18'/>",
        ["line-chart"] = "<path d='M3 3v18h18'/><path d='m19 9-5 5-4-4-3 3'/>",
        ["pie-chart"] = "<path d='M21.21 15.89A10 10 0 1 1 8 2.83'/><path d='M22 12A10 10 0 0 0 12 2v10z'/>",
        ["globe"] = "<circle cx='12' cy='12' r='10'/><path d='M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20'/><path d='M2 12h20'/>",
        ["briefcase"] = "<rect x='2' y='7' width='20' height='14' rx='2'/><path d='M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16'/>",
        ["award"] = "<circle cx='12' cy='8' r='6'/><path d='M15.48 12.89 17 22l-5-3-5 3 1.52-9.11'/>",
        ["external-link"] = "<path d='M15 3h6v6'/><path d='M10 14 21 3'/><path d='M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6'/>",
        ["settings"] = "<path d='M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z'/><circle cx='12' cy='12' r='3'/>",
        ["sliders"] = "<path d='M4 21v-7'/><path d='M4 10V3'/><path d='M12 21v-9'/><path d='M12 8V3'/><path d='M20 21v-5'/><path d='M20 12V3'/><path d='M1 14h6'/><path d='M9 8h6'/><path d='M17 16h6'/>",
        ["eye"] = "<path d='M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z'/><circle cx='12' cy='12' r='3'/>",
        ["list"] = "<path d='M8 6h13'/><path d='M8 12h13'/><path d='M8 18h13'/><path d='M3 6h.01'/><path d='M3 12h.01'/><path d='M3 18h.01'/>",
        ["inbox"] = "<path d='M22 12h-6l-2 3h-4l-2-3H2'/><path d='M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z'/>",
        ["lock"] = "<rect x='3' y='11' width='18' height='11' rx='2'/><path d='M7 11V7a5 5 0 0 1 10 0v4'/>",
        ["copy"] = "<rect x='8' y='8' width='14' height='14' rx='2'/><path d='M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2'/>",
        ["sticky-note"] = "<path d='M15.5 3H5a2 2 0 0 0-2 2v14c0 1.1.9 2 2 2h14a2 2 0 0 0 2-2V8.5L15.5 3Z'/><path d='M15 3v6h6'/>",
    };

    public static IEnumerable<string> Names => Paths.Keys;

    public static string Svg(string name) =>
        $"<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' style='fill:none;stroke:currentColor'>{Paths[name]}</svg>";
}
