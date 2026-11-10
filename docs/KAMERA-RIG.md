# Kamera-Rig: Versatz Tracker → Perspektivpunkt

Quelle: Session `vp-companion-app` (Stage), Antwort vom 10.11.2026. Nur übernommen, nicht selbst nachgemessen.

## Aktive Kalibrierung

- ID `01M575HPVX446XRMJH7XHRAFFP`, 18.10.2026 09:30 UTC
- Objektiv Sony CineAlta 35 mm T2, Eintrittspupille 9 cm hinter der Frontlinse
- 4 Lagen, Streuung 6,2 mm, Achse 0,32°
- **Ziel ist die Eintrittspupille (Perspektivpunkt), nicht der Sensor.**

| | Wert |
|---|---|
| x | 4,306 cm |
| y | 7,064 cm |
| z | −27,589 cm |
| Pitch | −0,236° |
| Yaw | 7,455° |
| Roll | −41,739° |

Konvention: Unreal-Achsen (X vorn, Y rechts, Z oben, cm, FRotator in Grad), im lokalen Raum des
Live-Link-Subjects. Anwendung als Kind unter dem Tracker:

```
Kamera_Welt = Versatz × Tracker × nDisplay-Root    (FTransform-Reihenfolge von Unreal)
```

Referenz-Implementierung (TypeScript): `vp-companion-app/packages/shared/src/kalibrierung.ts`, `versatz()` und `perspektive()`.

## Wo der Versatz liegt

- Stage-Server (VM 160), Tabelle der Kamera-Kalibrierungen; per WebSocket Ereignis `kamerakal` → `aktiv.versatz`
- Kopie vom Plugin: `<Projekt>/Saved/StageCompanion/KameraVersatz.json`
- Angewendet **nur in Unreal** (Stage-Companion-Plugin, `StageCompanionKalibrierung.cpp`). Im Mars ist nach
  bisherigem Wissen kein Versatz eingetragen (im Calibration Tool ungeprüft).
- Ohne aktiven Versatz: Live Link Component Controller mit OffsetTransform = nur 40° Roll
  (Quaternion X 0,342020, W 0,939693), keine Translation.

## Unreal-Seite

- Kamera: CineCameraActor „Arri" (`CineCameraActor_0` in `Stage_Main`), External Camera der ICVFX-Kamera in `NDC_Rueckprojektion`
- Live-Link-Subjects: `4JI58BJ-Rover1` = Kamera, `4JI58BJ-Rover3` = Kalibrier-Tracker
- Quelle: Preset `LiveLink_Mars` (Message Bus, ViveLiveLinkPlugin)

## Folgen für den TrackingRecorder

1. **Aufnahme immer roh** — die Tracker-Pose so, wie sie vom Mars kommt. Marlon (18.10.2026):
   „nichts stabilisieren, nichts interpolieren". Glättung höchstens als optionaler, abschaltbarer
   Nachbearbeitungsschritt beim Export, nie in der Aufnahme.
2. Versatz **nicht in die Rohdaten einrechnen**, sondern pro Take mitschreiben (Kalibrierungs-ID + Werte).
   Beim Export wahlweise Tracker-Bahn oder Perspektivpunkt-Bahn ausgeben.
3. Später den aktiven Versatz direkt vom Stage-Server (`kamerakal`) holen statt von Hand eintragen.
4. Achsen/Einheiten von FreeD (mm, Pan/Tilt/Roll) ↔ Live Link (Unreal cm, FRotator) an echten Paketen abgleichen.
