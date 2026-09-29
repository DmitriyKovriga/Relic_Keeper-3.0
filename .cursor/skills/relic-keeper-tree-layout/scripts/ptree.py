"""Minimal reader for PassiveSkillTreeSO .asset YAML (Unity text serialization)."""
import math
import re


def _vec(s):
    m = re.search(r'x: ([-\d.e]+), y: ([-\d.e]+)', s)
    return (float(m.group(1)), float(m.group(2)))


def parse(path):
    """Returns (nodes, clusters, beziers). Each node gets 'W' = world position and 'Raw' = content YAML lines."""
    lines = open(path, encoding='utf-8').read().splitlines()
    nodes, clusters, beziers = [], [], []
    section, cur, listkey, raw = None, None, None, False
    for ln in lines:
        if ln.startswith('  Nodes:'): section = 'n'; continue
        if ln.startswith('  Clusters:'): section = 'c'; continue
        if ln.startswith('  BezierConnections:'): section = 'b'; continue
        if re.match(r'^  \w', ln): section = None; continue
        if section is None: continue
        m = re.match(r'^  - (\w+): ?(.*)$', ln)
        if m:
            cur = {m.group(1): m.group(2), 'Raw': []}
            {'n': nodes, 'c': clusters, 'b': beziers}[section].append(cur)
            listkey, raw = None, False
            continue
        if section == 'n':
            if ln.startswith('    Template:'): raw = True
            if ln.startswith('    ConnectionIDs:'): raw = False
            if raw:
                cur['Raw'].append(ln)
        m = re.match(r'^    (\w+): ?(.*)$', ln)
        if m:
            k, v = m.group(1), m.group(2)
            if v == '' and k in ('ConnectionIDs', 'Orbits', 'UniqueModifiers', 'UniqueStatScalingRules'):
                cur[k] = []; listkey = k
            else:
                cur[k] = v; listkey = None
            continue
        m = re.match(r'^    - (.*)$', ln)
        if m and listkey == 'ConnectionIDs':
            cur[listkey].append(m.group(1).strip()); continue
        m = re.match(r'^    - Radius: (.*)$', ln)
        if m and section == 'c':
            cur['Orbits'].append(float(m.group(1)))
    cl = {}
    for c in clusters:
        c['Center'] = _vec(c['Center']); cl[c['ID']] = c
    for n in nodes:
        n['Position'] = _vec(n['Position'])
        n['NodeType'] = int(n['NodeType'])
        n['PlacementMode'] = int(n['PlacementMode'])
        n['OrbitIndex'] = int(n['OrbitIndex'])
        n['OrbitAngle'] = float(n['OrbitAngle'])
        n['W'] = n['Position']
        if n['PlacementMode'] == 1 and n['ClusterID'] in cl:
            c = cl[n['ClusterID']]
            if 0 <= n['OrbitIndex'] < len(c['Orbits']):
                r, a = c['Orbits'][n['OrbitIndex']], math.radians(n['OrbitAngle'])
                n['W'] = (c['Center'][0] + math.cos(a) * r, c['Center'][1] + math.sin(a) * r)
    for b in beziers:
        b['AnchorPercent'] = float(b['AnchorPercent'])
        b['InHandleOffset'] = _vec(b['InHandleOffset'])
        b['OutHandleOffset'] = _vec(b['OutHandleOffset'])
    return nodes, clusters, beziers


def stats(path):
    from collections import Counter
    nodes, clusters, beziers = parse(path)
    types = Counter(n['NodeType'] for n in nodes)
    edges = {tuple(sorted((n['ID'], c))) for n in nodes for c in n['ConnectionIDs']}
    return dict(nodes=len(nodes), small=types[0], notable=types[1], keystone=types[2],
                clusters=len(clusters), beziers=len(beziers), edges=len(edges))


if __name__ == '__main__':
    import sys
    for p in sys.argv[1:]:
        print(p, stats(p))
