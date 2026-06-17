# pdfmerge

Piccola utility in Go per unire fino a 3 PDF con una logica fissa, pensata per flussi di tipo *mail merge* (documento firmato + allegati).

## Logica di unione

| File | Cosa viene preso |
|------|------------------|
| **f1** | tutte le pagine (intero) |
| **f2** | dalla pagina 2 alla fine (scarta la prima pagina) |
| **f3** | tutte le pagine, accodate in fondo |

`f2` e `f3` sono opzionali. Se `f2` ha una sola pagina viene ignorato (dopo aver tolto la prima non resta nulla).

Se usi solo **f1 + f3** (senza f2) il risultato è un **merge completo**: f1 intero seguito da f3 intero, senza scartare alcuna pagina.

## Modi d'uso

### 1. Interfaccia grafica (nessun argomento)

Doppio click sull'eseguibile (oppure lancio senza argomenti):

```
pdfmerge
```

- Scansiona la cartella corrente cercando gruppi di file nominati `nome.p1.pdf`, `nome.p2.pdf`, `nome.p3.pdf`. Il confronto del nome è **case-insensitive**: `Pippo.p1.pdf` e `pippo.p2.pdf` finiscono nello stesso gruppo.
- Per **ogni** gruppo trovato apre una finestra con la combinazione proposta e chiede conferma (**Unisci** / **Salta**).
- Output: `nome.unito.pdf`.
- Se non trova nessun gruppo apre un selettore file: scegli da 1 a 3 PDF (l'ordine di selezione = f1, f2, f3) e poi la destinazione.

Esempio: con in cartella `documento.p1.pdf` e `documento.p2.pdf` propone l'unione e crea `documento.unito.pdf`.

### 2. Riga di comando

```
pdfmerge <f1.pdf> [f2.pdf] [f3.pdf] [-o output.pdf]
```

- `-o` imposta il file di output (default `documento_unito.pdf`).

Esempio:

```
pdfmerge documento.p1.pdf documento.p2.pdf -o unito.pdf
```

## Build

Richiede Go 1.21+.

```
go build -ldflags="-H windowsgui" -o pdfmerge.exe .
```

Il flag `-H windowsgui` evita l'apertura della finestra console quando si avvia la GUI con doppio click. Per una build con output su console (utile in modalità CLI) ometterlo:

```
go build -o pdfmerge.exe .
```

## Dipendenze

- [pdfcpu](https://github.com/pdfcpu/pdfcpu) — manipolazione PDF (trim, merge).
- [ncruces/zenity](https://github.com/ncruces/zenity) — dialoghi nativi (no CGO).

## Convenzione nomi file

Per la selezione automatica usa il pattern:

```
<nome>.p1.pdf   -> f1 (intero)
<nome>.p2.pdf   -> f2 (senza prima pagina)
<nome>.p3.pdf   -> f3 (accodato)
```
