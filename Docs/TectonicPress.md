# Tectonic Press

Two-handed hammer special. Cooldown 10 seconds; mana 25. The initial forward
strike deals 250% weapon damage and adds 1500 Pushback rating (approximately
three world units before resistance). Normal action-speed, melee, attack and
area damage rules apply.

Each accepted knockback receives its own empowered collision charge. During
that push, a swept forward check detects walls or other living enemies even
though enemy/enemy physical collisions are disabled. A successful collision
consumes the charge before emitting a shockwave. Floors, ceilings, triggers,
the victim's own hurtboxes and obstacles behind it do not count. A push that
expires in open space produces no wave. Frozen, fixed or fully pushback-immune
targets still take the weapon hit but cannot generate a wave. A rejected hit
does not push. Replacement pushes, pull, disable, reinitialization and freezing
clear the old charge.

The wave deals 200% weapon damage multiplied by `ShockwaveDamage / 100`.
`ShockwaveDamage` starts at 100 and supports normal Flat, Increase/Decrease,
More/Less layers; it affects only the secondary wave. It is a combat scalar,
not an elemental Shock modifier. Its enum entry is appended for save stability.
For example, 150 ShockwaveDamage gives a 300% weapon-damage wave, while the
initial strike remains 250%.

Weapon-hand stats and skill modifiers are captured at the strike. Shockwaves
retain ordinary damage channels, conversion, critical hits and hit ailments.
Each wave's weapon and critical roll is captured before a possible weapon swap.
Waves carry no pushback and no empowered charge. Each wave hits each target once,
including the pushed victim and the collided enemy, with wall occlusion.
Separate victims can each generate their own wave; waves cannot recursively
produce further waves. Owner death/disable prevents pending wave damage.

AreaOfEffect scales the collision proximity radius (base 0.12 units) and the
wave radius (base 1.5 units). The wave follows the project's full X / half Y
area scaling. The damage preview shows only the guaranteed initial strike;
EN/RU descriptions explain the conditional secondary damage.

Assets: `Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/TectonicPress/`.
Registration: `2HHammerSkillPoolRKM`, Special role, item database. Build command:
`Tools/Skills/Build Tectonic Press`.

PixelLab artwork:

| Piece | PixelLab ID |
|---|---|
| Icon | gallery `b6abacfe-0662-5253-89a8-f4ea05b99a59`, job `a1537ce6-59ae-4786-927c-1646313ccb33` |
| Strike animation | effect `3bc3db6d-eb64-493f-a14e-b7d87e7ecbae` |
| Collision shockwave | effect `f9257c5e-8da8-44fc-9b73-ac75cfa23742` |

Spritesheets preserve PixelLab frame metadata, point sampling, no compression,
24 pixels per unit, and 24 animation frames per second. No procedural substitute
art is used. Regression filter: `RelicKeeper.Tests.EditMode.TectonicPressTests`.
