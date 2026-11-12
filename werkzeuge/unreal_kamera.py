"""Mars-Take → Kamera in Unreal-Weltkoordinaten, je Bild des Clips, als JSON (für Sequencer per Python).

Kette wie im Stage-Companion-Plugin (Stage-Session 12.11.2026): Kamera „Arri" = Versatz × Tracker × Root.
- Tracker: Live-Link-Pose von Rover 1. Das Mars-Live-Link-Plugin rechnet nur die Achsen um (gemessen 12.11.2026
  an T02–T04: 0,3 cm / 0,1° Rest):  p_ue = 100 · A · p_mars,  R_ue = A · R_mars · Aᵀ,  A = [[0,0,1],[-1,0,0],[0,1,0]].
- Versatz: Kalibrierung der Stage (Tracker → Pupille), Root: Nullpunkt der Einstellung (aus der Posen-Sicherung).
- Zeitversatz: Mars-Bild t gehört zum Kamerabild t − versatz (Vergleich mit Nuke 12.11.: −2 Bilder).

  werkzeuge/.venv/bin/python werkzeuge/unreal_kamera.py TAKE.csv --clip-start 14:09:44:08 --bilder 558
        [--versatz-bilder -2] [--brennweite 35] [--aus kamera.json]
"""
import argparse, json, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from tracking import lade_mars_csv, tc_frames, frames_tc

A = np.array([[0, 0, 1], [-1, 0, 0], [0, 1, 0]], float)


def frotator_matrix(roll, pitch, yaw):
    """Wie Unreal FRotationMatrix: Spalten = X-, Y-, Z-Achse des Objekts in Weltkoordinaten."""
    r, p, y = np.radians([roll, pitch, yaw])
    SR, CR, SP, CP, SY, CY = np.sin(r), np.cos(r), np.sin(p), np.cos(p), np.sin(y), np.cos(y)
    X = [CP * CY, CP * SY, SP]
    Y = [SR * SP * CY - CR * SY, SR * SP * SY + CR * CY, -SR * CP]
    Z = [-(CR * SP * CY + SR * SY), CY * SR - CR * SP * SY, CR * CP]
    return np.column_stack([X, Y, Z])


def matrix_frotator(M):
    """Umkehrung von frotator_matrix (wie FMatrix::Rotator)."""
    X, Y, Z = M[:, 0], M[:, 1], M[:, 2]
    pitch = np.degrees(np.arctan2(X[2], np.hypot(X[0], X[1])))
    yaw = np.degrees(np.arctan2(X[1], X[0]))
    # Roll aus der Y-Achse gegenüber der Y-Achse ohne Roll
    Y0 = frotator_matrix(0, pitch, yaw)[:, 1]
    Z0 = frotator_matrix(0, pitch, yaw)[:, 2]
    roll = np.degrees(np.arctan2(np.dot(Y, Z0) * -1, np.dot(Y, Y0)))
    return float(roll), float(pitch), float(yaw)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("csv"); ap.add_argument("--serial", default="LHR-0FFCBEE6")
    ap.add_argument("--clip-start", required=True); ap.add_argument("--bilder", type=int, required=True)
    ap.add_argument("--versatz-bilder", type=float, default=-2.0)
    ap.add_argument("--versatz", default="6.60,5.71,-26.55,-0.02,7.49,-38.69", help="x,y,z cm, pitch,yaw,roll °")
    ap.add_argument("--root", default="297,332,31.561928,-38.151222", help="x,y,z cm, yaw °")
    ap.add_argument("--brennweite", type=float, default=35.0)
    ap.add_argument("--filmback", default="23.76,13.365")
    ap.add_argument("--aus")
    a = ap.parse_args()

    m = lade_mars_csv(a.csv, a.serial)
    vx, vy, vz, vp, vyaw, vr = (float(x) for x in a.versatz.split(","))
    rx, ry, rz, ryaw = (float(x) for x in a.root.split(","))
    Rv, tv = frotator_matrix(vr, vp, vyaw), np.array([vx, vy, vz])
    Rr, tr = frotator_matrix(0, 0, ryaw), np.array([rx, ry, rz])
    fb = [float(x) for x in a.filmback.split(",")]

    t0 = tc_frames(a.clip_start, m.fps)
    clip_t = t0 + np.arange(a.bilder, dtype=float)
    mars_t = clip_t + a.versatz_bilder
    innen = (mars_t >= m.t[0]) & (mars_t <= m.t[-1])
    s = m.bei(np.clip(mars_t, m.t[0], m.t[-1]))
    bilder = []
    for i in range(a.bilder):
        p_tr = 100.0 * A @ s.p[i]
        R_tr = A @ s.rot[i].as_matrix() @ A.T
        R_cam = Rr @ R_tr @ Rv
        p_cam = Rr @ (R_tr @ tv + p_tr) + tr
        roll, pitch, yaw = matrix_frotator(R_cam)
        bilder.append({"bild": i + 1, "timecode": frames_tc(int(clip_t[i]), m.fps), "gueltig": bool(innen[i]),
                       "location": [round(float(v), 4) for v in p_cam], "rotation": {"pitch": round(pitch, 5), "yaw": round(yaw, 5), "roll": round(roll, 5)}})
    out = {"quelle": os.path.basename(a.csv), "tracker": m.name, "variante": "mars",
           "clip_start_tc": a.clip_start, "fps": m.fps, "bilder": a.bilder, "zeitversatz_bilder": a.versatz_bilder,
           "brennweite_mm": a.brennweite, "filmback_mm": fb,
           "kette": "Kamera = Versatz × Tracker × Root (Unreal-FTransform); Tracker aus Mars über p_ue=100·A·p, R_ue=A·R·Aᵀ",
           "versatz": dict(zip(["x", "y", "z", "pitch", "yaw", "roll"], [vx, vy, vz, vp, vyaw, vr])),
           "root": dict(zip(["x", "y", "z", "yaw"], [rx, ry, rz, ryaw])), "einheiten": "cm, Grad, Unreal-Welt (X vorn, Y rechts, Z oben)",
           "bild_liste": bilder}
    aus = a.aus or os.path.splitext(a.csv)[0] + "_unreal_kamera.json"
    json.dump(out, open(aus, "w"), indent=1)
    print(f"{a.bilder} Bilder ab {a.clip_start} ({int(innen.sum())} mit Mars-Daten) → {aus}")
    print("erstes Bild:", bilder[0]["location"], bilder[0]["rotation"])


if __name__ == "__main__":
    main()
