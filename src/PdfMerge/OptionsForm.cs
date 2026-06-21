using System.Windows.Forms;

namespace PdfMerge;

/// Form di conferma con i parametri (salto pagine vuote, limita DPI).
public class OptionsForm : Form
{
    private readonly CheckBox _chkEmpty;
    private readonly CheckBox _chkDpi;
    private readonly NumericUpDown _nudDpi;

    public MergeOptions Options { get; } = new();

    public OptionsForm(string title, string combo, string outName)
    {
        bool gsAvail = PdfEngine.GhostscriptAvailable();

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 320);

        var lblCombo = new Label
        {
            Text = combo,
            AutoSize = false,
            Location = new Point(16, 12),
            Size = new Size(488, 120),
        };

        var grp = new GroupBox
        {
            Text = "Opzioni",
            Location = new Point(16, 138),
            Size = new Size(488, 110),
        };

        _chkEmpty = new CheckBox
        {
            Text = "Salta pagine vuote",
            Location = new Point(14, 26),
            AutoSize = true,
        };

        _chkDpi = new CheckBox
        {
            Text = "Limita risoluzione immagini",
            Location = new Point(14, 54),
            AutoSize = true,
            Enabled = gsAvail,
        };

        _nudDpi = new NumericUpDown
        {
            Location = new Point(230, 52),
            Width = 80,
            Minimum = 30,
            Maximum = 1200,
            Value = 150,
            Increment = 10,
            Enabled = false,
        };
        var lblDpi = new Label { Text = "DPI", Location = new Point(316, 54), AutoSize = true };

        _chkDpi.CheckedChanged += (_, _) => _nudDpi.Enabled = _chkDpi.Checked;

        var lblNote = new Label
        {
            Text = gsAvail
                ? "Il DPI ricampiona le immagini (richiede Ghostscript)."
                : "Ghostscript non trovato: opzione DPI non disponibile.",
            Location = new Point(14, 82),
            Size = new Size(460, 20),
            ForeColor = gsAvail ? SystemColors.GrayText : Color.Firebrick,
        };

        grp.Controls.AddRange(new Control[] { _chkEmpty, _chkDpi, _nudDpi, lblDpi, lblNote });

        var btnOk = new Button
        {
            Text = "Unisci",
            DialogResult = DialogResult.OK,
            Location = new Point(320, 268),
            Size = new Size(90, 30),
        };
        var btnSkip = new Button
        {
            Text = "Salta",
            DialogResult = DialogResult.Cancel,
            Location = new Point(414, 268),
            Size = new Size(90, 30),
        };

        AcceptButton = btnOk;
        CancelButton = btnSkip;

        Controls.AddRange(new Control[] { lblCombo, grp, btnOk, btnSkip });

        FormClosing += (_, _) =>
        {
            Options.SkipEmpty = _chkEmpty.Checked;
            if (gsAvail && _chkDpi.Checked)
            {
                Options.LimitDpi = true;
                Options.Dpi = (int)_nudDpi.Value;
            }
        };
    }
}
