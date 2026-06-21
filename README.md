# pdfmerge v2 (C#)

Utility Windows per unire fino a 3 PDF con logica fissa, pensata per flussi tipo
*mail merge* (documento firmato + allegati). Riscrittura in **C# / WinForms**.

> Branch `v2`. La versione 1.x in Go è sul branch `master`.

## Logica di unione

| File | Cosa viene preso |
|------|------------------|
| **f1** | tutte le pagine (intero) |
| **f2** | dalla pagina 2 alla fine (scarta la prima pagina) |
| **f3** | tutte le pagine, accodate in fondo |

Usando solo **f1 + f3** (senza f2) si ottiene un **merge completo**, senza scartare pagine.

## Opzioni (nel popup)

- **Salta pagine vuote** — rimuove le pagine senza testo/immagini/disegni (euristica sul content stream, via PdfSharp).
- **Comprimi immagini scannerizzate** — riduce le immagini oltre un lato massimo (px) e le ricomprime in JPEG, per ottenere PDF più piccoli (CIE/documenti scannerizzati). Puro .NET (iText7 + System.Drawing), nessuna dipendenza nativa.

## Uso

### Interfaccia grafica (doppio click / nessun argomento)

- Scansiona la cartella corrente per gruppi `nome.p1.pdf`, `nome.p2.pdf`, `nome.p3.pdf`
  (confronto nome **case-insensitive**).
- Per ogni gruppo apre un form con la combinazione e le opzioni; bottoni **Unisci** / **Salta**.
- Output: `nome.unito.pdf`.
- Se non trova gruppi apre un selettore file (1–3 PDF, ordine = f1, f2, f3) + dialog "salva come".

### Riga di comando

```
pdfmerge <f1.pdf> [f2.pdf] [f3.pdf] [-o out.pdf] [-skip-empty] [-compress] [-maxpx N] [-quality N]
pdfmerge -v
```

- `-compress` attiva la compressione immagini (default lato max 1600 px, qualità 80).
- `-maxpx N` lato massimo in pixel. `-quality N` qualità JPEG (1-100).

## Build

Richiede .NET SDK 9.

```
dotnet build -c Release
dotnet run --project src/PdfMerge
```

Eseguibile single-file:

```
dotnet publish src/PdfMerge/PdfMerge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Dipendenze

- [PdfSharp](https://www.pdfsharp.net/) — merge, split, ispezione content stream (pagine vuote).
- [iText7](https://github.com/itext/itext-dotnet) (**AGPL**) — ricompressione immagini per ridurre la dimensione del PDF.
- System.Drawing.Common — ridimensionamento/encoding JPEG (Windows).

> Nota licenza: iText7 è **AGPL**. Il repo è pubblico, coerente con i termini AGPL.

## Release automatiche

Al push di un tag `v2*` (es. `v2.0.0`) la GitHub Action compila l'exe single-file
Windows e lo allega alla Release.

## Convenzione nomi file

```
<nome>.p1.pdf   -> f1 (intero)
<nome>.p2.pdf   -> f2 (senza prima pagina)
<nome>.p3.pdf   -> f3 (accodato)
```
