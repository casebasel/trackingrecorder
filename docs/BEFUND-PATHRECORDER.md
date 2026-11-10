# Befund: Path-Recorder-Aufnahme vom 10.11.2026

Datei: `RawData_2026-11-10_14-48-21_14-48-20-23.fbx` (abgelegt unter `~/daten/trackingrecorder/2026-11-10_pathrecorder/`,
gelesen mit `werkzeuge/fbx_lesen.py`). 19 s, 476 Bilder.

## Format (Vorbild für unseren FBX-Export)

- FBX 7700 binär, FBX SDK 2020.3.4
- Y oben, cm, Bildrate PAL 25 (`TimeMode 10`), ein Key pro Bild, Drehreihenfolge 5 (ZYX)
- Objekte: `Model_PORT1`, `Model_PORT3` (Rover als Kamera-Modelle), `Model_BS1..4` (Basisstationen als Lichter)
- **Timecode pro Bild als animierte Eigenschaften `TCHour`, `TCMinute`, `TCSecond`, `TCFrame`** an jedem Modell.
  Das ist das Schema, das Unreal (Take Recorder / FBX-Import) versteht.
- Timecode 14:48:20:24 → 14:48:39:24, lückenlos (jeder Schritt genau 1 Bild)

## Was in den Daten auffällt

1. **Ausreisser Rover 3 (14:48:28:10 – ca. 28:24):** Sprung um 262 cm in einem Bild (= 65 m/s), dann
   ~50 cm pro Bild nach oben bis 7,96 m Höhe, danach zurück. Physikalisch unmöglich → Tracking verloren
   und falsch wiedergefunden. Muss automatisch erkannt und markiert werden.
2. **Basisstationen „wandern":** BS1–BS3 verschieben sich zwischen 30:18 und 32:13 um bis zu 23 cm und
   bis zu 17°. Die stehen physisch still → Mars rechnet seine Raumkarte neu. Damit verschiebt sich das
   ganze Koordinatensystem. Rover 1 springt in derselben Zeit mehrfach um 10–13 cm pro Bild
   (zeitlicher Zusammenhang, Ursache nicht bewiesen). BS4 bleibt fest.
3. **Eingefrorene Posen:** In ruhigen Phasen ist die Pose über viele Bilder **bitgenau gleich**
   (Rover 1 z. B. die ganze Sekunde 22). Ein echter Tracker rauscht immer ein wenig. Das spricht für eine
   Totzone/Stabilisierung im Mars: kleine Bewegungen werden verschluckt und beim Losfahren gibt es
   einen kleinen Ruck. Für Postproduktion unerwünscht → prüfen, ob sie sich abschalten lässt.

## Was Marlon dabei gemacht hat

- **Rover 1 stand still.** Marlon ist nur vorne durchgelaufen. Trotzdem wandert Rover 1 in der Aufnahme
  um bis zu 37 cm (X) und 32 cm (Höhe), mit Sprüngen bis 13 cm pro Bild.
- **Rover 3 wurde vom Boden hochgehoben** → dabei der Ausreisser bis ~8 m.

Zwei Vermutungen, nicht bewiesen:
- **A — Abdeckung:** Marlon verdeckt beim Durchlaufen Basisstationen; Mars rechnet die Raumkarte neu
  (wandernde BS1–3), das ganze Koordinatensystem rutscht, Rover 1 „bewegt" sich mit.
- **B — Rover 3 als Anker:** Mars meldet Rover 3 als Recenter-Ziel (`/api/GetRecenterTarget`), und im FreeD
  liegt Rover 3 bei ~0/0/48 mm, also im Nullpunkt. Hängt der Nullpunkt live an Rover 3, verschiebt Anheben
  von Rover 3 die ganze Welt — auch die stillstehende Kamera.

Test zum Trennen: (1) Rover 3 liegen lassen, nur vor der Kamera durchlaufen. (2) Niemand läuft, nur Rover 3
anheben. Rover 1 steht beide Male still.

## Folgen für den TrackingRecorder

- Ausreisser-Erkennung über Geschwindigkeit/Beschleunigung (Grenzwerte für ein Kamera-Rig), markieren,
  beim Export wahlweise ersetzen.
- Posen der Basisstationen mit aufnehmen (wenn über Live Link erreichbar) und Kartenwechsel melden;
  beim Export Sprünge des Koordinatensystems herausrechnen.
- Gleiche aufeinanderfolgende Posen zählen und anzeigen („Totzone aktiv?").
- FBX-Export im selben Schema (TCHour…TCFrame), damit Unreal den Timecode übernimmt.
