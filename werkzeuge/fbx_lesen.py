import struct, zlib, sys
def read(path):
    d=open(path,'rb').read(); ver=struct.unpack_from('<I',d,23)[0]; big=ver>=7500
    def prop(o):
        t=chr(d[o]); o+=1
        if t in 'YCIFDL':
            fmt={'Y':'<h','C':'<?','I':'<i','F':'<f','D':'<d','L':'<q'}[t]; v=struct.unpack_from(fmt,d,o)[0]; return v,o+struct.calcsize(fmt)
        if t in 'SR':
            n=struct.unpack_from('<I',d,o)[0]; o+=4; v=d[o:o+n]; return (v.decode('utf8','replace') if t=='S' else v),o+n
        if t in 'fdlib':
            n,enc,cl=struct.unpack_from('<III',d,o); o+=12; raw=d[o:o+cl]; o+=cl
            if enc: raw=zlib.decompress(raw)
            fmt={'f':'f','d':'d','l':'q','i':'i','b':'?'}[t]; return list(struct.unpack('<%d%s'%(n,fmt),raw)),o
        raise ValueError(t)
    def node(o):
        if big: end,np,pl=struct.unpack_from('<QQQ',d,o); o+=24
        else: end,np,pl=struct.unpack_from('<III',d,o); o+=12
        nl=d[o]; o+=1
        if end==0: return None,o
        name=d[o:o+nl].decode(); o+=nl
        props=[]
        for _ in range(np): v,o=prop(o); props.append(v)
        kids=[]
        while o<end:
            n,o2=node(o)
            if n is None: o=o2; break
            kids.append(n); o=o2
        return (name,props,kids),end
    o=27; top=[]
    while o<len(d)-200:
        n,o=node(o)
        if n is None: break
        top.append(n)
    return ver,top
def show(n,depth=0,maxd=9,maxk=40):
    name,props,kids=n
    ps=[]
    for p in props:
        if isinstance(p,list): ps.append(f"[{len(p)}x {p[:4]}…]")
        elif isinstance(p,bytes): ps.append(f"<{len(p)}B>")
        else: ps.append(repr(p)[:80])
    print('  '*depth+name+': '+', '.join(ps))
    if depth<maxd:
        for k in kids[:maxk]: show(k,depth+1,maxd,maxk)
        if len(kids)>maxk: print('  '*(depth+1)+f'… {len(kids)-maxk} weitere')
if __name__=='__main__':
    ver,top=read(sys.argv[1]); print('FBX-Version',ver)
    for n in top:
        if n[0] in sys.argv[2:] or len(sys.argv)==2: show(n)
