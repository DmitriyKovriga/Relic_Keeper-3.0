# Contact shadows — V04

Implemented 2026-10-24 using the existing GroundingVisualController and shadow
sprites. PlayerMovement and EnemyEntity already add this controller, so the pass
applies to existing actors without prefab migration or another shadow system.

The oval uses the opaque foot band with a minimum width of 130% of body width,
before the existing enemy-specific width multiplier. It has at least four pixels
of depth at 24 PPU and uses the sprite's full authored opacity at contact (the
source sprite already contains soft transparency). Height smoothly reduces opacity and width; contact
disappears at 2.5 world units. Inspector controls are Shadow Opacity, Shadow Fade
Height, Shadow Airborne Scale (0.7), and Shadow Depth Multiplier (0.8).
Existing lift, overlap, presets, and enemy offset/width controls remain intact.

The shadow aligns with the supporting surface normal. Side probes restrict its
width to the same surface plane; nearby lower floors do not support overhanging
portions. A partially supported body can use its remaining foot contact even
after its centre crosses a ledge. Ground triggers are ignored; solid Ground and
OneWayPlatform colliders are supported. Missing support or disabling the actor
hides the shadow immediately. Contact shadows draw on Default above normal
terrain, below World/Hero actors, and straddle the supporting surface so the
floor does not hide their lower half. Sprite assets, color profiles, combat,
and movement settings are unchanged.

## Validation

Unity compilation passed. The combined GroundShadowTests, JumpPuffTests, and
GroundDashVisualTests suite passed **35/35**, with no skips. Contact tests cover
animation-independent floor anchoring, overhead platforms, jump height, distant
floors, ledges including partial support, slopes, triggers, one-way platforms,
and disable cleanup.

Visibility correction: the initial V04 placement was too small and drew behind
terrain, hiding most of the oval beneath the feet. The corrected 13-test contact
suite includes GPU on/off comparisons at 480x270, proving terrain pixels visibly
darken with an actor present on both Background and Default terrain layers.
The study also writes per-class no-shadow comparisons and visibility reports;
Rogue samples are included in hub/Mortfall even without a loaded live player.

Play Mode captures inspected at 480x270: Warrior/Rogue/Mage on contact, a low
jump, a distant floor, a ledge and a slope; the actual hub camera; and static
samples on copied Mortfall room 001 artwork with temporary matching tilemap
colliders. This checks rendering on representative geometry without starting
dungeon gameplay or changing progress. Continuous motion and target-device
performance are not established by these still captures.

To regenerate the study, enter Play Mode and use **Tools > Relic Keeper >
Visuals > Capture Contact Shadow Study**. Images are written to ignored
`Logs/ContactShadows/`; all temporary actors, colliders, cameras and shadows are
removed before the call returns. The live player and saved scene remain intact.

V05 (authored contact darkening under roofs, walls and props) is the next proposed
step. Lighting, background mist and parallax remain separate later steps.
