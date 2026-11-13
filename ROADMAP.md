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
- FreeD: UDP `6001`, Rover 1, 2, 3 — Ziel seit 10.11.2026 `192.168.100.3` (Aufnahme-PC i9)
- **Mars-Strom UDP 5500** (Weg von Path Recorder) ist die Quelle der App; Filter am Mars aus (seit 10.11.)
- Live Link: Unreal Message Bus, **Multicast `230.0.0.1:6666`** — trägt Pose, FIZ und Timecode
- Timecode-Kette: Tentacle Sync → Mars TC-In → Live Link → Unreal. Nach jedem Mars-Neustart prüfen
  (`/api/GetTimeCode` → `Enable:true`), am 11.11. fehlte er nach dem Neustart.
- Bildrate Stage: **25p** (seit 13.10.2026)
- **Rover 1 = Kamera** (LHR-0FFCBEE6, mit Objektiv-Encoder; Marlon 12.11.2026), Rover 3 = Kalibrier-Tracker (LHR-16D49F61)
- **Mars → Unreal (Live Link):** reine Achsenumrechnung `p_ue = 100·(z, −x, y)`, `R_ue = A·R·Aᵀ` (gemessen 12.11. an T02–T04,
  Rest 0,3 cm / 0,1°). Kamera in Unreal = Versatz × Tracker × Root (Stage-Plugin), geprüft gegen die Unreal-Aufnahme
  STUDIO-01_02/_03: 0,3 cm / 0,08° → `werkzeuge/unreal_kamera.py`
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
- [x] Achsen Mars ↔ Unreal abgeglichen (12.11.2026, siehe oben)
- [x] Live Link passiv: auf dem Multicast kommen nur 50-Byte-Hello/Ping des UE Message Bus (Protokoll 17/18),
  **keine Posen** — die gehen nur an angemeldete Empfänger (Node .4)
- [x] **Mars-Datenstrom UDP 5500 entschlüsselt** (Weg von Path Recorder, Mitschnitt 10.11.2026): Anfrage `07`/`06`,
  pro Bild Timecode, Geräte-Zeit, alle Tracker **und Basisstationen** (Position m, Quaternion). Aufbau in
  `src/TrackingRecorder.Core/MarsStream.cs`. Mars antwortet nur ins Studio-LAN (nicht an VM 170).
  **06 = anmelden, 07 = abmelden** (aus dem Code von Path Recorder `UDPListener`). Mars sendet an jede
  Anmeldung weiter, bis 07 kommt. Am Gerät bestätigt: 25,00 Bilder/s, 0 fehlend, sauberes Abmelden.
- [x] Modus `mars` in der exe: Aufnahme CSV + Rohdatei, Live-Prüfung fehlende Bilder / Sprünge / Kartensprünge
- [x] Zuordnung Seriennummer ↔ Rover-Port über :7008 (`GetAllRoverStates`), in der App angezeigt
- [ ] Message-Bus-Anmeldung nachbauen (nur noch nötig für FIZ, falls nicht über FreeD) (Hello → Live-Link-Connect), dann Posen + FIZ + Timecode empfangen

## Phase 2 — Timecode

- [x] Notlösung: Uhr des Aufnahme-PCs (in jeder Zeile als `tc_source` vermerkt)
- [ ] Timecode aus Live Link (Message Bus, Multicast) übernehmen — Hauptweg, gleiche Quelle wie Unreal
- [x] Timecode-Status vom Mars per `/api/GetTimeCode` in der App (Zeile „Timecode-Eingang")
- [x] Timecode in jedem Bild aus dem 5500-Strom (Mars-TC = Tentacle) — Hauptweg, Live Link nicht nötig
- [ ] LTC-Decoder über Audio-Eingang des Aufnahme-PCs
- [ ] Drop-Frame (29.97/59.94), Anzeige TC-Status / Aussetzer

## Phase 3 — Windows-App

- [x] WPF-Oberfläche `TrackingRecorderApp.exe` (v0.3.0, Design von syncomat, `DESIGN.md`): Lage, Timecode-Tafel,
  Geräte mit Drift, Mars-Zustand (Timecode-Eingang, Genlock, Basisstationen, Rover) aus Web-Schnittstelle :7008,
  Ereignisse, Take/Speicherort/Aufnahme; Take-Zusammenfassung als JSON. Prüfbild: `--bild <png> [--hell|--dunkel] [--rec]`
- [x] Rover 1 meldet seit 11.11. wieder Zustand 5 (mit Objektiv-Encoder)
- [ ] Mars-Web-Schnittstelle antwortet zeitweise erst nach 4–6 s → Zeitlimit von 4 s auf ~15 s (App zeigt sonst „antwortet nicht")
- [ ] `StartTimecode` in der Take-JSON zählt den Vorlauf nicht mit → erstes Bild der Datei als Start, Vorlauf getrennt angeben
- [x] **Installation und Updates** (v0.5.0, Velopack): Installer `TrackingRecorder-win-Setup.exe` aus den GitHub-Releases,
  pro Benutzer unter %LOCALAPPDATA%\TrackingRecorder, Startmenü + Desktop. Sucht beim Start und alle 30 min, lädt im
  Hintergrund, spielt beim Schliessen ein; „Neu starten" in der Statusleiste (gesperrt während der Aufnahme).
  Veröffentlichen: Version im App-csproj erhöhen, dann `werkzeuge/release.sh`. Repo seit 10.11.2026 öffentlich.
  Update-Weg 0.5.0 → 0.5.1 auf PC .3 erfolgreich getestet (10.11.2026).
- [ ] Weiterleiten des FreeD-Stroms an Unreal (App sitzt dazwischen, Unreal bekommt weiter Live-Daten)

## Phase 4 — Export nach Unreal und Nuke (Marlon 12.11.2026: aus jedem Mars-Take ein FBX und eine Nuke-Kamera)

- [x] Werkzeuge (Python, `werkzeuge/`): `export.py` (.chan), `unreal_kamera.py` (Unreal-Welt je Clipbild),
  `kamera_fuer_unreal.py` (Austauschformat für Sequencer-Import), `vergleich.py` / `drehvergleich.py`
- [ ] **In der App: Knopf „Exportieren" je Take** → FBX für Unreal (Kamera in Weltkoordinaten, Timecode je Key wie
  Path Recorder: TCHour…TCFrame) **und** Nuke-Kamera (.chan, Bild 1 = Clip-Start), beide auf den Clip zugeschnitten
- [ ] Nuke-Kamera im Unreal-Nullpunkt, damit Nuke-3D und Unreal-Renders zusammenpassen. Gegenprobe der Umrechnung mit
  Peter Mercell „Unreal-Camera-to-Nuke-converter" (MIT, nur Camera3) und dessen Beispieldateien
- [ ] Export legt in `06_VFX_RENDER/<Datum>_<kurz>/` im Projekt-Abo ab (Marlon 12.11.); Rohdaten bleiben in `08_TRACKING`
- [ ] Zeitversatz Mars ↔ Bild als Kalibrierwert (gemessen −2 Bilder / −80 ms) im Export anwenden
- [ ] Versatz Tracker→Pupille und Root je Take vom Stage-Server übernehmen statt fest
- [ ] Konvention für SCHNITTSTELLEN (stage-system) liefern: Achsen/Einheiten, Nullpunkt, Versatz, Start-TC, fps, Lens File
- [ ] Abspielen per LiveLink / FreeD (später)

## Phase 5 — Tracking verbessern

- [x] Rohdaten nur mit Mars-Filter aus (gemessen 10.11.); App erkennt den Filter aus den Daten (Feld `Filter` je Take)
- [ ] Aufnahme bleibt immer roh. Glättung nur optional beim Export (One-Euro / Kalman), abschaltbar
- [ ] Latenzausgleich, Lücken füllen, Tracker→Sensor-Versatz
- [ ] **Kartensprünge herausrechnen:** Die Basisstationen hängen fest an Stahlträgern (Marlon 10.11.2026).
  Jede „Bewegung" einer BS in den Daten ist ein Schätzfehler von Mars und verschiebt die ganze Welt.
  Aus alter und neuer Lage der 4 BS die starre Verschiebung schätzen und auf die Rover-Posen umgekehrt anwenden.
- [ ] Prüfen, ob sich in Mars die Raumkarte (Basisstationen) festsetzen lässt, statt laufend neu zu schätzen

## Genauigkeitstest Greenscreen (Studio 12.11.2026, Projekt INTERN_RESTAURANT_UND_AI_UNREAL)

Takes STUDIO-01_T01–T04 (TrackingRecorder vollständig, 0 fehlende Bilder, Filter aus). Clips A006C001–C003.
Prüfliste fürs Handy: https://claude.ai/artifact/CAN9vpzppyVauZ44AVEy1V · Daten: `~/daten/trackingrecorder/2026-11-12_studiotest/`,
Projekt `06_VFX_RENDER/2026-11-12_tracking-test/`.
- [x] Unreal-Aufnahme: T04 abgebrochen (Editor-Hänger, TC-Sprung), T02/T03 nur Editor-Abtastung; Posen-Sicherung der Stage
  zeitlich unzuverlässig (σ 7 Bilder, bis ±0,65 s) — Fehler im Aufbau der Sicherung, Stage behebt in Plugin 0.22.14
- [x] Mars ↔ Nuke-Solve (T04, A006C003): Zeitversatz −80 ms fest, Zittern 0,09° (≈ 5 px in HD), **systematisch: Mars-Drehwinkel
  2,5–3,5 % grösser als Nuke** (≈ 0,75° RMS) — Ursache offen (Solve-Brennweite/Filmback/Verzeichnung oder Mars)
- [x] **Genesis-Solve** (Unreal-Tracker, COLMAP/GLOMAP, Level GenesisTracking, 13.11.): Drehwinkel **+3,1 %** gegenüber Mars,
  Zittern 0,32° (≈ 16 px), Reprojektion 8,9 px, Brennweite gelöst 38,4 mm (Kamera-Actor steht auf 35 mm), ohne Verzeichnung.
  → Mars liegt **zwischen** Nuke (−2,5 %) und Genesis (+3,1 %); der Winkelunterschied kommt sehr wahrscheinlich aus den Solves.
  Zeitversatz Mars ↔ Bild in beiden Solves −2 … −2,5 Bilder (80–100 ms).
- [ ] **90°-Test:** Schwenk um genau bekannten Winkel (Gradskala/Marken) → endgültiger Beleg für den Winkelmassstab von Mars
- [ ] **Schachbrett-Take** (eigener Studio-Take): Brennweite und Verzeichnung des 35 mm messen, Nuke neu lösen
- [ ] **Render-Vergleich** (Session Gaussian_to_Beeble, Kameras in `kameras/`): T04 mars + nuke, T03 unreal + mars, je Gitter
  und Set → in Nuke über die Clips legen, Rutschen sichtbar machen
- [ ] Achsen-Take (A) und die Takes 1–6 der Prüfliste nachholen
- [ ] Ingest legte den Drehordner nach `2026-11-12_OHNE_DREHORT`, Tracking liegt in `2026-11-12_STUDIO` → in Ingest einsortieren
- [ ] Root in Gaussian_to_Beeble auf Z 31,56: virtueller Boden ~30 cm unter dem echten — Entscheid Marlon (nach dem Test)

## Phase 5b — Verfeinern am Bild (Hybrid Mars + Bild)

Idee Marlon 10.11.2026: Marker auf der Leinwand / nDisplay, Track in Nuke, Systeme verbinden.
- Mars-Bahn = Grundform, Bild = genaue Lage. Nicht frei neu lösen, sondern wenige Korrekturen schätzen:
  Zeitversatz, Versatz Tracker→Pupille, langsame Drift pro Take.
- Marker mit bekannter 3D-Lage: über nDisplay auf die Leinwand projiziert (ChArUco-Muster gibt es schon:
  vp-companion-app `werkzeuge/leinwand-muster.py`, Auswertung `werkzeuge/leinwand-messen/`) plus Klebemarker am Greenscreen.
- Nuke: Kamera-Export (FBX/.chan) zum Prüfen und als Startwert für CameraTracker; 2D-Tracks aus Nuke zurück zum Einpassen.
- Mass für „sauber“: Restfehler der Marker in Pixeln pro Bild.

## Phase 6 — Anbindung VP Companion App (zweiter Schritt)

- [x] **Mithören bei der Stage** (v0.4.0): ws://192.168.100.20:4400/client?quelle=trackingrecorder&schmal=1, Ereignis
  `sitzung` (takeLaeuft / laufenderTake). „Mit der Klappe aufnehmen": REC/Cut der Klappe startet/stoppt die Aufnahme,
  Take-Name = Projekt_Einstellung_Tnn, Stage-Take-ID in der JSON, 2 s Vorlauf, 1 s Nachlauf. Im Studio 12.11. bestätigt.
- [x] **Ablage auf dem NAS** (v0.6.0, Systemkarte stage-system 4223b1b, Marlon 10.11.2026): Klappen-Takes nach
  `<Ablage>/<PROJEKT>/<Datum>_STUDIO/08_TRACKING/<Slate>_T<NN>_<studio_take.id>.*`; Ablage über Merkdatei
  `.stage-ingest-ablage.json` (Laufwerke 2 Ebenen tief, dann \\TRUENAS\Footage); erst lokal, dann kopieren, nie
  überschreiben, offene Takes jede Minute erneut. Hand-Takes bleiben lokal. Erster echter Lauf 12.11. (Auftragsliste leer).
- [ ] Clipname der Kamera übernehmen (im schmalen Stage-Modus nicht verfügbar)
- [ ] Mars-Filter: bleibt aus; Stage prüft am Studiotermin, ob das Frustum zittert (Besitz Mars: Stage)
- [ ] Rückmeldung an die Stage (Vorschlag Stage-Session: `teilnehmer.melden`, Feld `take.tracking`) — neue Schnittstelle,
  braucht Marlons Zustimmung und Eintrag in stage-system SCHNITTSTELLEN.md
- [x] Posen-Sicherung der Stage und TrackingRecorder bleiben beide (Marlon 10.11.); für die Post gilt der TrackingRecorder
- [ ] Versatz Tracker→Perspektivpunkt pro Take mitschreiben, aktiv vom Stage-Server (`kamerakal`) holen — siehe `docs/KAMERA-RIG.md`

## Zugang PC .3 (Aufnahme-PC „i9")

`ssh Ndisplay@192.168.100.3` vom Server-Claude (Schlüssel, PowerShell als Shell, Administrator).
Agenten-Ablage nur unter `C:\claude\`; Exporte fürs Projekt ins Abo `D:\Projekte\<PROJEKT>\06_VFX_RENDER\` (Marlon 12.11.).
App installiert unter %LOCALAPPDATA%\TrackingRecorder, Firewall-Regeln eingetragen.
Nuke-PC x3950: `192.168.100.8:9876` (nuke-mcp, `execute_python`) — lesend nutzbar, Änderungen nur nach Absprache.

## Entwicklung

```bash
dotnet test
dotnet run --project src/TrackingRecorder.Cli -- --port 6001 --fps 25 --out take01.csv
```
