# TrackingRecorder — Roadmap

Ziel: stabile Windows-App, die das Kamera-Tracking von VIVE Mars CamTrack mit Timecode aufnimmt
und nach Unreal Engine bringt. Ersatz für HTC „Path Recorder" (`C:\Program Files\VIVE Mars\Path Recorder\`).

**Endziel (Marlon, 10.11.2026):** Greenscreen-Dreh mit echter Kamerabewegung, Hintergrund **nachträglich**
in Unreal rendern. Dafür braucht es ein sehr sauberes Tracking: bildgenau zum Clip (Timecode), richtige
Perspektive (Eintrittspupille, nicht Tracker), passende Objektivdaten.

Was daraus folgt:
- Offline statt live: Nachbearbeitung darf vor- und zurückschauen (glätten ohne Verzögerung, Lücken
  sauber füllen). Die Rohaufnahme bleibt trotzdem immer unverändert erhalten.
- Bildgenaue Zuordnung zum Kamera-Clip über Timecode, Versatz Tracker↔Bild messbar machen.
- Objektiv: Brennweite, Fokus, Verzeichnung — sonst rutscht der Hintergrund trotz gutem Tracking.
- Rig Greenscreen: ARRI Amira + 35 mm Festbrennweite, **kein Zoom** → ein Verzeichnungsprofil, nur Fokus variiert.
- Prüfen gegen das Bild: Tracking-Marker auf dem Greenscreen, Restfehler in Pixeln messen.

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
- [x] FreeD-Takt auf PC .3 (25 min Mitschnitt): exakt 25,00 Hz pro Rover, Abstand 40 ± 2 ms,
  nur ~6 fehlende Bilder (Lücken max. 136 ms) → Empfang ist zuverlässig; Lücken trotzdem erkennen und melden
- [ ] Achsen FreeD ↔ Unreal abgleichen (Rover 3 liegt bei ~0/0/48 mm = Nullpunkt?)
- [x] Live Link passiv: auf dem Multicast kommen nur 50-Byte-Hello/Ping des UE Message Bus (Protokoll 17/18),
  **keine Posen** — die gehen nur an angemeldete Empfänger (Node .4)
- [x] **Mars-Datenstrom UDP 5500 entschlüsselt** (Weg von Path Recorder, Mitschnitt 10.11.2026): Anfrage `07`/`06`,
  pro Bild Timecode, Geräte-Zeit, alle Tracker **und Basisstationen** (Position m, Quaternion). Aufbau in
  `src/TrackingRecorder.Core/MarsStream.cs`. Mars antwortet nur ins Studio-LAN (nicht an VM 170).
- [x] Modus `mars` in der exe: Aufnahme CSV + Rohdatei, Live-Prüfung fehlende Bilder / Sprünge / Kartensprünge
- [ ] Zuordnung Seriennummer ↔ Rover-Port (LHR-0FFCBEE6 = Rover 1 Kamera, LHR-16D49F61 = Rover 3)
- [ ] Message-Bus-Anmeldung nachbauen (nur noch nötig für FIZ, falls nicht über FreeD) (Hello → Live-Link-Connect), dann Posen + FIZ + Timecode empfangen

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
- [ ] **Kartensprünge herausrechnen:** Die Basisstationen hängen fest an Stahlträgern (Marlon 10.11.2026).
  Jede „Bewegung" einer BS in den Daten ist ein Schätzfehler von Mars und verschiebt die ganze Welt.
  Aus alter und neuer Lage der 4 BS die starre Verschiebung schätzen und auf die Rover-Posen umgekehrt anwenden.
- [ ] Prüfen, ob sich in Mars die Raumkarte (Basisstationen) festsetzen lässt, statt laufend neu zu schätzen

## Phase 5b — Verfeinern am Bild (Hybrid Mars + Bild)

Idee Marlon 10.11.2026: Marker auf der Leinwand / nDisplay, Track in Nuke, Systeme verbinden.
- Mars-Bahn = Grundform, Bild = genaue Lage. Nicht frei neu lösen, sondern wenige Korrekturen schätzen:
  Zeitversatz, Versatz Tracker→Pupille, langsame Drift pro Take.
- Marker mit bekannter 3D-Lage: über nDisplay auf die Leinwand projiziert (ChArUco-Muster gibt es schon:
  vp-companion-app `werkzeuge/leinwand-muster.py`, Auswertung `werkzeuge/leinwand-messen/`) plus Klebemarker am Greenscreen.
- Nuke: Kamera-Export (FBX/.chan) zum Prüfen und als Startwert für CameraTracker; 2D-Tracks aus Nuke zurück zum Einpassen.
- Mass für „sauber“: Restfehler der Marker in Pixeln pro Bild.

## Phase 6 — Anbindung VP Companion App (zweiter Schritt)

- [ ] Takes und Aufnahmen mit Stage-Server und Klappe synchronisieren (Take-ID, REC/Stop, Timecode)
- [ ] Versatz Tracker→Perspektivpunkt pro Take mitschreiben, aktiv vom Stage-Server (`kamerakal`) holen — siehe `docs/KAMERA-RIG.md`

## Entwicklung

```bash
dotnet test
dotnet run --project src/TrackingRecorder.Cli -- --port 6001 --fps 25 --out take01.csv
```
