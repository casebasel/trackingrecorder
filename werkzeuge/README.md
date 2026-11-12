# Werkzeuge

Python-Umgebung einmal anlegen (VM 170): `python3 -m venv werkzeuge/.venv && werkzeuge/.venv/bin/pip install numpy scipy matplotlib`

| Datei | Was |
| --- | --- |
| `vergleich.py` | Mars-Take (CSV) gegen Referenzkamera (Nuke `.chan`, Unreal-FBX mit Begleit-JSON `startTc`, Path-Recorder-FBX, Posen-Sicherung `.jsonl`). Schätzt Welt-Umrechnung inkl. Massstab und Händigkeit, Versatz Tracker→Kamera und Zeitversatz; meldet Restfehler in Position, Grad und geschätzten Pixeln. Ausgabe `.json`, `.csv`, `.png`. |
| `export.py` | Mars-Take als Nuke-`.chan`, roh (cm, Y oben) oder mit der Umrechnung aus `vergleich.py` im Raum der Referenz (ein Key pro Bild, Bild 0 = `--start-tc`). |
| `synthetisch.py` | Prüft `vergleich.py` mit erfundenen Daten bekannter Wahrheit — muss „Alles innerhalb der Grenzen" melden. |
| `tracking.py` | Gemeinsame Bausteine (Lesen/Schreiben, Timecode, Umrechnung). |
| `fbx_analyse.py`, `fbx_lesen.py` | FBX lesen (Path Recorder), Ruhephasen/Sprünge auswerten. |
| `pcapng_lesen.py` | pktmon-Mitschnitte lesen (so wurde das Mars-Protokoll entschlüsselt). |
| `release.sh` | Neue App-Version bauen, mit Velopack packen, auf GitHub veröffentlichen. |

## Ablauf Genauigkeitstest (Greenscreen)

1. Take gleichzeitig in Unreal (Recording, Modus „Welt") und im TrackingRecorder, mit der Klappe.
2. Nuke 17: Solve je Take, Kamera als `.chan` exportieren (Bild 0 = Clip-Start, Start-TC aus dem Clip).
3. `vergleich.py TAKE.csv nuke.chan --start-tc <Clip-Start-TC>` → Fehler in Pixeln, Versatz, Zeitversatz.
4. `vergleich.py TAKE.csv <Slate>_T<NN>_stagekamera.fbx` (Begleit-JSON daneben) → Umrechnung Mars → Unreal-Kamera.
5. `export.py TAKE.csv --umrechnung bericht.json` → Mars-Kamera im Raum der Referenz für Nuke/Unreal.
