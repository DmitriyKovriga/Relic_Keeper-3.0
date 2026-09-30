"""Extract geometry only from the official PoE passive tree JSON (or its HTML page).

Usage: python Tools/PassiveTree/import_poe_motifs.py path/to/data.json
No icons, stats, class layouts or mastery paths are imported. Normal editor generation
uses the checked-in catalog offline; this is an explicit maintenance operation.
"""
import argparse
import collections
import hashlib
import json
import math
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Assets/Editor/PassiveTree/Zones/PassiveZoneMotifs.json'


def extract(text):
    if text.lstrip().startswith('{'):
        return json.loads(text)
    start = text.index('{', text.index('passiveSkillTreeData'))
    return json.JSONDecoder().raw_decode(text[start:])[0]


def angle(node, counts):
    count = counts[node['orbit']]
    if count == 16:
        return [0, 30, 45, 60, 90, 120, 135, 150, 180, 210, 225, 240, 270, 300, 315, 330][node['orbitIndex']] - 90
    if count == 40:
        return sorted(set(range(0, 360, 10)) | {45, 135, 225, 315})[node['orbitIndex']] - 90
    return 360 * node['orbitIndex'] / count - 90


def signature(points, radii, edges):
    # Canonical under rotation, reflection, node ordering and uniform scale.
    variants = []
    for pivot in points:
        for flip in (-1, 1):
            coords = [(round(radii[n['orbit']], 4), round(((n['angle'] - pivot['angle']) * flip) % 360, 3), n['notable']) for n in points]
            order = sorted(range(len(coords)), key=lambda i: coords[i])
            mapping = {old: new for new, old in enumerate(order)}
            links = sorted(tuple(sorted((mapping[a], mapping[b]))) for a, b in edges)
            variants.append(str(([coords[i] for i in order], links)))
    return hashlib.sha256(min(variants).encode()).hexdigest()[:16]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=pathlib.Path)
    args = parser.parse_args()
    raw = args.input.read_bytes()
    data = extract(raw.decode('utf-8-sig'))
    nodes = data['nodes']
    profiles = {}
    component_count = 0
    group_count = 0
    for gid, group in data['groups'].items():
        eligible = {nid for nid in group['nodes'] if nid in nodes and not any(nodes[nid].get(flag) for flag in (
            'isMastery', 'isProxy', 'isBlighted', 'ascendancyName', 'isKeystone', 'isJewelSocket', 'expansionJewel'))
            and 'classStartIndex' not in nodes[nid]}
        adjacency = {nid: set() for nid in eligible}
        for nid in eligible:
            for other in nodes[nid].get('out', []) + nodes[nid].get('in', []):
                other = str(other)
                if other in eligible and other != nid:
                    adjacency[nid].add(other)
                    adjacency[other].add(nid)
        pending = set(eligible)
        group_used = False
        while pending:
            stack = [min(pending, key=int)]
            component = set()
            while stack:
                nid = stack.pop()
                if nid in component:
                    continue
                component.add(nid)
                pending.discard(nid)
                stack.extend(adjacency[nid] - component)
            if not 3 <= len(component) <= 16 or not any(nodes[n].get('isNotable') for n in component):
                continue
            component_count += 1
            group_used = True
            ids = sorted(component, key=int)
            orbit_ids = sorted({nodes[n]['orbit'] for n in ids})
            orbit_map = {old: new for new, old in enumerate(orbit_ids)}
            source_radii = [data['constants']['orbitRadii'][o] for o in orbit_ids]
            maximum = max(source_radii)
            if maximum <= 0:
                continue
            radii = [round(r / maximum, 6) for r in source_radii]
            points = []
            for nid in ids:
                n = nodes[nid]
                neighbours = [str(v) for v in n.get('out', []) + n.get('in', [])]
                points.append({'orbit': orbit_map[n['orbit']], 'angle': angle(n, data['constants']['skillsPerOrbit']),
                               'notable': bool(n.get('isNotable')), 'port': any(other not in component and other in nodes and not nodes[other].get('isMastery') for other in neighbours)})
            index = {nid: i for i, nid in enumerate(ids)}
            edges = sorted({tuple(sorted((index[nid], index[other]))) for nid in ids for other in adjacency[nid]})
            key = signature(points, radii, edges)
            source = {'group': gid, 'names': [nodes[n]['name'] for n in ids if nodes[n].get('isNotable')]}
            if key in profiles:
                profiles[key]['sources'].append(source)
            else:
                profiles[key] = {'id': key, 'radii': radii, 'nodes': points, 'edges': [{'a': a, 'b': b} for a, b in edges], 'sources': [source]}
        group_count += int(group_used)
    motifs = sorted(profiles.values(), key=lambda m: (len(m['nodes']), m['id']))
    result = {'schema': 1, 'sourceUrl': 'https://www.pathofexile.com/passive-skill-tree',
              'sourceSha256': hashlib.sha256(raw).hexdigest(), 'sourceGroups': group_count,
              'sourceComponents': component_count, 'motifs': motifs}
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'groups': group_count, 'components': component_count, 'uniqueMotifs': len(motifs),
                      'byNodeCount': dict(sorted(collections.Counter(len(m['nodes']) for m in motifs).items()))}))


if __name__ == '__main__':
    main()
