"""Prüft vergleich.py mit erfundenen Daten bekannter Wahrheit: Mars-Take (CSV) und daraus eine „Nuke"-Kamera (.chan)
mit bekannter Welt-Umrechnung, Versatz, Spiegelung, Zeitversatz und Rauschen. Danach muss vergleich.py sie wiederfinden.

  werkzeuge/.venv/bin/python werkzeuge/synthetisch.py [ordner]
"""
import json, math, os, subprocess, sys
import numpy as np
from scipy.spatial.transform import Rotation as R

sys.path.insert(0, os.path.dirname(__file__))
from tracking import Spur, Umrechnung, schreibe_chan, frames_tc, tc_frames

ordner = sys.argv[1] if len(sys.argv) > 1 else "/tmp/tr-synth"
os.makedirs(ordner, exist_ok=True)
rng = np.random.default_rng(7)
fps, n, start = 25.0, 750, "15:02:11:04"
t0 = tc_frames(start, fps)
t = np.arange(n) / fps
# Kamerabewegung: Fahrt + Schwenk + Neigung + Höhe, Mars-Welt (m, Y oben)
p = np.column_stack([1.5 + 0.8 * np.sin(t * 0.4), 1.6 + 0.2 * np.sin(t * 0.9), -1.0 + 1.2 * np.sin(t * 0.25)])
eul = np.column_stack([10 * np.sin(t * 0.7), 40 * np.sin(t * 0.3), -38 + 5 * np.sin(t * 1.1)])   # xyz Grad
rot = R.from_euler("xyz", eul, degrees=True)

# Mars-CSV im Format des TrackingRecorders (mit 0,1 mm / 0,01° Rauschen)
pm = p + rng.normal(0, 0.0001, p.shape)
rm = R.from_rotvec(rng.normal(0, math.radians(0.01), (n, 3))) * rot
csv = os.path.join(ordner, "mars.csv")
with open(csv, "w") as f:
    f.write("utc_iso,mono_ms,device_ms,timecode,fps,serial,kind,x_m,y_m,z_m,qx,qy,qz,qw\n")
    for i in range(n):
        q = rm[i].as_quat()
        f.write(f"x,{i*40},{1000+i*40},{frames_tc(t0+i, fps)},25,LHR-CAM,tracker,{pm[i,0]},{pm[i,1]},{pm[i,2]},{q[0]},{q[1]},{q[2]},{q[3]}\n")
        f.write(f"x,{i*40},{1000+i*40},{frames_tc(t0+i, fps)},25,LHR-FIX,tracker,0,0.05,0,0,0,0,1\n")

# Wahrheit: Nuke-Welt (Massstab 37, gedreht, verschoben, gespiegelt), Versatz Tracker→Kamera, Zeitversatz 2,4 Bilder
wahr = Umrechnung(37.0, R.from_euler("xyz", [5, -120, 3], degrees=True).as_rotvec(), np.array([12.0, -3.0, 40.0]),
                  R.from_euler("xyz", [1.5, 7.5, -38.7], degrees=True).as_rotvec(), np.array([0.066, 0.057, -0.2655]),
                  spiegel=True, dt=2.4)
mars_wahr = Spur(t0 + np.arange(n, dtype=float), p, rot, fps, "wahr")
ref = wahr.anwenden(mars_wahr.bei(t0 + np.arange(n, dtype=float) + wahr.dt))
ref.t = t0 + np.arange(n, dtype=float)
ref.p = ref.p + rng.normal(0, 0.02, ref.p.shape)                      # Solve-Rauschen (Nuke-Einheiten)
ref.rot = R.from_rotvec(rng.normal(0, math.radians(0.02), (n, 3))) * ref.rot
chan = os.path.join(ordner, "nuke.chan")
schreibe_chan(chan, ref, start_tc=start)

out = os.path.join(ordner, "bericht")
subprocess.run([sys.executable, os.path.join(os.path.dirname(__file__), "vergleich.py"), csv, chan,
                "--start-tc", start, "--aus", out], check=True)
b = json.load(open(out + ".json"))
u = b["umrechnung"]
fehler = {
    "massstab": abs(u["massstab"] - 37.0) / 37.0,
    "zeitversatz": abs(u["zeitversatz_bilder"] - 2.4),
    "versatz_mm": float(np.linalg.norm(np.array(u["versatz"]["translation_m"]) - wahr.tx) * 1000),
    "versatz_grad": float(np.degrees((R.from_matrix(u["versatz"]["rotation_matrix"]).inv() * R.from_rotvec(wahr.rx)).magnitude())),
    "spiegel": u["spiegeln_z"] == True,
}
print(json.dumps(fehler, indent=1))
ok = fehler["massstab"] < 0.002 and fehler["zeitversatz"] < 0.15 and fehler["versatz_mm"] < 3 and fehler["versatz_grad"] < 0.1 and fehler["spiegel"]
print("Alles innerhalb der Grenzen" if ok else "AUSSERHALB DER GRENZEN")
sys.exit(0 if ok else 1)
