using System.Windows.Forms;

namespace PdfMerge;

/// Form di conferma con i parametri (salto pagine vuote, compressione immagini).
public class OptionsForm : Form
{
    private readonly CheckBox _chkEmpty;
    private readonly CheckBox _chkCompress;
    private readonly NumericUpDown _nudMaxPx;

    public MergeOptions Options { get; } = new();

    public OptionsForm(string title, string combo, string outName)
    {
        bool compAvail = PdfEngine.CompressionAvailable();

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

        _chkCompress = new CheckBox
        {
            Text = "Comprimi immagini scannerizzate",
            Location = new Point(14, 54),
            AutoSize = true,
            Enabled = compAvail,
        };

        _nudMaxPx = new NumericUpDown
        {
            Location = new Point(250, 52),
            Width = 80,
            Minimum = 200,
            Maximum = 6000,
            Value = 1600,
            Increment = 100,
            Enabled = false,
        };
        var lblPx = new Label { Text = "px lato max", Location = new Point(336, 54), AutoSize = true };

        _chkCompress.CheckedChanged += (_, _) => _nudMaxPx.Enabled = _chkCompress.Checked;

        var lblNote = new Label
        {
            Text = compAvail
                ? "Riduce le immagini grandi e le ricomprime in JPEG (PDF più piccolo)."
                : "Compressione disponibile solo su Windows.",
            Location = new Point(14, 82),
            Size = new Size(460, 20),
            ForeColor = compAvail ? SystemColors.GrayText : Color.Firebrick,
        };

        grp.Controls.AddRange(new Control[] { _chkEmpty, _chkCompress, _nudMaxPx, lblPx, lblNote });

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
            if (compAvail && _chkCompress.Checked)
            {
                Options.Compress = true;
                Options.MaxSide = (int)_nudMaxPx.Value;
            }
        };
    }
}
