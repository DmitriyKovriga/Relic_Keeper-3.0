"""Renders a passive tree to PNG the way the game draws it.

Same-orbit neighbours: short clockwise arc (chord if the sweep is >= 170 deg).
Pairs listed in BezierConnections: cubic anchor + handle offsets. Everything else: straight line.

    python render.py <tree.asset> <out.png> [--label]
"""
import math
import sys

from PIL import Image, ImageDraw

from ptree import parse

NODE_RADIUS = {0: 14, 1: 20, 2: 26, 3: 20}
LINE = (230, 190, 60)
FILL = {0: (215, 215, 215), 1: (40, 80, 90), 2: (120, 70, 20), 3: (50, 100, 50)}


def _cubic(p0, p1, p2, p3, t):
    u = 1 - t
    return tuple(u*u*u*p0[i] + 3*u*u*t*p1[i] + 3*u*t*t*p2[i] + t*t*t*p3[i] for i in range(2))


def short_sweep(a, b):
    s = (b - a) % 360
    if s > 180:
        a, s = b, (a - b) % 360
    return a, s


def edge_polylines(nodes, clusters, beziers, samples=24):
    """Yields (idA, idB, kind, [points]) for every connection, kind in line/arc/chord/bezier."""
    byid = {n['ID']: n for n in nodes}
    cl = {c['ID']: c for c in clusters}
    bz = {tuple(sorted((b['NodeIdA'], b['NodeIdB']))): b for b in beziers}
    done = set()
    for n in nodes:
        for cid in n['ConnectionIDs']:
            key = tuple(sorted((n['ID'], cid)))
            if key in done or cid not in byid:
                continue
            done.add(key)
            m = byid[cid]
            if key in bz:
                B = bz[key]
                pa, pb = byid[B['NodeIdA']]['W'], byid[B['NodeIdB']]['W']
                t = B['AnchorPercent'] / 100
                an = (pa[0] + (pb[0] - pa[0]) * t, pa[1] + (pb[1] - pa[1]) * t)
                c1 = (an[0] + B['InHandleOffset'][0], an[1] + B['InHandleOffset'][1])
                c2 = (an[0] + B['OutHandleOffset'][0], an[1] + B['OutHandleOffset'][1])
                yield key[0], key[1], 'bezier', [_cubic(pa, c1, c2, pb, i / samples) for i in range(samples + 1)]
                continue
            if (n['PlacementMode'] == 1 and m['PlacementMode'] == 1 and n['ClusterID'] == m['ClusterID']
                    and n['OrbitIndex'] == m['OrbitIndex'] and n['ClusterID'] in cl):
                s0, sw = short_sweep(n['OrbitAngle'], m['OrbitAngle'])
                if sw < 170:
                    c = cl[n['ClusterID']]
                    r = c['Orbits'][n['OrbitIndex']]
                    pts = [(c['Center'][0] + math.cos(math.radians(s0 + sw * i / samples)) * r,
                            c['Center'][1] + math.sin(math.radians(s0 + sw * i / samples)) * r)
                           for i in range(samples + 1)]
                    yield key[0], key[1], 'arc', pts
                    continue
                yield key[0], key[1], 'chord', [n['W'], m['W']]
                continue
            yield key[0], key[1], 'line', [n['W'], m['W']]


def render_model(nodes, clusters, beziers, out, label=False, scale=0.8):
    xs = [n['W'][0] for n in nodes]; ys = [n['W'][1] for n in nodes]
    pad = 80
    ox, oy = min(xs) - pad, min(ys) - pad
    W, H = int((max(xs) - ox + pad) * scale), int((max(ys) - oy + pad) * scale)
    img = Image.new('RGB', (W, H), (58, 58, 58))
    d = ImageDraw.Draw(img)
    T = lambda p: ((p[0] - ox) * scale, (p[1] - oy) * scale)
    for c in clusters:
        for r in c['Orbits']:
            cx, cy = T(c['Center'])
            d.ellipse([cx - r * scale, cy - r * scale, cx + r * scale, cy + r * scale], outline=(110, 100, 140))
    for _, _, kind, pts in edge_polylines(nodes, clusters, beziers):
        d.line([T(p) for p in pts], fill=(255, 90, 90) if kind == 'chord' else LINE, width=max(1, int(3 * scale)))
    for n in nodes:
        x, y = T(n['W']); r = NODE_RADIUS[n['NodeType']] * scale
        d.ellipse([x - r, y - r, x + r, y + r], fill=FILL[n['NodeType']], outline=(255, 255, 255))
        if label and n.get('Label'):
            d.text((x + r, y - r), n['Label'], fill=(255, 120, 120))
    img.save(out)


if __name__ == '__main__':
    render_model(*parse(sys.argv[1]), sys.argv[2], label='--label' in sys.argv)
