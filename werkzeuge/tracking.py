"""Gemeinsame Bausteine für Export und Vergleich (numpy/scipy, Umgebung werkzeuge/.venv).

Konventionen:
- Mars-Take (CSV des TrackingRecorders): Pose des Trackers in der Mars-Welt, Meter, Y oben, Quaternion x,y,z,w.
- Zeit: Timecode → absolute Bildnummer (hh*3600+mm*60+ss)*fps+ff, damit verschiedene Quellen über den TC zusammenpassen.
- .chan (Nuke): Zeilen „frame tx ty tz rx ry rz", Grad. Drehreihenfolge Nuke-Standard ZXY; hier als
  scipy-Euler extrinsisch „zxy" gelesen/geschrieben (R = Ry·Rx·Rz). Abweichend: --rotorder.
"""
from __future__ import annotations
import csv, json, math, os, sys
from dataclasses import dataclass
import numpy as np
from scipy.spatial.transform import Rotation as R, Slerp

sys.path.insert(0, os.path.dirname(__file__))


def tc_frames(tc: str, fps: float) -> int:
    h, m, s, f = (int(x) for x in tc.replace(";", ":").split(":"))
    return int(round(((h * 60 + m) * 60 + s) * round(fps) + f))


def frames_tc(n: int, fps: float) -> str:
    fr = int(round(fps))
    f = n % fr; s = n // fr
    return f"{s // 3600 % 24:02}:{s // 60 % 60:02}:{s % 60:02}:{f:02}"


@dataclass
class Spur:
    """Kamera- oder Trackerbahn: je Bild Zeit (Bildnummer, darf gebrochen sein), Position, Drehung."""
    t: np.ndarray        # (n,) Bildnummer
    p: np.ndarray        # (n,3)
    rot: R               # n Drehungen
    fps: float
    name: str = ""

    def bei(self, t: np.ndarray) -> "Spur":
        """Interpoliert auf andere Zeitpunkte (Position linear, Drehung Slerp); ausserhalb wird abgeschnitten."""
        t = np.clip(t, self.t[0], self.t[-1])
        p = np.column_stack([np.interp(t, self.t, self.p[:, k]) for k in range(3)])
        return Spur(t, p, Slerp(self.t, self.rot)(t), self.fps, self.name)


def lade_mars_csv(pfad: str, serial: str | None = None) -> Spur:
    """CSV des TrackingRecorders. Ohne serial: der Tracker, der sich am meisten bewegt (Kamera)."""
    rows: dict[str, list] = {}
    fps = 25.0
    with open(pfad, newline="") as f:
        for r in csv.DictReader(f):
            if r["kind"] != "tracker":
                continue
            fps = float(r["fps"])
            rows.setdefault(r["serial"], []).append(r)
    if not rows:
        raise SystemExit(f"{pfad}: keine Tracker-Zeilen")
    if serial is None:
        def weg(rs):
            p = np.array([[float(x["x_m"]), float(x["y_m"]), float(x["z_m"])] for x in rs])
            return np.abs(np.diff(p, axis=0)).sum()
        serial = max(rows, key=lambda k: weg(rows[k]))
    rs = rows[serial]
    # Bildnummer aus TC; mehrere Pakete im selben Bild (sollte nicht vorkommen) → Geräte-Zeit nehmen
    dev = np.array([float(x["device_ms"]) for x in rs])
    tc0 = tc_frames(rs[0]["timecode"], fps)
    t = tc0 + (dev - dev[0]) * fps / 1000.0
    p = np.array([[float(x["x_m"]), float(x["y_m"]), float(x["z_m"])] for x in rs])
    q = np.array([[float(x["qx"]), float(x["qy"]), float(x["qz"]), float(x["qw"])] for x in rs])
    keep = np.concatenate([[True], np.diff(t) > 1e-6])
    return Spur(t[keep], p[keep], R.from_quat(q[keep]), fps, serial)


def lade_chan(pfad: str, fps: float, start_tc: str | None, rotorder: str = "zxy") -> Spur:
    """Nuke .chan. Bildnummer der Datei + Start-TC (des Clips) = absolute Bildnummer."""
    a = np.loadtxt(pfad, ndmin=2)
    off = tc_frames(start_tc, fps) if start_tc else 0
    t = a[:, 0] + off
    rot = R.from_euler(rotorder, a[:, [4 + "xyz".index(c) for c in rotorder]], degrees=True)
    return Spur(t, a[:, 1:4].copy(), rot, fps, os.path.basename(pfad))


def schreibe_chan(pfad: str, s: Spur, start_tc: str | None = None, rotorder: str = "zxy") -> None:
    off = tc_frames(start_tc, s.fps) if start_tc else 0
    eul = s.rot.as_euler(rotorder, degrees=True)
    cols = {c: eul[:, i] for i, c in enumerate(rotorder)}
    with open(pfad, "w") as f:
        for i in range(len(s.t)):
            f.write(f"{s.t[i] - off:.3f} {s.p[i,0]:.5f} {s.p[i,1]:.5f} {s.p[i,2]:.5f} "
                    f"{cols['x'][i]:.5f} {cols['y'][i]:.5f} {cols['z'][i]:.5f}\n")


def _fbx_kurven_mit_zeit(pfad: str):
    """Wie fbx_analyse.kurven, aber mit Key-Zeiten: {(modell, eigenschaft, kanal): (zeiten_s, werte)}."""
    import fbx_lesen as F
    ver, top = F.read(pfad)
    objs = [n for n in top if n[0] == "Objects"][0][2]
    conns = [n for n in top if n[0] == "Connections"][0][2]
    byid = {n[1][0]: n for n in objs if n[1] and isinstance(n[1][0], int)}
    d = {}
    for c in conns:
        q = c[1]
        if q[0] == "OP" and byid.get(q[1], ("",))[0] == "AnimationCurveNode" and q[2] in byid:
            tgt = byid[q[2]][1][1].split("\x00")[0].replace("Model_", "")
            for cc in conns:
                r = cc[1]
                if r[0] == "OP" and r[2] == q[1] and r[1] in byid:
                    kids = {x[0]: x[1] for x in byid[r[1]][2]}
                    if "KeyTime" in kids and "KeyValueFloat" in kids:
                        d[(tgt, q[3], r[3].split("|")[-1])] = (np.array(kids["KeyTime"][0]) / 46186158000.0,
                                                               np.array(kids["KeyValueFloat"][0], float))
    return d


def lade_fbx(pfad: str, modell: str | None = None, start_tc: str | None = None, fps: float = 25.0) -> Spur:
    """FBX-Kamera. Zeit: Timecode je Key (TCHour…TCFrame, Path Recorder) oder Key-Zeit + Start-TC
    (--start-tc oder Begleitdatei <name>.json mit startTc, z. B. von der Stage). Positionen in Dateieinheiten,
    Drehung „Lcl Rotation" mit FBX-Standard XYZ (R = Rz·Ry·Rx)."""
    d = _fbx_kurven_mit_zeit(pfad)
    objs = sorted({k[0] for k in d if k[1] == "Lcl Translation"})
    if not objs:
        raise SystemExit(f"{pfad}: keine animierte Kamera gefunden")
    if modell is None:
        modell = max(objs, key=lambda o: sum(np.abs(np.diff(d[(o, "Lcl Translation", a)][1])).sum()
                                               for a in "XYZ" if (o, "Lcl Translation", a) in d))
    zeiten = d[(modell, "Lcl Translation", "X")][0]
    def kanal(eig, a):
        k = d.get((modell, eig, a))
        return np.zeros_like(zeiten) if k is None else np.interp(zeiten, k[0], k[1])
    p = np.column_stack([kanal("Lcl Translation", a) for a in "XYZ"])
    e = np.column_stack([kanal("Lcl Rotation", a) for a in "XYZ"])
    if (modell, "TCFrame", "TCFrame") in d:
        tc = [f"{int(kanal('TCHour','TCHour')[i]):02}:{int(kanal('TCMinute','TCMinute')[i]):02}:"
              f"{int(kanal('TCSecond','TCSecond')[i]):02}:{int(kanal('TCFrame','TCFrame')[i]):02}" for i in range(len(p))]
        t = np.array([tc_frames(x, fps) for x in tc], float)
    else:
        neben = os.path.splitext(pfad)[0] + ".json"
        if start_tc is None and os.path.exists(neben):
            j = json.load(open(neben)); start_tc = j.get("startTc"); fps = float(j.get("fps") or fps)
        t = zeiten * fps + (tc_frames(start_tc, fps) if start_tc else 0)
    return Spur(t, p, R.from_euler("xyz", e, degrees=True), fps, modell)


def lade_posen_jsonl(pfad: str) -> Spur:
    """Posen-Sicherung der Stage (data/posen/<takeId>.jsonl): rohe Live-Link-Pose von Rover 1, cm, pitch/yaw/roll,
    10 Hz, Feld marsTc. Unreal-FRotator: R = Rz(yaw)·Ry(pitch)·Rx(roll)."""
    t, p, e = [], [], []
    fps = 25.0
    for line in open(pfad):
        j = json.loads(line)
        if j.get("art") == "kopf":
            fps = float(j.get("fps") or fps); continue
        k = j.get("kamera") or {}
        if not k.get("gueltig") or not j.get("marsTc"):
            continue
        po = k["pose"]
        t.append(tc_frames(j["marsTc"], fps)); p.append([po["x"], po["y"], po["z"]])
        e.append([po["roll"], po["pitch"], po["yaw"]])
    t = np.array(t, float)
    return Spur(t, np.array(p), R.from_euler("xyz", np.array(e), degrees=True), fps, os.path.basename(pfad))


def lade(pfad: str, **kw) -> Spur:
    ext = pfad.lower()
    if ext.endswith(".csv"):
        return lade_mars_csv(pfad, kw.get("serial"))
    if ext.endswith(".chan"):
        return lade_chan(pfad, kw.get("fps", 25.0), kw.get("start_tc"), kw.get("rotorder", "zxy"))
    if ext.endswith(".fbx"):
        return lade_fbx(pfad, kw.get("modell"), kw.get("start_tc"), kw.get("fps", 25.0))
    if ext.endswith(".jsonl"):
        return lade_posen_jsonl(pfad)
    raise SystemExit(f"unbekanntes Format: {pfad}")


@dataclass
class Umrechnung:
    """Referenz = s·R_W·(Mars-Pose · Versatz X) + t_W, Drehung R_ref = R_W·R_mars·R_X; Spiegeln vorab (Händigkeit)."""
    s: float
    rw: np.ndarray       # Rotvec Welt
    tw: np.ndarray
    rx: np.ndarray       # Rotvec Versatz Tracker→Kamera
    tx: np.ndarray       # Versatz in Mars-Einheiten (m)
    spiegel: bool = False
    dt: float = 0.0      # Bilder: Referenz(t) ≈ Mars(t + dt)

    SPIEGEL = np.diag([1.0, 1.0, -1.0])

    def anwenden(self, m: Spur) -> Spur:
        p, rot = m.p, m.rot
        if self.spiegel:
            M = self.SPIEGEL
            p = p @ M.T
            rot = R.from_matrix(M @ rot.as_matrix() @ M)
        RW, RX = R.from_rotvec(self.rw), R.from_rotvec(self.rx)
        cam_p = p + rot.apply(self.tx)
        return Spur(m.t - self.dt, self.s * RW.apply(cam_p) + self.tw, RW * rot * RX, m.fps, m.name)

    def json(self) -> dict:
        RW, RX = R.from_rotvec(self.rw), R.from_rotvec(self.rx)
        return {"massstab": self.s, "spiegeln_z": self.spiegel, "zeitversatz_bilder": self.dt,
                "welt": {"rotation_matrix": RW.as_matrix().round(6).tolist(), "translation": self.tw.round(4).tolist()},
                "versatz": {"rotation_matrix": RX.as_matrix().round(6).tolist(),
                            "euler_xyz_grad": RX.as_euler("xyz", degrees=True).round(3).tolist(),
                            "translation_m": self.tx.round(5).tolist()}}

    @staticmethod
    def von_json(j: dict) -> "Umrechnung":
        return Umrechnung(j["massstab"], R.from_matrix(j["welt"]["rotation_matrix"]).as_rotvec(),
                          np.array(j["welt"]["translation"]), R.from_matrix(j["versatz"]["rotation_matrix"]).as_rotvec(),
                          np.array(j["versatz"]["translation_m"]), j.get("spiegeln_z", False), j.get("zeitversatz_bilder", 0.0))
