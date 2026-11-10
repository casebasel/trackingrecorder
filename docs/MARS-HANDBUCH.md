# VIVE Mars — was das Handbuch sagt (gelesen 10.11.2026)

Quelle: „VIVE Mars Base Camp" (https://vive-mars.notion.site/, 70 Seiten), Changelog
(https://vive-mars.notion.site/Software-update-history-ebd6c67b905c440198916c521fd9ecf1), User Guide PDF 2022,
Aximmetry-Forum. Nicht lesbar: mars.vive.com (403). Neueste dokumentierte Firmware: 3.07.999.4.
„Belegt" = steht in der Doku, „Vermutung" = unsere Ableitung.

## Für die Rohaufnahme einstellen (Dashboard > Settings)

- **Tracking stationary tolerance** — Ruhemodus mit Schwelle, liefert in Ruhe eine feste Pose (= unsere
  „eingefrorenen" Posen). Abschaltbar. → **aus**
- **Motion Filter** (Manual / Motor movement) — glättet langsame Bewegungen, brachte laut Forum nach 3.01 teils
  neues Ruckeln. Abschaltbar. → **aus**
- Kein Schalter für die interne Jitter-Optimierung (3.07).
- Bildstabilisator in Kamera/Objektiv aus. Timecode **und** Genlock an (HTC: nötig für Postproduktion).

## Raumkarte / Basisstationen

- Kein „Lock" dokumentiert. Es gibt **Reset tracking volume** (auch per Remote Control :7008).
- Basisstationen nie im Betrieb verschieben; sonst kann der Ursprung driften → Mars neu starten.
- **Kanal-Konflikte** der Basisstationen verursachen instabiles Tracking; Mars erkennt sie seit 3.07.
  Prüfen: alle 4 BS im Path Recorder sichtbar? Sonst Kanal ändern + Reset tracking volume.
- Basisstationen 25–45° nach unten, > 2 m hoch, Tracker von oben sehen. Spiegelungen/helles Licht stören.
- Mars-Logs: Settings > System logs > Export auf USB.

## Koordinatensystem

- **Recenter**: angetippter Rover wird Ursprung für alle. Danach nicht mehr bewegen.
- **Ground Setup**: richtet die Neigung von virtuellem und echtem Boden aus (= Verschiebung nach unserem Test 4).
- Ursprung seit 3.01 an der Rover-Unterkante, wählbar seit 3.02. Pivot der Pose: immer Tracker-Unterkante.
  Achsen „je Protokoll optimiert" → FreeD- und Live-Link-Achsen können sich unterscheiden.

## Timecode

- LTC über 3,5-mm-„TC input", Genlock über BNC „REF input". LTC max. 30 fps.
- In **Live Link** und **Path Recorder** (pro Bild) enthalten. **FreeD: nicht dokumentiert** — bei uns 0000.

## FreeD

- Port frei wählbar (Standard 40000), Broadcast erlaubt, seit 3.01 pro Rover eigene Ziel-IP.
- Camera ID = Rover-Anschluss. Zoom/Fokus vom Encoder gehen direkt in FreeD.
- Rover-USB-Anschlüsse: 1 = Tracker, **2 = Zoom, 3 = Fokus, 4 = Blende**.
  → Vermutung: Zoom/Fokus = 0 bei uns, weil der Encoder nicht an 3 steckt oder nicht erkannt ist
  (Dashboard zeigt bei Erkennung ein Zahnrad am Rover).

## Live Link

- Unreal UDP Message Bus; in Unreal wird Mars als **Static Endpoint `192.168.100.60:6666`** eingetragen.
  → Unsere App muss sich genauso als Message-Bus-Knoten anmelden; passiv mithören reicht nicht.
- Pro Rover ein Subject, Daten: Transform, Kamera-/FIZ-Rolle, Timecode.
- Mit nur einem Encoder (nur Fokus) kommen über Live Link **keine** FIZ-Daten an → HTC empfiehlt dafür FreeD.

## Path Recorder

- BETA seit 3.02, verbindet sich auf **Mars-Port 5500** (eigenes Protokoll; von VM 170 aus per TCP zu).
- Export nur FBX `RawData_<datum>_<zeit>_<start-TC>.fbx`, Timecode pro Bild. Ob vor/nach Filtern: nicht dokumentiert.
- Über Port 5500 kommen offenbar auch die Basisstations-Posen → Kandidat für unsere App.

## Objektiv

- Camera Calibration Tool: 2 Rover (Kamera + Schachbrett), Aufnahmen bei 0° / ±45°.
  Liefert Fx/Fy, k1/k2, Cx/Cy und **Nodal Offset**; Export `.ulens` für Unreal.
- Encoder nullen: an den Anschlag (Nah / Weit), dann nullen. Sensorgrösse im 16:9-Ausschnitt angeben.

## Latenz

- Keine Latenzwerte veröffentlicht. Tracking kommt „meist früher als das Video" → in Unreal Time Correction /
  Timed Data Monitor. Prediction/Extrapolation nicht erwähnt.
