using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace PdfMerge;

internal static class Program
{
    private static readonly string Version =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";

    private static string AppTitle => "pdfmerge v" + Version;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            Environment.ExitCode = RunCli(args);
            return;
        }

        ApplicationConfiguration.Initialize();

        var dir = Directory.GetCurrentDirectory();
        var groups = GroupScanner.Scan(dir);

        if (groups.Count == 0)
        {
            ManualFlow(dir);
            return;
        }

        int done = 0;
        foreach (var g in groups)
        {
            var outName = g.Name + ".unito.pdf";
            using var form = new OptionsForm(AppTitle, BuildCombo(g, outName), outName);
            if (form.ShowDialog() != DialogResult.OK)
                continue; // l'utente ha scelto Salta

            try
            {
                PdfEngine.Run(
                    Path.Combine(dir, g.P1!),
                    g.P2 != null ? Path.Combine(dir, g.P2) : null,
                    g.P3 != null ? Path.Combine(dir, g.P3) : null,
                    Path.Combine(dir, outName),
                    form.Options);
                done++;
                MessageBox.Show("Creato:\n" + outName, AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore unione " + g.Name + ":\n" + ex.Message, AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        if (done == 0)
            MessageBox.Show("Nessun file creato.", AppTitle);
    }

    /// Modo CLI headless: pdfmerge f1 [f2] [f3] [-o out] [-skip-empty] [-dpi N] [-v]
    private static int RunCli(string[] args)
    {
        var files = new List<string>();
        string? outPath = null;
        var opt = new MergeOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-v":
                case "--version":
                    Console.WriteLine("pdfmerge " + Version);
                    return 0;
                case "-o":
                    if (i + 1 < args.Length) outPath = args[++i];
                    break;
                case "-skip-empty":
                    opt.SkipEmpty = true;
                    break;
                case "-compress":
                    opt.Compress = true;
                    break;
                case "-maxpx":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var mp) && mp > 0)
                    {
                        opt.Compress = true;
                        opt.MaxSide = mp;
                    }
                    break;
                case "-quality":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var q) && q >= 1 && q <= 100)
                        opt.JpegQuality = q;
                    break;
                default:
                    files.Add(args[i]);
                    break;
            }
        }

        if (files.Count == 0)
        {
            Console.Error.WriteLine("uso: pdfmerge <f1.pdf> [f2.pdf] [f3.pdf] [-o out.pdf] [-skip-empty] [-compress] [-maxpx N] [-quality N]");
            return 1;
        }

        var f1 = files[0];
        var f2 = files.Count >= 2 ? files[1] : null;
        var f3 = files.Count >= 3 ? files[2] : null;
        outPath ??= "documento_unito.pdf";

        try
        {
            PdfEngine.Run(f1, f2, f3, outPath, opt);
            Console.WriteLine("OK -> " + outPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("errore: " + ex.Message);
            return 1;
        }
    }

    private static void ManualFlow(string dir)
    {
        var r = MessageBox.Show(
            "Nessun gruppo nome.p1.pdf / .p2.pdf / .p3.pdf trovato.\n\n" +
            "Scegliere i file manualmente?\n(seleziona da 1 a 3 PDF; l'ordine = f1, f2, f3)",
            AppTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (r != DialogResult.OK)
            return;

        using var ofd = new OpenFileDialog
        {
            Title = "Seleziona da 1 a 3 PDF (ordine = f1, f2, f3)",
            Filter = "PDF (*.pdf)|*.pdf",
            Multiselect = true,
            InitialDirectory = dir,
        };
        if (ofd.ShowDialog() != DialogResult.OK || ofd.FileNames.Length == 0)
            return;

        var sel = ofd.FileNames.Take(3).ToArray();
        string f1 = sel[0];
        string? f2 = sel.Length >= 2 ? sel[1] : null;
        string? f3 = sel.Length >= 3 ? sel[2] : null;

        using var sfd = new SaveFileDialog
        {
            Title = "Salva PDF unito come...",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = "documento_unito.pdf",
            InitialDirectory = dir,
            OverwritePrompt = true,
        };
        if (sfd.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(sfd.FileName))
            return;

        var combo = new StringBuilder();
        combo.AppendLine("Selezione manuale:\r\n");
        combo.AppendLine($"• pag.1-fine :  {Path.GetFileName(f1)}   (intero)");
        if (f2 != null) combo.AppendLine($"• pag.2-fine :  {Path.GetFileName(f2)}   (senza prima pagina)");
        if (f3 != null) combo.AppendLine($"• accodato   :  {Path.GetFileName(f3)}   (intero)");
        combo.Append($"\r\nOutput:  {sfd.FileName}");

        using var form = new OptionsForm(AppTitle, combo.ToString(), sfd.FileName);
        if (form.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            PdfEngine.Run(f1, f2, f3, sfd.FileName, form.Options);
            MessageBox.Show("Creato:\n" + sfd.FileName, AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Errore:\n" + ex.Message, AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string BuildCombo(PdfGroup g, string outName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Gruppo: {g.Name}\r\n");
        sb.AppendLine($"• pag.1-fine :  {g.P1}   (intero)");
        if (g.P2 != null) sb.AppendLine($"• pag.2-fine :  {g.P2}   (senza prima pagina)");
        if (g.P3 != null) sb.AppendLine($"• accodato   :  {g.P3}   (intero)");
        sb.Append($"\r\nOutput:  {outName}");
        return sb.ToString();
    }
}
