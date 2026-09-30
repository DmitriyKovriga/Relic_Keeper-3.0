"""Rogue tree, written into the original RoguePassiveSkillTree asset (no second copy).

Even quadrants: one dense ring, a wider cap, one notable on the cap only.
A small flank arc faces the ring. Odd quadrants: a straight column with one
notable after four smalls, and a real bow beside it that ends in its own
notable. Corner hooks are a visible wave, not a line riding the octagon edge.

    python rogue_daggers.py
    python rogue_daggers.py --write
"""
import os
import tempfile

from treegen import NOTABLE, SMALL, add, main, mul, norm

_TREE = 'Rogue' + 'PassiveSkillTree'
ASSET = 'Assets/Resources/PassiveTrees/%s/%s.asset' % (_TREE, _TREE)

CROWN = (0.72, 0.46, 0.28)
FLANK = (0.32, 0.52, 0.62)
WING = (0.50, 0.40, 0.62)
DIR_N = norm((-360, -140))          # Rv -> N
IN_N = (-0.362, 0.932)


def on_edge(origin, direction, inward, along, inset):
    return add(add(origin, mul(direction, along)), mul(inward, inset))


def axis_cluster(q, name, center, radii, color):
    if not q.mirror:
        return q.cluster(name, center, radii, color)
    return q.t.gid('s%d/cluster/%s' % (q.k, name))


def sector(q):
    even = q.variant == 0

    if not q.mirror:
        stem = q.free('stem', SMALL, (0, -158))
    else:
        stem = q.at((0, -158))
    q.link(q.R1, stem)

    if even:
        _crown(q, stem)
    else:
        _column(q, stem)

    # wave in the wedge between the spoke and the octagon edge: toward the edge,
    # toward the spoke, toward the edge. Straight skeleton lines stay straight.
    bis = norm((-1.64, 0.34))
    tail = [q.free('tail%d' % i, t, p) for i, (p, t) in enumerate((
        ((286, -363), SMALL), ((237, -312), SMALL), ((169, -346), NOTABLE)))]
    q.spline([q.Rv] + tail, t_start=bis)


def _crown(q, stem):
    c = axis_cluster(q, 'Crown', (0, -288), (74, 120), CROWN)
    if not q.mirror:
        bot = q.orbit('cBot', SMALL, c, 90, oi=0)
        top = q.orbit('cTop', SMALL, c, 270, oi=0)
        cap = q.orbit('cCap', NOTABLE, c, 270, oi=1)
        q.link(stem, bot)
        q.link(top, cap)
        q.link(cap, q.N)
    else:
        bot = q.at((0, -214))
        top = q.at((0, -362))
        cap = q.at((0, -408))

    low = q.orbit('cLow', SMALL, c, 45, oi=0)
    east = q.orbit('cEast', SMALL, c, 0, oi=0)
    high = q.orbit('cHigh', SMALL, c, 315, oi=0)
    cap_r = q.orbit('cCapR', SMALL, c, 315, oi=1)
    for a, b in ((bot, low), (low, east), (east, high), (high, top), (high, cap_r), (cap_r, cap)):
        q.link(a, b)

    # small arc: two smalls, then the notable. Sits off the corner wave.
    fc = q.cluster('Flank', (158, -262), (36,), FLANK)
    a = q.toward(fc, q.R2)
    flank = q.ring('flank', fc, (a, a - 80, a - 160), (SMALL, SMALL, NOTABLE))
    q.link(q.R2, flank[0])
    q.link(flank[0], east)


def _column(q, stem):
    """Straight road. The bow beside it is a real curve, and its notable is the tip."""
    if not q.mirror:
        col = [q.free('col%d' % i, SMALL, (0, y)) for i, y in enumerate((-230, -305, -375))]
        prize = q.free('colPrize', NOTABLE, (0, -440))
        q.link(stem, col[0])
        for a, b in zip(col, col[1:]):
            q.link(a, b)
        q.link(col[-1], prize)
        q.link(prize, q.N)
    else:
        col = [q.at((0, y)) for y in (-230, -305, -375)]

    bow = [q.free('bow%d' % i, t, p) for i, (p, t) in enumerate((
        ((78, -255), SMALL), ((118, -325), SMALL), ((132, -395), NOTABLE)))]
    q.spline([col[0]] + bow, t_start=(1, -0.35), t_end=(0.15, -1))
    q.link(q.R2, bow[0], (160, -270), (110, -250))


if __name__ == '__main__':
    main('rogue', sector, 'D4', ASSET, _TREE,
         os.path.join(tempfile.gettempdir(), 'rogue_tree.png'))
