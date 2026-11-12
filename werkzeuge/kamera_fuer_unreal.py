"""Wandelt eine Kamera-JSON von unreal_kamera.py (oder die Nuke-Variante) ins Austauschformat der Session
Gaussian_to_Beeble: {frames: [{frame (TC-Bildnummer), tc, loc_cm, rot_deg{pitch,yaw,roll}, focal_mm}], filmback_mm}.
Winkel werden fortlaufend gemacht (keine ±180-Sprünge).
  werkzeuge/.venv/bin/python werkzeuge/kamera_fuer_unreal.py EIN.json AUS.json"""
import json, sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from tracking import tc_frames

ein, aus = sys.argv[1], sys.argv[2]
j = json.load(open(ein))
L = j["bild_liste"]
ang = {k: np.unwrap(np.radians([b["rotation"][k] for b in L])) for k in ("pitch", "yaw", "roll")}
frames = [{"frame": tc_frames(b["timecode"], j["fps"]), "tc": b["timecode"], "bild": b["bild"],
           "loc_cm": b["location"], "rot_deg": {k: round(float(np.degrees(ang[k][i])), 5) for k in ang},
           "focal_mm": j["brennweite_mm"]} for i, b in enumerate(L)]
out = {"quelle": j["quelle"], "variante": j["variante"], "fps": j["fps"], "clip_start_tc": j["clip_start_tc"],
       "filmback_mm": j["filmback_mm"], "bezug": "Unreal-Welt, cm, FRotator (X vorn, Z oben, linkshändig)",
       "kette": j.get("kette"), "zeitversatz_bilder": j.get("zeitversatz_bilder"), "anzahl": len(frames), "frames": frames}
json.dump(out, open(aus, "w"), indent=1)
print(f"{len(frames)} Bilder {frames[0]['tc']}–{frames[-1]['tc']} → {aus}")
