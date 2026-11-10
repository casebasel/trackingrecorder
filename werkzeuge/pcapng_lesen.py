import struct
def pakete(path):
    """Minimaler pcapng-Leser: liefert (zeit_s, src, sport, dst, dport, payload) für IPv4/UDP, entdoppelt (pktmon erfasst je Komponente)."""
    d=open(path,'rb').read(); o=0; seen=set(); out=[]; tsres={}
    while o+12<=len(d):
        bt,bl=struct.unpack_from('<II',d,o)
        if bt==6:
            iface,th,tl,cl,ol=struct.unpack_from('<IIIII',d,o+8)
            pkt=d[o+28:o+28+cl]; t=((th<<32)|tl)/1e7 if False else ((th<<32)|tl)
            if len(pkt)>=42 and pkt[12:14]==b'\x08\x00' and pkt[23]==17:
                ihl=(pkt[14]&15)*4; ip=pkt[14:]; u=ip[ihl:]
                src='.'.join(map(str,ip[12:16])); dst='.'.join(map(str,ip[16:20]))
                sp,dp,ln=struct.unpack_from('>HHH',u,0); pl=u[8:ln]
                key=(ip[4:6],src,dst,sp,dp,pl)
                if key not in seen:
                    seen.add(key); out.append((t,src,sp,dst,dp,pl))
        o+=bl
    t0=out[0][0]; return [((t-t0)/1e6,)+x[1:] for x in out]  # pktmon: Zeit in µs? relativ
