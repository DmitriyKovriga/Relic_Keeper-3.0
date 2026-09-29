"""Rogue tree: a shuriken. C4 pinwheel, cluster variants alternate between even and odd quadrants.

    python rogue_shuriken.py            # preview + validation
    python rogue_shuriken.py --write    # write the asset
"""
import os
import tempfile

from treegen import NOTABLE, SMALL, add, main, mul, norm

ASSET = 'Assets/Resources/PassiveTrees/RoguePassiveSkillTree/RoguePassiveSkillTree.asset'

HOLE, BLADE, GUARD, WEDGE = (0.55, 0.5, 0.35), (0.36, 0.55, 0.62), (0.62, 0.45, 0.72), (0.45, 0.66, 0.5)
EDGE_DIR = norm((360, -140))            # octagon edge from the left diagonal corner toward N
EDGE_IN = (0.363, 0.933)                # inward normal of that edge


def sector(q):
    even = q.variant == 0

    # centre: the ring is a real orbit through the first spoke nodes, notables on an inner orbit
    hole = q.cluster('Hole', (0, 0), (95, 170), HOLE, shared=True)
    q.orbit(None, None, hole, 225, oi=1, existing=q.L1)
    q.orbit(None, None, hole, 315, oi=1, existing=q.R1)
    hub = q.orbit('hub', SMALL, hole, 270, oi=1)
    q.link(q.L1, hub); q.link(hub, q.R1)
    q.link(hub, q.orbit('inner', NOTABLE, hole, 270, oi=0))

    # blade: both edges hook clockwise, tip on the cardinal corner; each edge is one cubic
    lead = q.curve_chain('lead', q.L2, q.N, (-150, -260), (-50, -380), (0.3, 0.55, 0.8), (SMALL,) * 3)
    trail = q.curve_chain('trail', q.R1, q.N, (150, -230), (90, -410), (0.28, 0.52, 0.76), (SMALL,) * 3)

    if even:
        cb = q.cluster('Blade Fork', (0, -300), (60,), BLADE)
        left = q.ring('coreL', cb, (90, 150, 198, 244), (SMALL, SMALL, SMALL, NOTABLE))
        right = q.ring('coreR', cb, (30, 342, 296), (SMALL, SMALL, NOTABLE))
        q.link(left[0], right[0])
        q.link(hub, left[0])                       # radial: hub, entry and centre are collinear
    else:
        cb = q.cluster('Blade Eye', (0, -300), (58,), BLADE)
        ring = q.ring('eye', cb, (90, 150, 210, 270, 330, 30), (SMALL,) * 6, closed=True)
        q.link(ring[3], q.free('eyeCore', NOTABLE, (0, -300)))
        q.link(hub, ring[0])

    # tail from the diagonal corner hugs the octagon edge and enters the orbit tangentially
    gc = (-180, -360) if even else (-174, -350)
    cg = q.cluster('Guard' if even else 'Guard Snail', gc, (42 if even else 46,), GUARD)
    entry = 249
    if even:
        guard = q.ring('guard', cg, (entry, 330, 50), (SMALL, SMALL, NOTABLE))
    else:
        guard = q.ring('snail', cg, (entry, 320, 30, 100), (SMALL,) * 4)
        q.link(guard[-1], q.free('snailCore', NOTABLE, gc))
    tail = q.free('tail', SMALL, add(add((-360, -360), mul(EDGE_DIR, 95)), mul(EDGE_IN, 20)))
    q.spline([q.Lv, tail, guard[0]], t_start=EDGE_DIR, t_end=q.tangent(entry))

    # wedge between the trailing edge and the next spoke: radial entry and radial exit
    cw = q.cluster('Wedge' if even else 'Wedge Ring', (225, -335), (50,), WEDGE)
    a_in, a_out = q.toward(cw, q.R2), q.toward(cw, trail[1])
    if even:
        wedge = q.ring('wedge', cw, (a_in, a_out, a_out + 72, a_out + 144), (SMALL,) * 3 + (NOTABLE,))
    else:
        wedge = q.ring('wring', cw, (a_in, a_out, a_out + 84, a_out + 168), (SMALL,) * 4, closed=True)
        q.link(wedge[2], q.free('wringCore', NOTABLE, (225, -335)))
    q.link(q.R2, wedge[0])
    q.link(trail[1], wedge[1])


if __name__ == '__main__':
    main('rogue', sector, 'C4', ASSET, 'RoguePassiveSkillTree',
         os.path.join(tempfile.gettempdir(), 'rogue_tree.png'))
