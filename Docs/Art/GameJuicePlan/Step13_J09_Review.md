# J09 — Existing-prop audit and final review gate

Reviewed 2026-10-30. **Review complete; no additional reactive-prop pilot shipped.**
This is an implementation decision at the roadmap's optional gate, not a claim
that the user explicitly asked to skip J09 or that an unbuilt effect passed A/B.

## Findings and decision

J09 asks for one or two visual-only prop families, first checking whether existing
objects already react. Expansion requires a meaningful benefit during ordinary
movement. The current candidates already have useful presentation:

- Mortfall torches have automatic source binding, independent smooth flame/light
  motion and source glow. The player partially blocks nearby light and produces
  a directional ground/wall shadow that responds continuously to movement and
  source crossing. These accepted interactions are implemented, not proposals.
- Exit-cache intake/valuable arrivals are covered by J01; portal activation is
  covered by J06. Another generic prop pulse would duplicate their feedback.
- Hub foliage/building motion, smoke, leaves, embers and Mortfall drips/dust are
  existing ambient treatments. Ambient movement is not being relabeled as
  player-triggered interaction. No separate authored Mortfall foliage/cloth pilot
  with an observed ordinary-play gap was selected in this bounded audit.

The saved Mortfall profile enables local lights, source glow and player shadows.
For example, its current player-light blocking strength is 0.857 and overlap
darkness is 0.14. These are inspected user settings, not new tuning targets.
The player shadow selects at most eight source contributions; this does not
establish a measured CPU/GPU budget for a new system.

No demonstrated need justifies another proximity/impact loop or new decorative
objects here. The optional addition is therefore left out for now. This preserves
the accepted visual treatment and the earlier user decision to remove the V14
forge-work-area/broken-winch props. Revisit J09 only for a specific existing prop
whose missing response is noticeable in ordinary play; then compare a bounded
pilot before expanding it.

Source/asset audit: `TorchLightBinding`, `LocalLight2DSource`, `PlayerLightShadow`,
`EnvironmentAnimation`, `AmbientParticleSource`, `HubBuildingWindBuilder`, the
saved Mortfall profile and Room 001's authored lights. Prior feature evidence is
in [player light/shadow](../Player_Light_Shadow.md),
[ambient particles](../Ambient_Particles.md), [J01](Step02_J01_Intake.md) and
[J06](Step08_J06_RoomResolution.md).

## Verification and one test correction

The first focused run passed nine tests and exposed one outdated assertion:
`EverySavedMortfallTorchHasASourceAndTerrainBlockers` counted every
`LocalLight2DSource` as a torch. Room 001 legitimately contains two torch sources
plus a nested `CoolRoomFill` (`IsRoomFill = true`, flame motion disabled). Its
third light is not a duplicate torch.

The assertion now counts non-fill sources when comparing with authored torches.
Its binding-count, terrain-blocker and multi-room checks remain. The room-fill
prefab, lighting values and room placements were preserved.

After the correction, Unity compilation and **10 EditMode tests passed,
0 failed/skipped**: `TorchLightBindingTests` (4) and `PlayerLightShadowTests` (6).
Coverage includes saved Mortfall torch wiring, automatic tile updates, no duplicate
binding, source disable/reuse, smooth independent flame motion, distance/crossing
geometry, body/collider ownership and actual rendered light/ambient preservation.
The local Graphify source cache was refreshed after the test edit.

Only that test and roadmap documentation changed. No runtime implementation,
scene, prefab, profile, light, collider, reward, target or raster art changed.
No new ordinary-run/A/B clip, standalone build or performance profile was produced
for this review. Earlier visual reports remain historical evidence of their
stated checks. The current combined ordinary-play acceptance remains pending.

## Remaining ordinary-play review

Use the current Editor or rebuild the isolated baseline player before comparing.
The old baseline executable does not contain the later features. The existing
isolated builder is `RelicKeeper.Editor.Combat.GameJuiceBaselineBuild.Build`;
it was not run during this audit.

Follow one ordinary 10–15-minute route and record where feedback is unclear:

1. In the hub, swap equipment, allocate a passive point and perform an orb craft
   if those resources are available. Confirm that the displayed result matches
   the committed change, including a worse/removed affix.
2. Enter Mortfall. Watch the original enemy attack mark and the caster's short
   projectile-aim line while moving. Check that release/recovery remain readable.
3. Fight with Ignite and Shock, including several enemies. Compare normal and
   reduced effects for clarity at ordinary camera scale.
4. Observe item bubbles/flights and rare/unique arrivals at the exit cache. Use
   the cache while flights are active and check item ownership/counts.
5. Watch portal unlock before full clear, then a clear/level-up overlap. Continue
   into another room without waiting for decorative effects.
6. Pass a torch from both sides and jump nearby. Review the existing light/shadow
   response in normal movement before considering another prop reaction.
7. With a character intended for the test, check death continuation and, on a
   separate attempt, automatic return. Death still has its ordinary consequences.
8. Check EN/RU and reduced effects. Record confusing or distracting moments before
   proposing more decoration; J07/J08 remain user-owned and excluded.

The implementation slices are ready for this combined review. This is not a claim
that final acceptance has already passed.
