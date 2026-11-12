"""Schneller Drehungs-Vergleich Mars ↔ Nuke-.chan (nur Rotationen, Hand-Auge-Fit, Zeitversatz, Aufteilung des Fehlers).
  werkzeuge/.venv/bin/python werkzeuge/drehvergleich.py MARS.csv NUKE.chan START_TC_BILD0 [--serial …] [--breite 1920]"""
import argparse, sys, os, numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from scipy.spatial.transform import Rotation as R
from scipy.optimize import least_squares
from tracking import lade_mars_csv, tc_frames
ap = argparse.ArgumentParser(); ap.add_argument("mars"); ap.add_argument("chan"); ap.add_argument("start_tc")
ap.add_argument("--serial", default="LHR-0FFCBEE6"); ap.add_argument("--breite", type=int, default=1920)
ap.add_argument("--hfov", type=float, default=2*np.degrees(np.arctan(23.76/2/35)))
a = ap.parse_args()
m = lade_mars_csv(a.mars, a.serial)
c = np.loadtxt(a.chan); t = c[:, 0] + tc_frames(a.start_tc, 25)
sel = (t > m.t[0] + 20) & (t < m.t[-1] - 20); c, t = c[sel], t[sel]
ref = R.from_euler("zxy", c[:, [6, 4, 5]], degrees=True)
M = np.diag([1, 1, -1.])
def fit(dt, mir):
    mr = m.bei(t + dt).rot; mm = R.from_matrix(M @ mr.as_matrix() @ M) if mir else mr
    res = lambda x: ((ref.inv() * (R.from_rotvec(x[:3]) * mm * R.from_rotvec(x[3:]))).as_rotvec()).ravel()
    best = None
    for x0 in [np.zeros(6), np.r_[0,0,np.pi/2,0,0,0], np.r_[np.pi/2,0,0,0,0,0], np.r_[0,np.pi/2,0,0,0,0], np.r_[np.pi,0,0,0,0,0]]:
        r = least_squares(res, x0, method="lm")
        if best is None or r.cost < best.cost: best = r
    return best, mm
best = None
for mir in (False, True):
    for dt in np.arange(-6, 6.01, 0.25):
        r, mm = fit(dt, mir)
        if best is None or r.cost < best[0].cost: best = (r, mm, dt, mir)
r, mm, dt, mir = best
W, X = R.from_rotvec(r.x[:3]), R.from_rotvec(r.x[3:])
ev = np.degrees((ref.inv() * (W * mm * X)).as_rotvec()); tot = np.linalg.norm(ev, axis=1)
sm = np.array([np.convolve(ev[:, k], np.ones(25) / 25, "same") for k in range(3)]).T
pxg = a.breite / a.hfov
fwd = ref.apply([0, 0, -1]); tilt = np.degrees(np.arcsin(fwd[:, 1])); pan = np.degrees(np.arctan2(fwd[:, 0], -fwd[:, 2]))
print(f"{len(t)} Bilder · Zeitversatz {dt:+.2f} Bilder ({dt*40:+.0f} ms) · gespiegelt {mir}")
print(f"Drehfehler RMS {np.sqrt((tot**2).mean()):.3f}° ≈ {np.sqrt((tot**2).mean())*pxg:.0f} px · p95 {np.percentile(tot,95):.3f}°")
print(f"  langsam (1 s) {np.sqrt((np.linalg.norm(sm,axis=1)**2).mean()):.3f}° · schnell {np.sqrt((np.linalg.norm(ev-sm,axis=1)**2).mean()):.3f}° ≈ {np.sqrt((np.linalg.norm(ev-sm,axis=1)**2).mean())*pxg:.1f} px")
for name, v in (("Neigung", tilt - tilt.mean()), ("Schwenk", pan - pan.mean())):
    for k, lab in enumerate("xyz"):
        cc = np.corrcoef(v, ev[:, k])[0, 1]
        if abs(cc) > 0.5: print(f"  Fehler um Kamera-{lab} folgt der {name}: r={cc:+.2f}, {np.polyfit(v, ev[:, k], 1)[0]*100:+.2f} % des Winkels")
print(f"  Spanne Neigung {np.ptp(tilt):.1f}°, Schwenk {np.ptp(pan):.1f}°")
