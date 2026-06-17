// pdfmerge: unisce fino a 3 PDF.
//   f1 -> tutte le pagine
//   f2 -> senza la prima pagina (pag 2..fine)
//   f3 -> accodato intero
//
// Modi d'uso:
//
//  1. Argomenti espliciti (nessuna finestra):
//       pdfmerge <f1.pdf> [f2.pdf] [f3.pdf] [-o output.pdf]
//
//  2. Interfaccia grafica (nessun argomento):
//       scansiona la directory corrente per gruppi  nome.p1.pdf / nome.p2.pdf / nome.p3.pdf
//       e per OGNI gruppo mostra una finestra con la combinazione chiedendo conferma.
//       Se non trova gruppi apre un selettore file (scegli 1..3 file in ordine).
package main

import (
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strings"

	"github.com/ncruces/zenity"
	"github.com/pdfcpu/pdfcpu/pkg/api"
)

const appTitle = "pdfmerge"

func main() {
	files, outFlag := parseArgs(os.Args[1:])

	if len(files) == 0 {
		guiMode()
		return
	}

	// modo esplicito (CLI)
	f1 := files[0]
	f2, f3 := "", ""
	if len(files) >= 2 {
		f2 = files[1]
	}
	if len(files) >= 3 {
		f3 = files[2]
	}
	out := outFlag
	if out == "" {
		out = "documento_unito.pdf"
	}
	if err := merge(f1, f2, f3, out); err != nil {
		fmt.Fprintln(os.Stderr, "errore:", err)
		os.Exit(1)
	}
	fmt.Println("OK ->", out)
}

func parseArgs(in []string) (files []string, out string) {
	for i := 0; i < len(in); i++ {
		if in[i] == "-o" && i+1 < len(in) {
			out = in[i+1]
			i++
			continue
		}
		files = append(files, in[i])
	}
	return
}

// guiMode: scansione + finestre di conferma.
func guiMode() {
	wd, err := os.Getwd()
	if err != nil {
		zenity.Error("Impossibile leggere la directory corrente:\n"+err.Error(), zenity.Title(appTitle))
		return
	}

	groups := scanGroups(wd)

	if len(groups) == 0 {
		// nessun gruppo -> selettore file manuale
		guiManual(wd)
		return
	}

	// ordina per chiave (lowercase) per output stabile
	var keys []string
	for k := range groups {
		keys = append(keys, k)
	}
	sort.Strings(keys)

	done := 0
	for _, k := range keys {
		g := groups[k]
		if g.files["1"] == "" {
			continue // serve almeno la p1
		}
		out := g.name + ".unito.pdf"

		msg := fmt.Sprintf("Gruppo: %s\n\n", g.name)
		msg += fmt.Sprintf("• pag.1→fine :  %s   (intero)\n", g.files["1"])
		if g.files["2"] != "" {
			msg += fmt.Sprintf("• pag.2→fine :  %s   (senza 1ª pagina)\n", g.files["2"])
		}
		if g.files["3"] != "" {
			msg += fmt.Sprintf("• accodato   :  %s   (intero)\n", g.files["3"])
		}
		msg += fmt.Sprintf("\nOutput:  %s\n\nProcedo con questa combinazione?", out)

		err := zenity.Question(msg,
			zenity.Title(appTitle),
			zenity.OKLabel("Unisci"),
			zenity.CancelLabel("Salta"),
			zenity.QuestionIcon)
		if err == zenity.ErrCanceled {
			continue
		}
		if err != nil {
			return // finestra chiusa / errore -> esci
		}

		if err := merge(g.files["1"], g.files["2"], g.files["3"], filepath.Join(wd, out)); err != nil {
			zenity.Error("Errore unione "+g.name+":\n"+err.Error(), zenity.Title(appTitle))
			continue
		}
		done++
		zenity.Info("Creato:\n"+out, zenity.Title(appTitle), zenity.InfoIcon)
	}

	if done == 0 {
		zenity.Info("Nessun file creato.", zenity.Title(appTitle))
	}
}

// guiManual: selettore file quando non ci sono gruppi.
func guiManual(wd string) {
	r := zenity.Question(
		"Nessun gruppo nome.p1.pdf / .p2.pdf / .p3.pdf trovato.\n\n"+
			"Vuoi scegliere i file manualmente?\n"+
			"(seleziona da 1 a 3 PDF; l'ordine = f1, f2, f3)",
		zenity.Title(appTitle),
		zenity.OKLabel("Scegli file"),
		zenity.CancelLabel("Esci"),
		zenity.QuestionIcon)
	if r != nil {
		return
	}

	sel, err := zenity.SelectFileMultiple(
		zenity.Title("Seleziona da 1 a 3 PDF (ordine = f1, f2, f3)"),
		zenity.FileFilters{{Name: "PDF", Patterns: []string{"*.pdf"}, CaseFold: true}})
	if err != nil || len(sel) == 0 {
		return
	}
	if len(sel) > 3 {
		sel = sel[:3]
	}
	f1 := sel[0]
	f2, f3 := "", ""
	if len(sel) >= 2 {
		f2 = sel[1]
	}
	if len(sel) >= 3 {
		f3 = sel[2]
	}

	out, err := zenity.SelectFileSave(
		zenity.Title("Salva PDF unito come..."),
		zenity.ConfirmOverwrite(),
		zenity.Filename(filepath.Join(wd, "documento_unito.pdf")),
		zenity.FileFilters{{Name: "PDF", Patterns: []string{"*.pdf"}, CaseFold: true}})
	if err != nil || out == "" {
		return
	}

	if err := merge(f1, f2, f3, out); err != nil {
		zenity.Error("Errore:\n"+err.Error(), zenity.Title(appTitle))
		return
	}
	zenity.Info("Creato:\n"+out, zenity.Title(appTitle), zenity.InfoIcon)
}

// group rappresenta un gruppo nome.pN.pdf.
type group struct {
	name  string            // nome visualizzato/output (dal file p1 se presente)
	files map[string]string // "1","2","3" -> nome file
}

// scanGroups trova i gruppi nome.pN.pdf nella directory.
// Il confronto del nome (prefisso) è CASE-INSENSITIVE: Pippo.p1.pdf e
// pippo.p2.pdf finiscono nello stesso gruppo.
func scanGroups(dir string) map[string]*group {
	re := regexp.MustCompile(`(?i)^(.+)\.p([123])\.pdf$`)
	groups := map[string]*group{}
	entries, err := os.ReadDir(dir)
	if err != nil {
		return groups
	}
	for _, e := range entries {
		if e.IsDir() {
			continue
		}
		m := re.FindStringSubmatch(e.Name())
		if m == nil {
			continue
		}
		prefix, part := m[1], m[2]
		key := strings.ToLower(prefix) // confronto case-insensitive
		g := groups[key]
		if g == nil {
			g = &group{name: prefix, files: map[string]string{}}
			groups[key] = g
		}
		g.files[part] = e.Name()
		// usa il nome del file p1 come nome di riferimento per l'output
		if part == "1" {
			g.name = prefix
		}
	}
	return groups
}

// merge: f1 intero, f2 senza prima pagina, f3 accodato intero.
func merge(f1, f2, f3, out string) error {
	if f1 == "" {
		return fmt.Errorf("primo file mancante")
	}
	tmpDir, err := os.MkdirTemp("", "pdfmerge")
	if err != nil {
		return err
	}
	defer os.RemoveAll(tmpDir)

	parts := []string{f1}

	if f2 != "" {
		n, err := api.PageCountFile(f2)
		if err != nil {
			return fmt.Errorf("lettura %s: %w", f2, err)
		}
		if n >= 2 {
			tmp2 := filepath.Join(tmpDir, "f2.pdf")
			if err := api.TrimFile(f2, tmp2, []string{"2-"}, nil); err != nil {
				return fmt.Errorf("trim %s: %w", f2, err)
			}
			parts = append(parts, tmp2)
		}
	}

	if f3 != "" {
		parts = append(parts, f3)
	}

	return api.MergeCreateFile(parts, out, false, nil)
}
