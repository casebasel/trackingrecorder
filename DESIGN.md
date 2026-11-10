# Design: TrackingRecorder

Übernimmt die Gestalt von **syncomat** („Das ruhige Formular", `casebasel/syncomat` DESIGN.md) mit den
Signalregeln aus `stage-system/DESIGN-GEMEINSAM.md`. Werte am 10.11.2026 von der syncomat-Session als sRGB
geliefert (Quelle syncomat `src/App.css` v0.9.39). Umsetzung: `src/TrackingRecorder.App/Theme.cs`, `App.xaml`.

## Grundsätze

- Flach, Haarlinien statt Schatten, schwarze Kopfzeile (48 px): Titel links, zwei Zahlen rechts (Bilder/s · Geräte),
  Störung als Gold-Zusatz.
- Lage in einem Satz (22 px fett) mit Lampe, darunter eine Zeile Einzelheiten.
- **Timecode als Tafel** (Cascadia Mono 44 px): erlaubte Abweichung „Klappe/Leitstand" aus DESIGN-GEMEINSAM.
- Tabellenzeilen 44 px (Geräte) bzw. 34 px (Mars), Trenner `linie-fein`, Gruppenkopf 13 px fett + Zahl in `text2`.
- Hell und Dunkel nach Windows-Einstellung, live umschaltend.
- Schrift: Segoe UI Variable Text / Segoe UI; Mono (Cascadia Mono, Consolas) nur für Zahlen, Serien, Pfade, Timecode;
  Tabellenziffern überall.

## Farben (hell | dunkel)

grund #FFFFFF | #0E0F12 · flaeche #F4F5F7 | #17191D · linie #D9DCE1 | #2A2D33 · linie-fein #ECEEF1 | #1E2126 ·
text #111317 | #ECEEF1 · text2 #5B616B | #9AA1AC · kopf #111317 | #000000 · blau #2867E4 | #568EF9 ·
warn #895200 | #F3B94C (auf Kopf immer #F3B94C) · rot #AC1922 | #FA706A · rot-flaeche #B3232B | #A8282A ·
ok #006E30 | #61CB7C

## Signalregeln

- **Rot nur für die laufende Aufnahme** (Lampe, Stopp-Knopf in `rot-flaeche`). Nie für Störungen.
- **Gold** für jede Störung (fehlende Bilder, Sprung, Kartensprung, Basisstation verschoben, kein Signal). Sperrt nichts.
- **Grün** für verbunden / Pose gültig / Genlock synchron.
- **Grau gefüllt** = letzter Stand (keine frischen Daten).
- Form trägt den Zustand: gefüllter Punkt = in Ordnung bzw. letzter Stand, leerer Ring = Störung, Gold-Strich = Ereignis.
  Das Wort steht immer daneben.
- **Blau** nur für die eine Handlung: „Aufnahme starten".

## Knöpfe und Felder

Haupt 40 px, Radius 6, gefüllt; Neben 30 px, 1 px `linie`, Hover `flaeche`; Eingabe 30 px, `flaeche`, Fokus Rand Blau 2 px.
Fokusring überall 2 px Blau. Gesperrt 45 % Deckkraft.
