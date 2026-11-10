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
- [x] Echte Mars-Pakete mitgeschnitten (10.11.2026, `~/daten/trackingrecorder/2026-11-10_mitschnitt/`):
  FreeD 25 Hz pro Rover, Prüfsumme ok, **Spare = 0000 → kein Timecode in FreeD**, Zoom/Fokus = 0 (FIZ fehlt in FreeD)
- [ ] Achsen FreeD ↔ Unreal abgleichen (Rover 3 liegt bei ~0/0/48 mm = Nullpunkt?)
- [x] Live Link passiv: auf dem Multicast kommen nur 50-Byte-Hello/Ping des UE Message Bus (Protokoll 17/18),
  **keine Posen** — die gehen nur an angemeldete Empfänger (Node .4)
- [ ] Message-Bus-Anmeldung nachbauen (Hello → Live-Link-Connect), dann Posen + FIZ + Timecode empfangen

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
- [ ] Aufnahme bleibt immer roh. Glättung nur optional beim Export (One-Euro / Kalman), abschaltbar
- [ ] Latenzausgleich, Lücken füllen, Tracker→Sensor-Versatz

## Phase 6 — Anbindung VP Companion App (zweiter Schritt)

- [ ] Takes und Aufnahmen mit Stage-Server und Klappe synchronisieren (Take-ID, REC/Stop, Timecode)
- [ ] Versatz Tracker→Perspektivpunkt pro Take mitschreiben, aktiv vom Stage-Server (`kamerakal`) holen — siehe `docs/KAMERA-RIG.md`

## Entwicklung

```bash
dotnet test
dotnet run --project src/TrackingRecorder.Cli -- --port 6001 --fps 25 --out take01.csv
```
