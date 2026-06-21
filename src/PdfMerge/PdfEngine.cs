using System.Drawing;
using System.Drawing.Imaging;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using ps = PdfSharp.Pdf;
using psio = PdfSharp.Pdf.IO;
using it = iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;

namespace PdfMerge;

/// Motore: merge (f1 intero, f2 senza prima pagina, f3 accodato),
/// salto pagine vuote (PdfSharp) e compressione immagini scannerizzate (iText7).
public static class PdfEngine
{
    private static readonly HashSet<string> ContentOps = new()
    {
        "Tj", "TJ", "'", "\"",
        "Do",
        "f", "F", "f*", "B", "B*", "b", "b*", "S", "s", "sh",
    };

    public static void Run(string f1, string? f2, string? f3, string outPath, MergeOptions opt)
    {
        if (string.IsNullOrEmpty(f1))
            throw new ArgumentException("primo file mancante");

        using var output = new ps.PdfDocument();

        AppendAll(output, f1);
        if (!string.IsNullOrEmpty(f2)) AppendSkippingFirst(output, f2);
        if (!string.IsNullOrEmpty(f3)) AppendAll(output, f3);

        if (opt.SkipEmpty) RemoveEmptyPages(output);

        if (opt.Compress)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "pdfmerge_" + Guid.NewGuid().ToString("N") + ".pdf");
            output.Save(tmp);
            try
            {
                CompressImages(tmp, outPath, opt.MaxSide, opt.JpegQuality);
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

    // ---- merge / pagine vuote (PdfSharp) ----

    private static void AppendAll(ps.PdfDocument output, string file)
    {
        using var input = psio.PdfReader.Open(file, psio.PdfDocumentOpenMode.Import);
        for (int i = 0; i < input.PageCount; i++)
            output.AddPage(input.Pages[i]);
    }

    private static void AppendSkippingFirst(ps.PdfDocument output, string file)
    {
        using var input = psio.PdfReader.Open(file, psio.PdfDocumentOpenMode.Import);
        for (int i = 1; i < input.PageCount; i++)
            output.AddPage(input.Pages[i]);
    }

    private static void RemoveEmptyPages(ps.PdfDocument doc)
    {
        for (int i = doc.PageCount - 1; i >= 0; i--)
        {
            if (doc.PageCount <= 1) break;
            if (!PageHasContent(doc.Pages[i]))
                doc.Pages.RemoveAt(i);
        }
    }

    private static bool PageHasContent(ps.PdfPage page)
    {
        CSequence seq;
        try { seq = ContentReader.ReadContent(page); }
        catch { return true; }
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

    // ---- compressione immagini (iText7 + System.Drawing) ----

    /// La compressione è pura managed (nessuna dipendenza nativa); su Windows usa System.Drawing.
    public static bool CompressionAvailable() => OperatingSystem.IsWindows();

    private static void CompressImages(string inPath, string outPath, int maxSide, int quality)
    {
        if (maxSide <= 0) maxSide = 1600;
        if (quality < 1 || quality > 100) quality = 80;

        using var reader = new it.PdfReader(inPath);
        using var writer = new it.PdfWriter(outPath,
            new it.WriterProperties().SetFullCompressionMode(true));
        using var pdf = new it.PdfDocument(reader, writer);

        for (int p = 1; p <= pdf.GetNumberOfPages(); p++)
        {
            var res = pdf.GetPage(p).GetResources().GetPdfObject();
            var xobjs = res.GetAsDictionary(it.PdfName.XObject);
            if (xobjs == null) continue;

            foreach (var key in xobjs.KeySet())
            {
                if (xobjs.Get(key) is not it.PdfStream stream) continue;
                if (!it.PdfName.Image.Equals(stream.GetAsName(it.PdfName.Subtype))) continue;
                TryRecompress(stream, maxSide, quality);
            }
        }

        pdf.Close();
    }

    private static void TryRecompress(it.PdfStream stream, int maxSide, int quality)
    {
        try
        {
            var xobj = new PdfImageXObject(stream);
            byte[] decoded = xobj.GetImageBytes(); // pixel decodificati (png/jpg)

            using var ms = new MemoryStream(decoded);
            using var src = new Bitmap(ms);

            int w = src.Width, h = src.Height;
            double scale = 1.0;
            int longSide = Math.Max(w, h);
            if (longSide > maxSide) scale = (double)maxSide / longSide;

            int nw = Math.Max(1, (int)Math.Round(w * scale));
            int nh = Math.Max(1, (int)Math.Round(h * scale));

            byte[] jpeg = ToJpeg(src, nw, nh, quality);

            // se non si guadagna nulla e non si riduce, lascia l'originale
            if (jpeg.Length >= decoded.Length && scale == 1.0)
                return;

            stream.Clear();
            stream.SetData(jpeg);
            stream.Put(it.PdfName.Type, it.PdfName.XObject);
            stream.Put(it.PdfName.Subtype, it.PdfName.Image);
            stream.Put(it.PdfName.Width, new it.PdfNumber(nw));
            stream.Put(it.PdfName.Height, new it.PdfNumber(nh));
            stream.Put(it.PdfName.BitsPerComponent, new it.PdfNumber(8));
            stream.Put(it.PdfName.ColorSpace, it.PdfName.DeviceRGB);
            stream.Put(it.PdfName.Filter, it.PdfName.DCTDecode);
            stream.Remove(it.PdfName.DecodeParms);
            stream.Remove(it.PdfName.SMask);
            stream.Remove(it.PdfName.Decode);
        }
        catch
        {
            // immagine non gestibile (CMYK, mascherata, ecc.) -> lascia invariata
        }
    }

    private static byte[] ToJpeg(Bitmap src, int nw, int nh, int quality)
    {
        using var dst = new Bitmap(nw, nh, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(dst))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.Clear(Color.White);
            g.DrawImage(src, 0, 0, nw, nh);
        }

        var enc = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

        using var outMs = new MemoryStream();
        dst.Save(outMs, enc, ep);
        return outMs.ToArray();
    }
}
