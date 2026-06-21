package main

import (
	"os"
	"path/filepath"
	"testing"
)

func TestScanGroupsCaseInsensitive(t *testing.T) {
	dir := t.TempDir()
	for _, n := range []string{"Pippo.p1.pdf", "pippo.p2.pdf", "PIPPO.p3.pdf", "Altro.p1.pdf"} {
		if err := os.WriteFile(filepath.Join(dir, n), []byte("x"), 0o644); err != nil {
			t.Fatal(err)
		}
	}
	g := scanGroups(dir)
	if len(g) != 2 {
		t.Fatalf("attesi 2 gruppi, ottenuti %d: %+v", len(g), g)
	}
	pippo, ok := g["pippo"]
	if !ok {
		t.Fatalf("gruppo 'pippo' mancante: %+v", g)
	}
	if pippo.files["1"] == "" || pippo.files["2"] == "" || pippo.files["3"] == "" {
		t.Fatalf("p1/p2/p3 non tutti rilevati: %+v", pippo.files)
	}
	if pippo.name != "Pippo" {
		t.Fatalf("nome da p1 atteso 'Pippo', ottenuto %q", pippo.name)
	}
}
