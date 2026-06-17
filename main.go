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

	var prefixes []string
	for p := range groups {
		prefixes = append(prefixes, p)
	}
	sort.Strings(prefixes)

	done := 0
	for _, prefix := range prefixes {
		g := groups[prefix]
		if g["1"] == "" {
			continue // serve almeno la p1
		}
		out := prefix + ".unito.pdf"

		msg := fmt.Sprintf("Gruppo: %s\n\n", prefix)
		msg += fmt.Sprintf("• pag.1→fine :  %s   (intero)\n", g["1"])
		if g["2"] != "" {
			msg += fmt.Sprintf("• pag.2→fine :  %s   (senza 1ª pagina)\n", g["2"])
		}
		if g["3"] != "" {
			msg += fmt.Sprintf("• accodato   :  %s   (intero)\n", g["3"])
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

		if err := merge(g["1"], g["2"], g["3"], filepath.Join(wd, out)); err != nil {
			zenity.Error("Errore unione "+prefix+":\n"+err.Error(), zenity.Title(appTitle))
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

// scanGroups trova i gruppi nome.pN.pdf nella directory.
func scanGroups(dir string) map[string]map[string]string {
	re := regexp.MustCompile(`(?i)^(.+)\.p([123])\.pdf$`)
	groups := map[string]map[string]string{}
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
		if groups[prefix] == nil {
			groups[prefix] = map[string]string{}
		}
		groups[prefix][part] = e.Name()
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
