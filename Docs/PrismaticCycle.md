# Prismatic Cycle

Wand primary ability, zero cooldown and mana cost. Four accepted uses form a cycle:
fire → cold → lightning → prismatic burst → fire.

The first three casts convert all weapon damage to their respective element.
The fourth cast fires a dedicated prismatic volley with damage split equally
between fire, cold and lightning, and 100% more SpellDamage. It does not launch
an ordinary elemental shot or a delayed bonus. ProjectileCount applies to all
four volleys; forks, chains and pierces preserve each projectile's damage snapshot.
Prismatic hits create the three-colour impact effect.

Progress advances once per accepted use, including misses and cancelled accepted
casts, independently of projectile count or hits. Busy or otherwise rejected
input does not advance it. Progress is separate per hand, survives runner
replacement, and resets on owner disable, death or character change.
Damage previews average the four casts; English and Russian tooltips describe
the fourth-cast burst. Projectile art remains at half scale.

Editable projectile speed, lifetime, damage coefficient and bonus More modifier
live in `Step_PrismaticCycle`. `Tools/Skills/Build Prismatic Cycle` rebuilds the
sprites, material, prefabs, recipe, skill, pool/database registration and labels.

## PixelLab artwork

The projectile presentation now uses four distinct eight-frame PixelLab VFX
loops at 20 fps: a flame tail, an ice crystal, a lightning fork, and a white
prism with three coloured trails. The bonus impact uses an authored flash,
three expanding elemental arcs, and scattering sparks at 24 fps.
Animation lives on a child renderer; the original root sprites remain stable
collision references. Pooled launches reset the selected clip and its time.
Projectile art is rendered at half scale; the impact is also compact.
Point sampling, 24 pixels per unit, no compression or mipmaps.

PixelLab VFX Creator effects (editable on pixellab.ai):

| Animation | Effect ID |
|---|---|
| Fire | `741698ab-cac4-4a42-8a89-403510eb9ff3` |
| Cold | `fbbe8960-5363-4793-8ddf-ec0aa123b98f` |
| Lightning | `5d8555a1-7ba9-47c4-8636-938db92c6e0c` |
| Prismatic | `96cd8621-5f2f-4537-b1e8-7a65532332a5` |
| Impact | `3e45046f-b2a3-4bd3-bc76-bf4432411238` |

Spritesheets and PixelLab frame/pivot metadata live under `Animations/`.
The builder slices the supplied rectangles, preserving their order and pivots.
The original static artwork below is retained for the icon and collision anchors.

All new icon/projectile/impact art was generated through PixelLab MCP:

| Asset | Gallery ID | Generation job |
|---|---|---|
| Icon | `98af39d6-fade-5661-bcf6-42d9c91cc128` | `47c9b311-2127-4c07-bda9-789fab574b44` |
| Fire | `b0bb1d47-b762-5bc5-93f7-4c021ad295ee` | `724d4d97-7a4a-4b1a-b04e-ba7cfeec1cd8` |
| Cold | `7035cd25-1b06-5966-9a74-8a708ae3a080` | `9f51a67c-b73f-4bea-bad1-dd667f8563ea` |
| Lightning | `9e550a02-abff-591c-9d27-f3670a7a9303` | `562d742e-d086-459e-afae-ba8c280236ba` |
| Prismatic bolt/impact | `94689c62-326d-502f-8bf0-b4ba4feb76f4` | `57dc9e70-14ac-4745-a68f-d497719f4004` |

Tests: `RelicKeeper.Tests.EditMode.PrismaticCycleTests`. Live 480x270 checks
previously confirmed the original volley behaviour, fire/cold/lightning
order, rejection of repeated input while casting, unchanged mana, zero cooldown,
and English/Russian tooltip layout.
