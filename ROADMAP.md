# TrackingRecorder — Roadmap

Ziel: stabile Windows-App, die das Kamera-Tracking von VIVE Mars CamTrack mit Timecode aufnimmt
und nach Unreal Engine bringt. Ersatz für HTC „Path Recorder" (`C:\Program Files\VIVE Mars\Path Recorder\`).

## Bekannt (Setup Studio)

- Mars-Box: `192.168.100.60`
- FreeD: UDP-Port `6001`, Rover 1, 2, 3 — wird aktuell an `192.168.100.4` gesendet
- LiveLink: Port `6666` (Protokoll noch unbekannt)

## Phase 1 — Empfang & Aufnahme (läuft)

- [x] FreeD-D1-Parser (29 Bytes, Prüfsumme), Tests
- [x] UDP-Empfang in eigenem Thread mit hoher Priorität
- [x] CSV-Aufnahme, nur anhängend, regelmässig geflusht, inkl. Rohpaket (hex)
- [x] Konsolen-Tool: Live-Anzeige pro Rover mit Rate in Hz
- [ ] Echte Mars-Pakete mitschneiden: Einheiten/Achsen prüfen, Spare-Bytes deuten (Zähler? Timecode?)
- [ ] LiveLink-Strom auf 6666 analysieren — enthält er Timecode?

## Phase 2 — Timecode

- [x] Notlösung: Uhr des Aufnahme-PCs (in jeder Zeile als `tc_source` vermerkt)
- [ ] Timecode von Mars übernehmen, falls im Datenstrom
- [ ] LTC-Decoder über Audio-Eingang des Aufnahme-PCs
- [ ] Drop-Frame (29.97/59.94), Anzeige TC-Status / Aussetzer

## Phase 3 — Windows-App

- [ ] WPF-Oberfläche: Rover-Status, Rec/Stop, Take-Namen, Speicherort
- [ ] Weiterleiten des FreeD-Stroms an Unreal (App sitzt dazwischen, Unreal bekommt weiter Live-Daten)

## Phase 4 — Export nach Unreal

- [ ] CSV/JSON mit Timecode für Sequencer
- [ ] FBX-Kamera
- [ ] Abspielen per LiveLink / FreeD

## Phase 5 — Tracking verbessern

- [ ] Klären, ob Mars ungeglättete Rohdaten liefern kann (Glättung in Mars aus, eigene Filter)
- [ ] Eigene Glättung (One-Euro / Kalman), live und nachträglich
- [ ] Latenzausgleich, Lücken füllen, Tracker→Sensor-Versatz

## Entwicklung

```bash
dotnet test
dotnet run --project src/TrackingRecorder.Cli -- --port 6001 --fps 25 --out take01.csv
```
