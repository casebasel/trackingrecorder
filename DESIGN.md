# Design: TrackingRecorder

**Massgebend ist das Design System „Filmstudio Stage“**: https://claude.ai/artifact/DLVnnCHU46neKXpxK6adYK —
vor jeder UI-Arbeit `project/README.md` und `project/tokens.json` lesen (Artifact-Tool, action read mit path).
TrackingRecorder gehört seit 15.11.2026 (Entscheid Marlon, über stage-system) zur Familie **Set-Apps**:
dieselbe Gestalt wie **Stage Ingest** (führende App der Aufnahme), Akzent nachts Bernstein, tags Blau.
Neue gemeinsame Regeln nur über stage-system; Widersprüche an stage-system melden, nie selbst im Artefakt ändern.
Diese Datei hält die Abweichungen und den Stand der Umsetzung in WPF (`Theme.cs`, `App.xaml`, `Zeichen.cs`, `MainWindow.xaml`).

## Gestalt (wie Stage Ingest)

- **Kopf 44 px** in `panel-2` mit Linie unten: Logo, Name 14 px fett, Version Mono 11 px `text-3`;
  rechts Bilder/s · Geräte, Störung als Gold-Zeichen mit Wort.
- **Blöcke** (`panel`, 1 px `linie`, Radius 6) mit **Block-Kopf** (min. 40 px, `panel-2`, Titel 13 px fett, Zahl `text-3`):
  Lage, Geräte, Mars, Ereignisse. Zeilen mit 1 px `linie`, 46 px (Geräte) bzw. 32 px (Mars).
- Lage in einem Satz (20 px fett) mit Zeichen, darunter eine Zeile Einzelheiten.
- **Timecode als Tafel** (Cascadia Mono 44 px) — erlaubte Abweichung.
- Unten die Aufnahme (`panel`), darunter die Statusleiste (`panel-2`, 30 px).
- Tag und Nacht nach Windows-Einstellung, live umschaltend. Schrift Segoe UI Variable Text / Segoe UI,
  Mono (Cascadia Mono) nur für Zahlen, Serien, Pfade, Timecode; Tabellenziffern überall.

## Farben

Set-App-Tokens aus `tokens.json` (OKLCH → sRGB) in `Theme.cs`: fenster, panel, panel-2, feld, hover, linie, linie-stark,
text, text-2, text-3, text-aus, akzent, akzent-hover, akzent-text, auf-akzent, auswahl, ok, warn, rot, rot-flaeche,
rot-flaeche-gedrueckt, auf-rot. Das Blau der Datenzentrale samt `auf-blau` ist entfallen.

## Zeichen (Lampen) — Form trägt den Zustand, das Wort steht immer daneben

| Art | Form | Farbe | Wofür |
|---|---|---|---|
| ok | Kreis mit Haken | `ok` (Grün) | bereit, verbunden, Pose gültig, Genlock, Filter aus, Station steht fest |
| warn | Dreieck mit Ausrufezeichen | `warn` (Gold) | jede Störung; sperrt nichts |
| unbekannt | gestrichelter Ring | `linie-stark`, Wort `text-3`, **kein Gold** | Mars-Filter unbekannt, Web antwortet nicht (letzter Stand), keine frischen Daten |
| rec | gefüllter Punkt | `rot` | nur laufende Aufnahme |

## Knöpfe und Felder

- Knopf 28 px, Padding 0 12, Rand `linie-stark`, Radius 4, `panel-2`, Gewicht 500, Kontaktschatten (`schatten-taste`,
  auf eigener Fläche hinter dem Inhalt, damit ClearType bleibt). Hover `hover`. Gesperrt 45 %.
- Hauptknopf gross 38 px, Padding 0 20, 14 px 600, Radius 6: **„Aufnahme starten“ in `akzent` / `auf-akzent`** (ein Tipp).
- **Während der Aufnahme: `rot-flaeche` / `auf-rot`, „Aufnahme stoppen · halten“. Stoppen nur durch 0,6 s Halten**
  (Maus, Leertaste oder Enter); die Füllung läuft von links in `rot-flaeche-gedrueckt`. Kurzes Tippen stoppt nicht und
  zeigt „Zum Stoppen 0,6 s gedrückt halten“. Klappe (Cut) stoppt wie bisher ohne Halten.
- Eingabe 28 px, `feld`, 1 px `linie`, Radius 4, Fokus Rand `akzent`. Kontrollkästchen 16 px, an = `akzent` mit Haken `auf-akzent`.
- Fokusring überall 2 px `akzent`.

## Logo

`assets/icon.svg` (im Fenster als `DrawingImage` „Logo“ in `App.xaml`): Graphit-Kachel #0f1114, Bildrahmen-Ecken
in Hellgrau #c9ccd1, Kamerabahn mit Startpunkt in Bernstein #e08a3c — Stil Stage Ingest / Plate Assistant.
