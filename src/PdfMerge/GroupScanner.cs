using System.Text.RegularExpressions;

namespace PdfMerge;

/// Gruppo di file nome.pN.pdf.
public class PdfGroup
{
    public string Name = "";
    public string? P1;
    public string? P2;
    public string? P3;
}

/// Scansiona una directory per gruppi nome.p1/p2/p3.pdf (confronto case-insensitive).
public static class GroupScanner
{
    private static readonly Regex Re =
        new(@"^(.+)\.p([123])\.pdf$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<PdfGroup> Scan(string dir)
    {
        var map = new Dictionary<string, PdfGroup>();

        foreach (var path in Directory.EnumerateFiles(dir, "*.pdf"))
        {
            var name = Path.GetFileName(path);
            var m = Re.Match(name);
            if (!m.Success) continue;

            var prefix = m.Groups[1].Value;
            var part = m.Groups[2].Value;
            var key = prefix.ToLowerInvariant(); // case-insensitive

            if (!map.TryGetValue(key, out var g))
            {
                g = new PdfGroup { Name = prefix };
                map[key] = g;
            }

            switch (part)
            {
                case "1":
                    g.P1 = name;
                    g.Name = prefix; // il nome di riferimento viene dal file p1
                    break;
                case "2":
                    g.P2 = name;
                    break;
                case "3":
                    g.P3 = name;
                    break;
            }
        }

        return map.Values
            .Where(g => g.P1 != null) // serve almeno la p1
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
