"""Geometry toolkit for Relic Keeper passive tree layouts.

A layout script defines ONE sector in a local frame and the toolkit repeats it by symmetry.
See ../SKILL.md for the workflow and ../reference.md for the API and design vocabulary.
"""
import math
import os
import uuid

from ptree import parse
from render import NODE_RADIUS, edge_polylines, render_model

SMALL, NOTABLE, KEY, START = 0, 1, 2, 3
START_POS = (1800.0, 1400.0)
SCRIPT_GUID = '5d371fe250f0d624db51c662da4fdb36'  # PassiveSkillTreeSO.cs

# Canonical skeleton, relative to the start node, screen axes (y grows down).
SPOKE = [(120, 120), (220, 220), (360, 360)]      # three nodes per diagonal spoke, last one is the corner
CARDINAL = 500                                    # cardinal corners (0, -500), (500, 0), ...
KEY_CARDINAL = 560                                # keystone outside each cardinal corner
KEY_DIAGONAL = (400, 400)                         # keystone outside each diagonal corner

SYMMETRY = {
    'C4': [(k, False) for k in range(4)],                          # pinwheel: layout covers a whole quadrant
    'D4': [(k, m) for k in range(4) for m in (False, True)],        # mirror + rotate: layout covers x >= 0 of a quadrant
    'NONE': [(0, False)],                                           # layout builds everything itself (use q.frame())
}


# ---------------------------------------------------------------- vector math
def add(p, q): return (p[0] + q[0], p[1] + q[1])
def sub(p, q): return (p[0] - q[0], p[1] - q[1])
def mul(p, s): return (p[0] * s, p[1] * s)
def lerp(p, q, t): return (p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t)
def length(p): return math.hypot(p[0], p[1])
def norm(p):
    L = length(p)
    return (p[0] / L, p[1] / L)
def polar(c, r, a):
    return (c[0] + math.cos(math.radians(a)) * r, c[1] + math.sin(math.radians(a)) * r)
def angle_to(c, p):
    return math.degrees(math.atan2(p[1] - c[1], p[0] - c[0])) % 360
def rot90(p, k):
    x, y = p
    for _ in range(k % 4):
        x, y = -y, x
    return (x, y)


def cubic(P, t):
    p0, p1, p2, p3 = P
    u = 1 - t
    return tuple(u*u*u*p0[i] + 3*u*u*t*p1[i] + 3*u*t*t*p2[i] + t*t*t*p3[i] for i in range(2))


def split(P, t0, t1):
    """Control points of the piece of cubic P between t0 and t1 (de Casteljau)."""
    def cut(P, t):
        p0, p1, p2, p3 = P
        a, b, c = lerp(p0, p1, t), lerp(p1, p2, t), lerp(p2, p3, t)
        d, e = lerp(a, b, t), lerp(b, c, t)
        f = lerp(d, e, t)
        return (p0, a, d, f), (f, e, c, p3)
    _, right = cut(P, t0)
    if t1 >= 1:
        return right
    left, _ = cut(right, (t1 - t0) / (1 - t0))
    return left


# ---------------------------------------------------------------- tree
class Tree:
    def __init__(self, class_id, target=None):
        self.class_id = class_id
        self.ns = uuid.uuid5(uuid.NAMESPACE_URL, 'relic-keeper/passive-tree/' + class_id)
        self.nodes, self.byid, self.clusters, self.beziers = [], {}, [], []
        self.existing, self.notes = {}, []
        if target and os.path.exists(target):
            for n in parse(target)[0]:
                self.existing[n['ID']] = n
        self.skeleton_ids = set()
        self.skeleton_edges = set()
        self._build_skeleton()

    def gid(self, name):
        return str(uuid.uuid5(self.ns, name))

    def _reuse_id(self, name, world, ntype):
        """Skeleton nodes keep the IDs already in the target asset (matched by position and type)."""
        for n in self.existing.values():
            if n['NodeType'] == ntype and length(sub(n['W'], world)) < 3:
                return n['ID']
        return self.gid(name)

    def add_node(self, nid, ntype, world, label, **orbit):
        n = dict(ID=nid, NodeType=ntype, PlacementMode=0, Position=world, W=world, ClusterID='',
                 OrbitIndex=0, OrbitAngle=0.0, ConnectionIDs=[], Label=label)
        n.update(orbit)
        assert nid not in self.byid, 'duplicate node id ' + label
        self.nodes.append(n); self.byid[nid] = n
        return nid

    def link(self, a, b):
        assert a != b
        A, B = self.byid[a], self.byid[b]
        if b not in A['ConnectionIDs']: A['ConnectionIDs'].append(b)
        if a not in B['ConnectionIDs']: B['ConnectionIDs'].append(a)

    def cluster(self, cid):
        return next(c for c in self.clusters if c['ID'] == cid)

    def at(self, rel):
        w = add(START_POS, rel)
        for n in self.nodes:
            if abs(n['W'][0] - w[0]) < 3 and abs(n['W'][1] - w[1]) < 3:
                return n['ID']
        raise KeyError('no node at %s' % (rel,))

    def _build_skeleton(self):
        def node(name, ntype, rel):
            w = add(START_POS, rel)
            nid = self._reuse_id(name, w, ntype)
            self.add_node(nid, ntype, w, name)
            self.skeleton_ids.add(nid)
            return nid

        def edge(a, b):
            self.link(a, b)
            self.skeleton_edges.add(tuple(sorted((a, b))))

        start = node('skel/start', START, (0, 0))
        corners = []
        for k in range(4):
            prev = start
            for i, p in enumerate(SPOKE):
                cur = node('skel/spoke%d/%d' % (k, i), SMALL, rot90((-p[0], -p[1]), k))
                edge(prev, cur); prev = cur
            corners.append(prev)
            corners.append(node('skel/corner%d' % k, SMALL, rot90((0, -CARDINAL), k)))
        for i in range(8):
            edge(corners[i], corners[(i + 1) % 8])
        for k in range(4):
            kd = node('skel/keyDiag%d' % k, KEY, rot90((-KEY_DIAGONAL[0], -KEY_DIAGONAL[1]), k))
            edge(corners[2 * k], kd)
            kc = node('skel/keyCard%d' % k, KEY, rot90((0, -KEY_CARDINAL), k))
            edge(corners[2 * k + 1], kc)

    # ------------------------------------------------------------ output
    def render(self, out, label=False):
        render_model(self.nodes, self.clusters, self.beziers, out, label=label)

    def write(self, path, asset_name):
        """Writes the asset. Template/UniqueModifiers of nodes with an unchanged ID are preserved."""
        def f(v):
            s = ('%.4f' % v).rstrip('0').rstrip('.')
            return '0' if s in ('', '-0') else s
        out = ['%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n',
               '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n',
               '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n',
               '  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: %s\n' % (SCRIPT_GUID, asset_name),
               '  m_EditorClassIdentifier: Assembly-CSharp::Scripts.Skills.PassiveTree.PassiveSkillTreeSO\n  Nodes:\n']
        for n in self.nodes:
            p = n['W']
            out.append('  - ID: %s\n    NodeType: %d\n    PlacementMode: %d\n' % (n['ID'], n['NodeType'], n['PlacementMode']))
            out.append('    Position: {x: %s, y: %s}\n' % (f(p[0]), f(p[1])))
            out.append('    ClusterID: %s\n    OrbitIndex: %d\n    OrbitAngle: %s\n' % (n['ClusterID'] or '', n['OrbitIndex'], f(n['OrbitAngle'])))
            old = self.existing.get(n['ID'])
            if old and old['Raw']:
                out.extend(line + '\n' for line in old['Raw'])
            else:
                out.append('    Template: {fileID: 0}\n    UniqueModifiers: []\n    UniqueStatScalingRules: []\n')
            out.append('    ConnectionIDs:\n')
            out.extend('    - %s\n' % c for c in n['ConnectionIDs'])
        out.append('  Clusters:\n')
        for c in self.clusters:
            r, g, b = c['Color']
            out.append('  - ID: %s\n    Name: %s\n    Center: {x: %s, y: %s}\n    Orbits:\n' % (c['ID'], c['Name'], f(c['Center'][0]), f(c['Center'][1])))
            for rad in c['Orbits']:
                out.append('    - Radius: %s\n      IsPartialArc: 0\n      ArcStartAngle: 0\n      ArcEndAngle: 360\n' % f(rad))
            out.append('    EditorColor: {r: %s, g: %s, b: %s, a: 0.4}\n    RoadConnections: []\n' % (f(r), f(g), f(b)))
        out.append('  BezierConnections:\n')
        for b in self.beziers:
            out.append('  - NodeIdA: %s\n    NodeIdB: %s\n    AnchorPercent: %s\n' % (b['NodeIdA'], b['NodeIdB'], f(b['AnchorPercent'])))
            out.append('    InHandleOffset: {x: %s, y: %s}\n' % (f(b['InHandleOffset'][0]), f(b['InHandleOffset'][1])))
            out.append('    OutHandleOffset: {x: %s, y: %s}\n    MirrorHandles: 0\n' % (f(b['OutHandleOffset'][0]), f(b['OutHandleOffset'][1])))
        out.append('  GridSize: 20\n  SnapToGrid: 1\n')
        os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
        open(path, 'w', encoding='utf-8', newline='\n').write(''.join(out))
        lost = [i for i, n in self.existing.items() if i not in self.byid and n['Raw'] and 'fileID: 0}' not in n['Raw'][0]]
        if lost:
            print('WARNING: %d nodes with assigned templates were dropped: %s' % (len(lost), lost[:5]))

    # ------------------------------------------------------------ validation
    def validate(self, targets=None):
        """Returns a list of problems (empty = OK); non-blocking remarks go to self.notes."""
        problems, self.notes = [], []
        start = next(n['ID'] for n in self.nodes if n['NodeType'] == START)
        seen, stack = set(), [start]
        while stack:
            i = stack.pop()
            if i in seen: continue
            seen.add(i)
            for c in self.byid[i]['ConnectionIDs']:
                if i not in self.byid[c]['ConnectionIDs']:
                    problems.append('asymmetric link %s -> %s' % (self.byid[i]['Label'], self.byid[c]['Label']))
                stack.append(c)
        for n in self.nodes:
            if n['ID'] not in seen:
                problems.append('unreachable: ' + n['Label'])
        for a, b in self.skeleton_edges:
            if b not in self.byid[a]['ConnectionIDs']:
                problems.append('skeleton edge removed: %s - %s' % (self.byid[a]['Label'], self.byid[b]['Label']))
        for i in self.skeleton_ids:
            if len(self.byid[i]['ConnectionIDs']) > 6:
                problems.append('too many links (%d) on %s' % (len(self.byid[i]['ConnectionIDs']), self.byid[i]['Label']))
        # node spacing
        R = lambda n: NODE_RADIUS[n['NodeType']]
        for i, a in enumerate(self.nodes):
            for b in self.nodes[i + 1:]:
                d = length(sub(a['W'], b['W']))
                if d < R(a) + R(b) + 10:
                    problems.append('nodes overlap (%.0f): %s / %s' % (d, a['Label'], b['Label']))
        # lines passing through foreign nodes, chords, crossings
        polys = list(edge_polylines(self.nodes, self.clusters, self.beziers))
        for ia, ib, kind, pts in polys:
            if kind == 'chord':
                self.notes.append('orbit sweep >= 170 deg is drawn as a straight chord (fine if intended): %s - %s'
                                  % (self.byid[ia]['Label'], self.byid[ib]['Label']))
            for n in self.nodes:
                if n['ID'] in (ia, ib): continue
                if _dist_poly(n['W'], pts) < R(n) + 4:
                    problems.append('line %s - %s passes through %s' % (self.byid[ia]['Label'], self.byid[ib]['Label'], n['Label']))
        for x in range(len(polys)):
            for y in range(x + 1, len(polys)):
                A, B = polys[x], polys[y]
                if {A[0], A[1]} & {B[0], B[1]}: continue
                if _polys_cross(A[3], B[3]):
                    problems.append('lines cross: %s-%s x %s-%s' % tuple(self.byid[i]['Label'] for i in (A[0], A[1], B[0], B[1])))
        return problems

    def report(self, targets=(154, 113, 32, 8)):
        from collections import Counter
        c = Counter(n['NodeType'] for n in self.nodes)
        print('nodes %d (small %d, notable %d, keystone %d) clusters %d beziers %d | warrior: %d (%d/%d/%d)' % (
            len(self.nodes), c[SMALL], c[NOTABLE], c[KEY], len(self.clusters), len(self.beziers), *targets))


def _dist_seg(p, a, b):
    ab = sub(b, a); L = ab[0] ** 2 + ab[1] ** 2
    if L < 1e-6: return length(sub(p, a))
    t = max(0, min(1, ((p[0] - a[0]) * ab[0] + (p[1] - a[1]) * ab[1]) / L))
    return length(sub(p, add(a, mul(ab, t))))


def _dist_poly(p, pts):
    return min(_dist_seg(p, pts[i], pts[i + 1]) for i in range(len(pts) - 1))


def _seg_cross(p1, p2, p3, p4):
    def o(a, b, c): return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    d1, d2, d3, d4 = o(p3, p4, p1), o(p3, p4, p2), o(p1, p2, p3), o(p1, p2, p4)
    return d1 * d2 < 0 and d3 * d4 < 0


def _polys_cross(A, B):
    for i in range(len(A) - 1):
        for j in range(len(B) - 1):
            if _seg_cross(A[i], A[i + 1], B[j], B[j + 1]):
                return True
    return False


# ---------------------------------------------------------------- sector
class Sector:
    """Local frame = the north quadrant between the NW and NE spokes, relative to start, y down.

    world = start + rot90(mirror_x(local), k). Orbit angles: 0 = right, 90 = down, 270 = up.
    Named skeleton nodes of the local frame: L1 L2 Lv (left spoke), R1 R2 Rv (right spoke),
    N (cardinal corner), keyN (cardinal keystone), keyR (keystone of the right diagonal corner).
    """

    def __init__(self, tree, k, mirror):
        self.t, self.k, self.mirror = tree, k, mirror
        self.variant = k % 2
        for name, rel in (('L1', (-120, -120)), ('L2', (-220, -220)), ('Lv', (-360, -360)),
                          ('R1', (120, -120)), ('R2', (220, -220)), ('Rv', (360, -360)),
                          ('N', (0, -CARDINAL)), ('keyN', (0, -KEY_CARDINAL)), ('keyR', (KEY_DIAGONAL[0], -KEY_DIAGONAL[1]))):
            setattr(self, name, self.at(rel))

    # transforms
    def w(self, p):
        x, y = p
        if self.mirror: x = -x
        return add(START_POS, rot90((x, y), self.k))

    def local(self, world):
        x, y = rot90(sub(world, START_POS), -self.k)
        return (-x, y) if self.mirror else (x, y)

    def angle(self, a):
        if self.mirror: a = 180 - a
        return (a + 90 * self.k) % 360

    def pos(self, nid): return self.local(self.t.byid[nid]['W'])
    def at(self, rel): return self.t.at(sub(self.w(rel), START_POS))
    def id(self, name): return self.t.gid('s%d%s/%s' % (self.k, 'm' if self.mirror else '', name))

    # nodes and clusters
    def free(self, name, ntype, rel):
        return self.t.add_node(self.id(name), ntype, self.w(rel), name)

    def cluster(self, name, center, radii, color=(0.5, 0.5, 0.8), shared=False):
        """shared=True: one cluster for all sectors (e.g. centred on the start)."""
        cid = self.t.gid('shared/' + name) if shared else self.id('cluster/' + name)
        if not any(c['ID'] == cid for c in self.t.clusters):
            self.t.clusters.append(dict(ID=cid, Name=name, Center=self.w(center), Orbits=[float(r) for r in radii], Color=color))
        return cid

    def center(self, cid):
        return self.local(self.t.cluster(cid)['Center'])

    def orbit(self, name, ntype, cid, angle, oi=0, existing=None):
        """Node on orbit oi at a local angle. existing=<skeleton id> attaches that node without moving it."""
        c = self.t.cluster(cid)
        a = self.angle(angle)
        w = polar(c['Center'], c['Orbits'][oi], a)
        fields = dict(PlacementMode=1, ClusterID=cid, OrbitIndex=oi, OrbitAngle=float(a), W=w, Position=w)
        if existing:
            n = self.t.byid[existing]
            assert length(sub(n['W'], w)) < 3, 'orbit would move %s' % n['Label']
            n.update(fields)
            return existing
        return self.t.add_node(self.id(name), ntype, w, name, **fields)

    def ring(self, prefix, cid, angles, types, oi=0, closed=False):
        """Consecutive orbit nodes linked as arcs. Keep each step < 170 deg and >= ~45 units of arc."""
        ids = [self.orbit('%s%d' % (prefix, i), types[i], cid, a, oi) for i, a in enumerate(angles)]
        for a, b in list(zip(ids, ids[1:])) + ([(ids[-1], ids[0])] if closed else []):
            self.t.link(a, b)
        return ids

    # connections
    def link(self, a, b, c1=None, c2=None):
        """Straight link, or a cubic with absolute local control points c1 (near a) and c2 (near b)."""
        self.t.link(a, b)
        if c1 is None:
            return
        pa, pb = self.t.byid[a]['W'], self.t.byid[b]['W']
        wc1, wc2 = self.w(c1), self.w(c2)
        if a > b:
            a, b, pa, pb, wc1, wc2 = b, a, pb, pa, wc2, wc1
        anchor = lerp(pa, pb, 0.5)
        self.t.beziers.append(dict(NodeIdA=a, NodeIdB=b, AnchorPercent=50.0,
                                   InHandleOffset=sub(wc1, anchor), OutHandleOffset=sub(wc2, anchor)))

    def curve_chain(self, prefix, a, b, c1, c2, ts, types):
        """New nodes along ONE cubic a->b at params ts; each segment is an exact piece, so the line is smooth."""
        P = (self.pos(a), c1, c2, self.pos(b))
        ids = [a] + [self.free('%s%d' % (prefix, i), types[i], cubic(P, t)) for i, t in enumerate(ts)] + [b]
        params = [0.0] + list(ts) + [1.0]
        for i in range(len(ids) - 1):
            sp = split(P, params[i], params[i + 1])
            self.link(ids[i], ids[i + 1], sp[1], sp[2])
        return ids[1:-1]

    def spline(self, ids, t_start=None, t_end=None, tension=1.0):
        """C1-smooth path through existing nodes (Catmull-Rom). Pass end tangents to flow into orbits/edges."""
        P = [self.pos(i) for i in ids]
        T = []
        for i in range(len(P)):
            if i == 0 and t_start is not None: T.append(norm(t_start))
            elif i == len(P) - 1 and t_end is not None: T.append(norm(t_end))
            else: T.append(norm(sub(P[min(i + 1, len(P) - 1)], P[max(i - 1, 0)])))
        for i in range(len(P) - 1):
            L = length(sub(P[i + 1], P[i])) / 3 * tension
            self.link(ids[i], ids[i + 1], add(P[i], mul(T[i], L)), sub(P[i + 1], mul(T[i + 1], L)))

    def arc_link(self, a, b, cid, oi=0):
        """Bezier that follows the (short) circular arc of orbit oi between two nodes lying on that circle,
        for when a node cannot be an orbit member (e.g. the touching point of two circles)."""
        c = self.center(cid)
        r = self.t.cluster(cid)['Orbits'][oi]
        a0, a1 = angle_to(c, self.pos(a)), angle_to(c, self.pos(b))
        sweep = (a1 - a0 + 540) % 360 - 180
        kappa = 4 / 3 * math.tan(math.radians(abs(sweep)) / 4) * r
        d = 1 if sweep > 0 else -1
        self.link(a, b, add(self.pos(a), mul(self.tangent(a0, d), kappa)), sub(self.pos(b), mul(self.tangent(a1, d), kappa)))

    def tangent(self, angle, direction=1):
        """Local tangent of travel along an orbit at a local angle; +1 = increasing angle (clockwise on screen)."""
        a = math.radians(angle)
        return (-math.sin(a) * direction, math.cos(a) * direction)

    def toward(self, cid, nid):
        """Local orbit angle from the cluster centre toward a node: use it for radial entries."""
        return angle_to(self.center(cid), self.pos(nid))


def build(class_id, layout, symmetry='C4', target=None):
    """symmetry: 'C4' | 'D4' | 'NONE' or an explicit list of (k, mirror) sectors."""
    tree = Tree(class_id, target)
    for k, mirror in (SYMMETRY[symmetry] if isinstance(symmetry, str) else symmetry):
        layout(Sector(tree, k, mirror))
    return tree


def main(class_id, layout, symmetry, asset_path, asset_name, png):
    """Standard CLI for layout scripts: render + validate, and write with --write."""
    import sys
    tree = build(class_id, layout, symmetry, asset_path)
    tree.render(png, label='--label' in sys.argv)
    tree.report()
    problems = tree.validate()
    for p in problems:
        print('  !', p)
    for n in tree.notes:
        print('  note:', n)
    print('OK' if not problems else '%d problems' % len(problems), '| preview:', png)
    if '--write' in sys.argv:
        if problems and '--force' not in sys.argv:
            print('not written: fix problems or pass --force')
            return
        tree.write(asset_path, asset_name)
        print('written', asset_path)
