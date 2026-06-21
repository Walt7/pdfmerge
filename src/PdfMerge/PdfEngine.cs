using Ghostscript.NET;
using Ghostscript.NET.Processor;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;

namespace PdfMerge;

/// Motore: merge (f1 intero, f2 senza prima pagina, f3 accodato),
/// salto pagine vuote (PdfSharp) e downsample immagini (Ghostscript.NET).
public static class PdfEngine
{
    // operatori del content stream che indicano contenuto visibile
    private static readonly HashSet<string> ContentOps = new()
    {
        "Tj", "TJ", "'", "\"", // testo
        "Do",                  // XObject (immagini/form)
        "f", "F", "f*", "B", "B*", "b", "b*", "S", "s", "sh", // riempimenti/tracciati/shading
    };

    public static void Run(string f1, string? f2, string? f3, string outPath, MergeOptions opt)
    {
        if (string.IsNullOrEmpty(f1))
            throw new ArgumentException("primo file mancante");

        using var output = new PdfDocument();

        AppendAll(output, f1);
        if (!string.IsNullOrEmpty(f2)) AppendSkippingFirst(output, f2);
        if (!string.IsNullOrEmpty(f3)) AppendAll(output, f3);

        if (opt.SkipEmpty) RemoveEmptyPages(output);

        if (opt.LimitDpi)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "pdfmerge_" + Guid.NewGuid().ToString("N") + ".pdf");
            output.Save(tmp);
            try
            {
                DownsampleImages(tmp, outPath, opt.Dpi);
            }
            finally
            {
                try { File.Delete(tmp); } catch { /* ignore */ }
            }
        }
        else
        {
            output.Save(outPath);
        }
    }

    private static void AppendAll(PdfDocument output, string file)
    {
        using var input = PdfReader.Open(file, PdfDocumentOpenMode.Import);
        for (int i = 0; i < input.PageCount; i++)
            output.AddPage(input.Pages[i]);
    }

    private static void AppendSkippingFirst(PdfDocument output, string file)
    {
        using var input = PdfReader.Open(file, PdfDocumentOpenMode.Import);
        for (int i = 1; i < input.PageCount; i++) // salta pagina 0
            output.AddPage(input.Pages[i]);
    }

    private static void RemoveEmptyPages(PdfDocument doc)
    {
        for (int i = doc.PageCount - 1; i >= 0; i--)
        {
            if (doc.PageCount <= 1) break; // non svuotare del tutto
            if (!PageHasContent(doc.Pages[i]))
                doc.Pages.RemoveAt(i);
        }
    }

    private static bool PageHasContent(PdfPage page)
    {
        CSequence seq;
        try
        {
            seq = ContentReader.ReadContent(page);
        }
        catch
        {
            return true; // in dubbio, tieni la pagina
        }
        return SequenceHasOp(seq);
    }

    private static bool SequenceHasOp(CSequence seq)
    {
        foreach (CObject o in seq)
        {
            switch (o)
            {
                case COperator op when ContentOps.Contains(op.OpCode.Name):
                    return true;
                case CSequence nested when SequenceHasOp(nested):
                    return true;
            }
        }
        return false;
    }

    /// True se Ghostscript è installato sulla macchina.
    public static bool GhostscriptAvailable()
    {
        try
        {
            return GhostscriptVersionInfo.GetLastInstalledVersion() != null;
        }
        catch
        {
            return false;
        }
    }

    private static void DownsampleImages(string inPath, string outPath, int dpi)
    {
        if (dpi <= 0) dpi = 150;

        var gs = GhostscriptVersionInfo.GetLastInstalledVersion()
                 ?? throw new InvalidOperationException("Ghostscript non trovato.");

        var d = dpi.ToString();
        var args = new[]
        {
            "gs", // argv[0], ignorato
            "-dNOPAUSE", "-dBATCH", "-dQUIET", "-dSAFER",
            "-sDEVICE=pdfwrite",
            "-dCompatibilityLevel=1.5",
            "-dDownsampleColorImages=true", "-dColorImageResolution=" + d,
            "-dDownsampleGrayImages=true", "-dGrayImageResolution=" + d,
            "-dDownsampleMonoImages=true", "-dMonoImageResolution=" + d,
            "-dColorImageDownsampleType=/Bicubic",
            "-dGrayImageDownsampleType=/Bicubic",
            "-sOutputFile=" + outPath,
            inPath,
        };

        using var processor = new GhostscriptProcessor(gs);
        processor.StartProcessing(args, null);
    }
}
