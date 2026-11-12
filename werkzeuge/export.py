"""Exportiert die Kamera eines Mars-Takes (CSV des TrackingRecorders) als Nuke-.chan.

  werkzeuge/.venv/bin/python werkzeuge/export.py TAKE.csv [--umrechnung bericht.json] [--start-tc 15:02:11:04]
        [--serial LHR-0FFCBEE6] [--aus take.chan]

Ohne --umrechnung: rohe Tracker-Bahn in Mars-Koordinaten, in cm (Y oben) — gut zum Ansehen, aber noch ohne
Versatz Tracker→Kamera und ohne Nullpunkt von Unreal/Nuke.
Mit --umrechnung (Ausgabe von vergleich.py): Kamera im Raum der Referenz, also mit Versatz, Massstab, Nullpunkt
und Zeitversatz — so liegt sie in Nuke bzw. Unreal auf der Referenzkamera.
--start-tc: Bildnummer 0 der .chan = dieser Timecode (Clip-Start); sonst die Bildnummer seit 00:00:00:00.
In Nuke 17: Camera-Node › File › Import chan file; Drehreihenfolge des Nodes ZXY lassen.
"""
import argparse, json, os, sys
sys.path.insert(0, os.path.dirname(__file__))
from tracking import Spur, Umrechnung, lade_mars_csv, schreibe_chan, frames_tc

ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
ap.add_argument("csv"); ap.add_argument("--umrechnung"); ap.add_argument("--start-tc"); ap.add_argument("--serial")
ap.add_argument("--rotorder", default="zxy"); ap.add_argument("--aus")
a = ap.parse_args()

m = lade_mars_csv(a.csv, a.serial)
if a.umrechnung:
    j = json.load(open(a.umrechnung))
    u = Umrechnung.von_json(j.get("umrechnung", j))
    k = u.anwenden(m)
else:
    k = Spur(m.t, m.p * 100.0, m.rot, m.fps, m.name)
# ein Key pro ganzem Bild (der Zeitversatz verschiebt sonst auf Bruchteile)
import numpy as np
ganz = np.arange(np.ceil(k.t[0]), np.floor(k.t[-1]) + 1)
k = k.bei(ganz)
aus = a.aus or os.path.splitext(a.csv)[0] + ".chan"
schreibe_chan(aus, k, start_tc=a.start_tc, rotorder=a.rotorder)
print(f"{len(k.t)} Bilder {frames_tc(int(k.t[0]), k.fps)}–{frames_tc(int(k.t[-1]), k.fps)} · Tracker {m.name} → {aus}")
