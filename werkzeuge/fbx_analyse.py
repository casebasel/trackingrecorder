"""Path-Recorder-FBX auswerten: Bewegung, Sprünge, eingefrorene Posen, Basisstationen.
   python3 werkzeuge/fbx_analyse.py datei.fbx"""
import sys, math, os
sys.path.insert(0, os.path.dirname(__file__))
import fbx_lesen as F

def kurven(path):
    ver, top = F.read(path)
    objs = [n for n in top if n[0] == 'Objects'][0][2]
    conns = [n for n in top if n[0] == 'Connections'][0][2]
    byid = {n[1][0]: n for n in objs if n[1] and isinstance(n[1][0], int)}
    data = {}
    for c in conns:
        p = c[1]
        if p[0] == 'OP' and byid.get(p[1], ('',))[0] == 'AnimationCurveNode':
            tgt = byid[p[2]][1][1].split('\x00')[0].replace('Model_', '')
            for cc in conns:
                q = cc[1]
                if q[0] == 'OP' and q[2] == p[1]:
                    data[(tgt, p[3], q[3].split('|')[-1])] = [x for x in byid[q[1]][2] if x[0] == 'KeyValueFloat'][0][1][0]
    return data

def main(path):
    d = kurven(path)
    objekte = sorted({k[0] for k in d})
    ref = objekte[0]
    n = len(d[(ref, 'TCFrame', 'TCFrame')])
    tc = lambda i: '%02d:%02d:%02d:%02d' % tuple(int(d[(ref, 'TC' + k, 'TC' + k)][i]) for k in ('Hour', 'Minute', 'Second', 'Frame'))
    print(f"{os.path.basename(path)}: {n} Bilder, {tc(0)} – {tc(n-1)}")
    for o in objekte:
        P = [[d[(o, 'Lcl Translation', a)][i] for a in 'XYZ'] for i in range(n)]
        R = [[d[(o, 'Lcl Rotation', a)][i] for a in 'XYZ'] for i in range(n)]
        st = [math.dist(P[i], P[i + 1]) for i in range(n - 1)]
        rng = [max(p[k] for p in P) - min(p[k] for p in P) for k in range(3)]
        rr = [max(r[k] for r in R) - min(r[k] for r in R) for k in range(3)]
        gleich = sum(1 for i in range(1, n) if P[i] == P[i - 1] and R[i] == R[i - 1])
        lim = 3 if o.startswith('PORT') else 0.5
        spr = [(i, s) for i, s in enumerate(st) if s > lim]
        print(f"\n{o}: Spanne X/Y/Z {rng[0]:.1f}/{rng[1]:.1f}/{rng[2]:.1f} cm · Drehung {rr[0]:.2f}/{rr[1]:.2f}/{rr[2]:.2f}° · "
              f"Weg {sum(st):.1f} cm · eingefroren {gleich}/{n-1} · Sprünge >{lim} cm: {len(spr)}")
        for i, s in spr[:10]:
            print(f"   {tc(i)}→{tc(i+1)} {s:6.1f} cm")
        if len(spr) > 10: print(f"   … {len(spr)-10} weitere")

if __name__ == '__main__':
    for p in sys.argv[1:]: main(p)
