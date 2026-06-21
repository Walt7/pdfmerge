namespace PdfMerge;

/// Parametri opzionali della pipeline di unione.
public class MergeOptions
{
    public bool SkipEmpty { get; set; }

    /// Comprimi le immagini scannerizzate (riduce dimensione file).
    public bool Compress { get; set; }

    /// Lato massimo in pixel: le immagini più grandi vengono ridotte.
    public int MaxSide { get; set; } = 1600;

    /// Qualità JPEG (1-100) usata in ricompressione.
    public int JpegQuality { get; set; } = 80;
}
