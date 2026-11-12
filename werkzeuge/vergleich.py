"""Vergleich einer Mars-Aufnahme (TrackingRecorder) mit einer Referenzkamera (Nuke-Solve, Unreal, Posen-Sicherung).

Schätzt die feste Umrechnung Referenz ≈ Welt · (Mars · Versatz) inklusive Massstab, Händigkeit und Zeitversatz,
und meldet, was danach übrig bleibt: Fehler in Position, Drehung und — wichtig für Greenscreen — in Pixeln.

  werkzeuge/.venv/bin/python werkzeuge/vergleich.py MARS.csv REFERENZ.(chan|fbx|jsonl) [--start-tc 15:02:11:04]
        [--serial LHR-0FFCBEE6] [--hfov 37.4] [--breite 3840] [--entfernung 300] [--aus bericht]

--start-tc: TC des ersten Bildes der .chan (Clip-Start), sonst gelten die Bildnummern der Datei als absolut.
--hfov/--breite: horizontaler Bildwinkel in Grad und Bildbreite in Pixeln (35 mm auf 23,76 mm Filmback ≈ 37,4°).
--entfernung: typischer Abstand zum Hintergrund in cm (für den Anteil der Positionsfehler am Pixelfehler).
Ausgabe: <aus>.json (Umrechnung + Kennzahlen), <aus>.csv (Fehler je Bild), <aus>.png (Verlauf).
"""
from __future__ import annotations
import argparse, itertools, json, math, os, sys
import numpy as np
from scipy.optimize import least_squares
from scipy.spatial.transform import Rotation as R

sys.path.insert(0, os.path.dirname(__file__))
from tracking import Spur, Umrechnung, lade, frames_tc


def kandidaten_drehungen():
    """24 achsparallele Drehungen als Startwerte (Achsen vertauscht/umgedreht, det +1)."""
    out = []
    for perm in itertools.permutations(range(3)):
        for signs in itertools.product([1, -1], repeat=3):
            M = np.zeros((3, 3))
            for i, (j, s) in enumerate(zip(perm, signs)):
                M[i, j] = s
            if np.linalg.det(M) > 0:
                out.append(R.from_matrix(M).as_rotvec())
    return out


def residuen(x, m: Spur, ref: Spur, spiegel: bool, w_rot: float):
    u = Umrechnung(math.exp(x[0]), x[1:4], x[4:7], x[7:10], x[10:13], spiegel, 0.0)
    pr = u.anwenden(m)
    dp = (pr.p - ref.p).ravel()
    dr = (ref.rot.inv() * pr.rot).as_rotvec().ravel() * w_rot
    return np.concatenate([dp, dr])


def anpassen(mars: Spur, ref: Spur, dt: float, spiegel: bool, start=None):
    """Least-Squares für eine feste Zeitverschiebung. Gibt (Kosten, Umrechnung) zurück."""
    m = mars.bei(ref.t + dt)
    m.t = ref.t
    # Gewicht Drehung: 1° soll so viel zählen wie die Positionsstreuung der Referenz / 100 (≈ 1 % der Bahn)
    spread = max(np.ptp(ref.p, axis=0).max(), 1e-6)
    w_rot = spread / 100.0 / math.radians(1.0)
    best = None
    starts = [start] if start is not None else kandidaten_drehungen()
    for rw0 in starts:
        if start is not None:
            x0 = start
        else:
            s0 = np.ptp(ref.p, axis=0).max() / max(np.ptp(m.p, axis=0).max(), 1e-9)
            x0 = np.concatenate([[math.log(max(s0, 1e-6))], rw0, ref.p.mean(0) - s0 * R.from_rotvec(rw0).apply(m.p.mean(0)),
                                 np.zeros(3), np.zeros(3)])
        r = least_squares(residuen, x0, args=(m, ref, spiegel, w_rot), method="lm", max_nfev=4000)
        if best is None or r.cost < best.cost:
            best = r
    x = best.x
    return best.cost, Umrechnung(math.exp(x[0]), x[1:4], x[4:7], x[7:10], x[10:13], spiegel, dt), best.x


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("mars"); ap.add_argument("referenz")
    ap.add_argument("--serial"); ap.add_argument("--start-tc"); ap.add_argument("--rotorder", default="zxy")
    ap.add_argument("--fps", type=float, default=25.0)
    ap.add_argument("--hfov", type=float, default=2 * math.degrees(math.atan(23.76 / 2 / 35)))
    ap.add_argument("--breite", type=int, default=3840)
    ap.add_argument("--entfernung", type=float, default=300.0, help="cm zum Hintergrund")
    ap.add_argument("--max-versatz", type=float, default=10.0, help="Suche Zeitversatz ± Bilder")
    ap.add_argument("--aus", default=None)
    a = ap.parse_args()

    mars = lade(a.mars, serial=a.serial)
    ref = lade(a.referenz, fps=a.fps, start_tc=a.start_tc, rotorder=a.rotorder)
    # gemeinsamer Zeitraum
    lo, hi = max(mars.t[0], ref.t[0]) + a.max_versatz, min(mars.t[-1], ref.t[-1]) - a.max_versatz
    keep = (ref.t >= lo) & (ref.t <= hi)
    if keep.sum() < 20:
        raise SystemExit(f"Zu wenig gemeinsame Bilder (Mars {frames_tc(int(mars.t[0]), mars.fps)}–{frames_tc(int(mars.t[-1]), mars.fps)}, "
                         f"Referenz {frames_tc(int(ref.t[0]), ref.fps)}–{frames_tc(int(ref.t[-1]), ref.fps)}). Start-TC richtig?")
    ref = Spur(ref.t[keep], ref.p[keep], ref.rot[keep], ref.fps, ref.name)

    # 1) Händigkeit und Grundlage bei dt = 0
    res = [anpassen(mars, ref, 0.0, sp) for sp in (False, True)]
    cost, u, x = min(res, key=lambda r: r[0])
    # 2) Zeitversatz: grob in ganzen Bildern, dann fein
    for schritt, spanne in ((1.0, a.max_versatz), (0.1, 1.0)):
        mitte = u.dt
        for dt in np.arange(mitte - spanne, mitte + spanne + 1e-9, schritt):
            c, uu, xx = anpassen(mars, ref, float(dt), u.spiegel, start=x)
            if c < cost:
                cost, u, x = c, uu, xx

    pred = u.anwenden(mars.bei(ref.t + u.dt)); pred.t = ref.t
    dpos = np.linalg.norm(pred.p - ref.p, axis=1)                       # Referenz-Einheiten
    drot = np.degrees(np.linalg.norm((ref.rot.inv() * pred.rot).as_rotvec(), axis=1))
    px_pro_grad = a.breite / a.hfov
    einheit_cm = 1.0 if np.ptp(ref.p) > 5 else 100.0                    # m-Referenz → cm
    px_dreh = drot * px_pro_grad
    px_pos = np.degrees(np.arctan2(dpos * einheit_cm, a.entfernung)) * px_pro_grad
    px = px_dreh + px_pos

    def kz(v):
        return {"mittel": float(np.mean(v)), "rms": float(np.sqrt(np.mean(v ** 2))), "p95": float(np.percentile(v, 95)), "max": float(np.max(v))}

    bericht = {
        "mars": os.path.basename(a.mars), "mars_tracker": mars.name, "referenz": os.path.basename(a.referenz),
        "bilder": int(len(ref.t)), "von": frames_tc(int(ref.t[0]), ref.fps), "bis": frames_tc(int(ref.t[-1]), ref.fps),
        "umrechnung": u.json(),
        "zeitversatz_ms": u.dt * 1000.0 / ref.fps,
        "fehler_position_ref_einheiten": kz(dpos), "fehler_drehung_grad": kz(drot), "fehler_pixel_geschaetzt": kz(px),
        "annahmen": {"hfov_grad": a.hfov, "breite_px": a.breite, "entfernung_cm": a.entfernung},
    }
    aus = a.aus or os.path.splitext(a.mars)[0] + "_vergleich"
    with open(aus + ".json", "w") as f:
        json.dump(bericht, f, indent=2, ensure_ascii=False)
    with open(aus + ".csv", "w") as f:
        f.write("timecode,fehler_pos,fehler_grad,fehler_px\n")
        for i in range(len(ref.t)):
            f.write(f"{frames_tc(int(round(ref.t[i])), ref.fps)},{dpos[i]:.4f},{drot[i]:.4f},{px[i]:.2f}\n")
    try:
        import matplotlib; matplotlib.use("Agg"); import matplotlib.pyplot as plt
        fig, ax = plt.subplots(3, 1, figsize=(10, 7), sharex=True)
        x_s = (ref.t - ref.t[0]) / ref.fps
        for k, (v, lab) in enumerate(((dpos * einheit_cm * 10, "Position (mm)"), (drot, "Drehung (°)"), (px, "geschätzt (px)"))):
            ax[k].plot(x_s, v, lw=0.8, color="#2867e4"); ax[k].set_ylabel(lab); ax[k].grid(alpha=.3)
        ax[2].set_xlabel("Sekunden ab " + bericht["von"])
        fig.suptitle(f"Mars gegen {bericht['referenz']} · Zeitversatz {bericht['zeitversatz_ms']:.0f} ms")
        fig.tight_layout(); fig.savefig(aus + ".png", dpi=120)
    except Exception as e:
        print("Grafik nicht erstellt:", e)

    fp, fr, fpx = bericht["fehler_position_ref_einheiten"], bericht["fehler_drehung_grad"], bericht["fehler_pixel_geschaetzt"]
    print(f"{bericht['bilder']} Bilder {bericht['von']}–{bericht['bis']} · Massstab {u.s:.4f} · gespiegelt {u.spiegel} · "
          f"Zeitversatz {u.dt:+.1f} Bilder ({bericht['zeitversatz_ms']:+.0f} ms)")
    print(f"Position RMS {fp['rms']:.3f} (p95 {fp['p95']:.3f}) Ref-Einheiten · Drehung RMS {fr['rms']:.3f}° (p95 {fr['p95']:.3f}°) · "
          f"Pixel RMS {fpx['rms']:.1f} (p95 {fpx['p95']:.1f}, max {fpx['max']:.1f})")
    print("Versatz Tracker→Kamera:", u.json()["versatz"])
    print("Bericht:", aus + ".json")


if __name__ == "__main__":
    main()
