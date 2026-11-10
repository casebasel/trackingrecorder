# TrackingRecorder — Roadmap

Ziel: stabile Windows-App, die das Kamera-Tracking von VIVE Mars CamTrack mit Timecode aufnimmt
und nach Unreal Engine bringt. Ersatz für HTC „Path Recorder" (`C:\Program Files\VIVE Mars\Path Recorder\`).

## Bekannt (Setup Studio)

- Mars-Box: `192.168.100.60`, Web-API „Remote Control" auf `http://192.168.100.60:7008`
  (`/api/GetTimeCode`, `/api/GetGenlockState`, `/api/GetRecenterTarget` …, siehe vp-companion-app `docs/NETZWERK.md`)
- FreeD: UDP `6001`, Rover 1, 2, 3 — Ziel aktuell `192.168.100.4` (nDisplay-Node „Ada")
- Live Link: Unreal Message Bus, **Multicast `230.0.0.1:6666`** — trägt Pose, FIZ und Timecode
- Timecode-Kette: Tentacle Sync → Mars TC-In → Live Link → Unreal. Stand 10.11.2026 meldet
  `/api/GetTimeCode` `Enable:false` — am Mars kommt gerade kein Timecode an.
- Bildrate Stage: **25p** (seit 13.10.2026)
- Rover 1 = Kamera (mit Objektiv-Encoder), Rover 3 = Kalibrations-Tracker (Befund 29.09.2026, unbestätigt)
- Multicast erreicht den Server-Claude (VM 170, anderes Subnetz) nicht — Live-Link-Tests nur im Studio-LAN

## Phase 1 — Empfang & Aufnahme (läuft)

- [x] FreeD-D1-Parser (29 Bytes, Prüfsumme), Tests
- [x] UDP-Empfang in eigenem Thread mit hoher Priorität
- [x] CSV-Aufnahme, nur anhängend, regelmässig geflusht, inkl. Rohpaket (hex)
- [x] Konsolen-Tool: Live-Anzeige pro Rover mit Rate in Hz
- [ ] Echte Mars-Pakete mitschneiden: Einheiten/Achsen prüfen, Spare-Bytes deuten (Zähler? Timecode?)
- [ ] LiveLink-Strom auf 6666 analysieren — enthält er Timecode?

## Phase 2 — Timecode

- [x] Notlösung: Uhr des Aufnahme-PCs (in jeder Zeile als `tc_source` vermerkt)
- [ ] Timecode aus Live Link (Message Bus, Multicast) übernehmen — Hauptweg, gleiche Quelle wie Unreal
- [ ] Timecode-Status vom Mars per `/api/GetTimeCode` überwachen
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

## Phase 6 — Anbindung VP Companion App (zweiter Schritt)

- [ ] Takes und Aufnahmen mit Stage-Server und Klappe synchronisieren (Take-ID, REC/Stop, Timecode)
- [ ] Tracker→Sensor-Versatz des Kamera-Rigs aus dem Unreal-Rig der VP Companion App übernehmen

## Entwicklung

```bash
dotnet test
dotnet run --project src/TrackingRecorder.Cli -- --port 6001 --fps 25 --out take01.csv
```
