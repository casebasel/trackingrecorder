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

## Test 1 (14:53:04 – 14:53:24): nur durchlaufen, keinen Tracker angefasst

`RawData_2026-11-10_14-53-04_test1-durchlaufen.fbx` + FreeD-Mitschnitt im selben Zeitraum:

- Rover 1 (Kamera): Spanne unter 0,3 mm, Drehung 0,000° — **stabil**. Keine Sprünge.
- Rover 3: Spanne unter 0,4 mm, Drehung unter 0,04°.
- Basisstationen: bewegen sich **nicht** (0,0 cm).
- FreeD zeigt dasselbe wie Path Recorder.
- Eingefroren: Rover 1 in 410 von 475 FreeD-Paketen gleiche Pose wie davor → Totzone bestätigt sich.

→ **Vermutung A (Abdeckung) ist unwahrscheinlich.** Durchlaufen stört das Tracking nicht.
Bleibt Vermutung B (Rover 3 als Anker) — Test 2 steht aus.

## Test 2 (14:53:49 – 14:54:09): nur Rover 3 langsam angehoben und wieder abgelegt

`RawData_2026-11-10_14-53-49_test2-rover3-heben.fbx`:

- Rover 3: sauber von 7 cm auf ~2,03 m und zurück, ohne Ausreisser (max. 2,7 m/s). Abgelegt ~20 cm neben dem Start.
- Rover 1 (Kamera): Spanne unter 0,5 mm, Drehung 0,01° — **stabil**.
- Basisstationen: bewegen sich **nicht**.

→ **Vermutung B (Rover 3 als Anker) ist ebenfalls widerlegt.** Anheben von Rover 3 verschiebt die Welt nicht.

## Zwischenstand

Im Normalbetrieb ist Mars stabil (Kamera im Stand < 0,5 mm). Der Fehler in der ersten Aufnahme hängt am
**Neuberechnen der Raumkarte** (BS1–3 wandern bis 23 cm), nicht an Abdeckung oder Rover 3. Offene Frage:
Was hat das ausgelöst? Verdacht: Die erste Aufnahme lag kurz nach dem Einstellen des Timecodes am Mars
(14:44–14:49) — evtl. Neustart/Neuinitialisierung, die sich noch einpendelte.

Für die App heisst das: **Kartenwechsel erkennen ist Pflicht.** Ein solcher Take muss rot markiert werden,
sonst merkt man es erst beim Rendern. Path Recorder kennt die BS-Posen — woher, gilt es herauszufinden.

## Test 3 (14:56:42 – 14:57:04): Massstab, Rover 3 auf dem Boden 2,00 m versetzt

`RawData_2026-11-10_14-56-43_test3-massstab.fbx`:

- **Massstab stimmt:** 2,00 m am Massband → Mars 199,9 / 200,0 cm, Höhe dabei +0,2 cm. Wiederholgenauigkeit
  zweier Ruhephasen am selben Ort: 0,2 cm.
- Zwischenhalt erhöht (Tisch/Kiste?): Mars +73,9 cm über Boden — echte Höhe noch offen.
- **Ausreisser Rover 3 beim Tragen:** um 14:56:55 kurz −65 cm, also unter dem Boden.
- **Kamera (Rover 1) dreht sich um 0,3°**, ohne angefasst zu werden (14:56:51:21–23, Rover 3 lag da erhöht).
  0,3° entsprechen beim 35 mm auf 4K grob 30 px Versatz im Bild.
- **Basisstationen springen wieder**, obwohl alles ruht: BS3 um 4,7 cm (14:57:01:11) und 6,4 cm (14:57:03:06),
  BS1 um 4,1 cm (14:57:04:01). Rover 1 reagiert darauf diesmal nicht.

→ Massstab ist kein Problem. Die Störungen sind **kurze Fehllagen beim Bewegen** (Rover 3 unter dem Boden)
und **kleine Drehsprünge der stehenden Kamera**. Beides muss die App erkennen und melden.

## Test 4 (15:01:50 – 15:02:09): nach neuem Ground Setup, 2-m-Test wiederholt

`RawData_2026-11-10_15-01-50_test4-groundsetup-2m.fbx`:

- **Neuer Nullpunkt:** Rover 3 am Boden jetzt bei −1,2 bis −2,1 cm Höhe (vorher +7,7 cm). Kamera (Rover 1)
  Startlage −109 / 162 / −155 cm (vorher ca. −352 / 173 / −176) → das Koordinatensystem hat sich verschoben.
- 2,00 m → 200,0 / 199,7 cm. Höhenunterschied über die 2 m: −0,8 cm (Boden oder Bodenebene ~0,2° schief).
- Basisstationen: **keine Bewegung**. Kamera: stabil (< 0,2 mm). Rover 3: keine Fehllage beim Tragen.

⚠ Das Ground Setup verschiebt die Welt. Alles, was in Mars-Weltkoordinaten eingemessen wurde (Leinwandlage,
nDisplay-Root der Stage), stimmt danach nicht mehr. Der Versatz Tracker→Pupille liegt im Tracker-Raum und bleibt gültig.

## Test 5 (15:17:15 – 15:17:40): Stationary tolerance + Motion Filter aus

`RawData_2026-11-10_15-17-15_test5-pktmon.fbx`, Kamera (Rover 1) unberührt:

- **Eingefroren: 0 von 627 Bildern** (vorher ~80 %) → die Rohdaten kommen jetzt ungefiltert.
- Rauschen der stehenden Kamera (Standardabweichung): Position 0,06–0,11 mm, Drehung **0,007–0,010°**
  (Spanne bis 0,06°). Grob gerechnet (Amira 4K, 35 mm, ~100 px pro Grad): Drehrauschen ≈ 1 px,
  Spitzen bis ~6 px. Die Drehung ist damit der grösste Fehleranteil; Glätten in der Nachbearbeitung muss
  vor allem dort ansetzen.
- Basisstationen ruhig.
