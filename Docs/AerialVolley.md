# Aerial Volley / Воздушный залп

Bow special in the existing secondary skill pool. Cooldown: 10 seconds. Mana cost: 10. Accepted configuration: three successive volleys of three arrows, each arrow dealing 200% weapon damage. Projectile Count adds floor(max(0, stat)) arrows to every volley. Each volley uses a narrow fixed 18° fan; additional arrows increase its density. Normal projectile speed, pierce, fork, chain and independent hit-history rules apply.

## Sequence and leap

The base sequence has Attack Speed mode and a 1.4 skill speed multiplier. At Attack Speed 1 it takes approximately 0.714 seconds. Releases occur at 20%, 44% and 68% (approximately 0.143, 0.314 and 0.486 seconds). Each volley has a separate bow draw, release and recovery. A `DrawnProjectilePrefab` override on each windup re-nocks the arrow once when that window begins; projectiles leave the drawn arrow's current world position.

Movement locks from 0% to 90%. At 2%, a 12-force impulse at 115° relative to forward launches the hero backward and upward, while preserving facing. Force scales by max(0, 1 + JumpForce/100), matching the existing percent interpretation of Jump Force. Both the height and backward distance increase. Gravity, solid collisions and incoming damage remain active. The leap does not consume a normal jump charge and does not grant immunity, damage reduction or parry. A dedicated skill-jump presentation event reuses each character's existing jump animation without spawning the ordinary jump dust over the authored takeoff effect.

Attack Speed changes volley timing, while Jump Force changes the physical leap. Depending on those stats, gravity and obstacles, later volleys may occur near landing. There is no airborne-only restriction or suspension of gravity. Walls and other solid bodies can stop retreat. Dodging or other skill cancellation unlocks movement, hides the nocked arrow and forfeits future volleys; already released arrows continue normally.

With 100 physical weapon damage and no other modifiers, each arrow deals 200. All nine connecting yields 1800 before mitigation. The existing damage preview sums one arrow from each volley (600), rather than assuming every projectile overlaps one enemy; both locales explain this.

## Art and data

PixelLab supplies the opaque 32×32 icon, looping four-frame 32×16 arrow (20 fps), and seven-frame 40×24 takeoff dust (24 fps). Muted feathers, steel arrowheads and restrained wind/dust strokes fit physical archery. Point filtering, 24 PPU, no compression/mipmaps. Arrow pivot follows the PixelLab anchor at (21,8); dust uses its center anchor. The takeoff effect stays at the launch position. Standard Animator clips and the existing VFX lifetime path handle playback and cleanup.

- Icon job: `6c9c89ad-f514-413e-9d24-a7f2ba99c14a`; gallery: `234eaa9c-c50b-5332-80e4-a4e35dbf7936`.
- Arrow effect: `ddb193f4-9439-4ccc-8dd9-d538e829a9e3`; step: `f596fd65-7416-48f1-bd77-6fa93918fb79`; export: `063aad78-bf6d-4262-91c4-dfafbf22ab63`.
- Takeoff effect: `765b70ca-136f-446a-81ec-61a43031b750`; step: `ebd781f9-7a9f-402a-84e2-1263c6dbbfc1`; export: `ac44e5ed-d587-407a-ba8f-701e5726fe02`.

`AerialVolleyBuilder.Apply` builds art, recipe, registration and English/Russian localization. `RefreshArt` updates only artwork. Balance lives in the recipe and skill asset. Generic impulse options preserve previous skills' behavior unless opted into; no existing serialized fields or enums were renamed.

## Checks

The 17 focused cases verify registration/costs/art, both-facing jump-force scaling and gravity, jump animation notification, three timed redraws/releases, per-volley count, per-arrow damage and preview, cancellation before/between volleys, ordinary incoming damage, wall collision, and both locales. Live Play Mode renders at 480×270 verify the physical backward/upward leap and nine arrows released forward through three volleys.

Final validation: Unity compilation succeeded and all 91 relevant EditMode checks passed, with zero failures or skips (AerialVolleyTests, NearFanTests, BowDrawTests, PiercingBowShotTests, RetreatingThrustTests, JumpPuffTests, descriptions/highlights, DPS previews and icon backgrounds). Final console error query was empty. Play Mode captured the three separate redraws/releases with jump/fall poses and the dedicated takeoff effect. Temporary preview code was removed; Play Mode stopped and simulation mode, autosave, time scale and background settings restored.
