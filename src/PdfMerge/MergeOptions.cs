namespace PdfMerge;

/// Parametri opzionali della pipeline di unione.
public class MergeOptions
{
    public bool SkipEmpty { get; set; }
    public bool LimitDpi { get; set; }
    public int Dpi { get; set; } = 150;
}
