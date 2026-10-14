# Tower shield and Tower Charge

The Adventurer's Tower Shield retains its identity, inventory icon, 100 armor,
25 health on block and 50 block chance. It now has a PixelLab generated held
sprite, a separate defensive pool containing the armor helmet's 15 affixes,
and a guaranteed Tower Charge skill. The accidentally created `shield` stance
is removed; `shield_guard` and Spectacular Parry remain available.

Tower Charge costs 20 mana and has a 5 second cooldown. At 1 attack per second,
the hero winds up for 0.16 seconds, charges 3 world units during the next 0.36
seconds, and recovers by 0.72 seconds. Attack speed changes the animation and
charge duration while preserving the intended distance. Walls block the charge;
ordinary enemy bodies do not. Gravity continues to work. The held shield stays
visible at impact and uses the reusable `shield_charge` swing. Animated ground
dash dust and a nine-frame ivory/silver body pressure arc emphasize movement.
The body effect follows the hero, mirrors with facing and fits the charge window
at the current attack speed. Each enemy along the swept path takes one hit
and receives 200 pushback directed forward. Cancelling restores normal movement.

## Reusable stat links

`SkillDataSO.StatLinks` is exposed in both the inspector and the Skill Editor's
Runtime Links section. Each link selects a source stat, percentage, target stat
and modifier type. Tower Charge uses Armor → 5% → DamagePhysical → Flat.
For example, 1000 current armor adds 50 physical base damage before applicable
damage increases, multipliers and conversions. Equipment bonuses and buffs
contribute to current armor. Ordinary global added damage and damage modifiers
still apply through the existing damage pipeline.

Sources are read from the unlinked skill stat scope to prevent recursive links
or ordering dependence. The resulting modifiers exist only in that skill's
scope, never mutate player stats, and are rebuilt for each hit. Recipe damage,
projectile damage and DPS previews share `StatLinkModifiers.Apply`. Automatic
EN/RU descriptions explain the link; numeric pushback remains hidden.

`PlayerCharge` is a reusable duration step with editable Distance.
`PersistentDamageRectangle.SweepBetweenSamples` optionally covers the area
between successive samples. `PushbackInFacingDirection` optionally directs
knockback forward even when a fast charge has already crossed its target.
`WeaponStrike.KeepWeaponVisible` is an optional override; other attacks keep
their existing behavior.

## Art provenance

Generated through the configured PixelLab MCP, using the original inventory
icon as the held model reference. Sources are kept in `Docs/Art/Shields`.

- Final held sprite: compact, angled 24×24 model with a thick beveled rim.
  `create_image_pro`: `bbc0be0a-f6d8-4d32-8ac7-b1da8f929ced`, refined through
  `edit_image_pixen`: `bb273a55-d8d5-4306-9c8a-5b4d5ca611d8`.
  The original generated source is archived as `TowerShieldHeldSource-v1.png`.
- Body pressure effect, `create_image_pro`:
  `783db602-4459-4efe-8103-98de28c36ffd`; nine-frame `animate_image` sequence:
  `cce2ef29-e7ce-4b1c-b913-514b5844bbf5`. The 48×48 frames are packed without
  repainting, imported at 24 PPU with point filtering and played during the charge.
- Skill icon with background, `edit_image_pixen`: `150d3629-5a12-490a-801e-a1a30d9da7a1`.
  PixelLab workbench edit `addc10ef-14fd-4014-8b2a-2597b91bf442` changes the
  generated gray background to dark charcoal, preserving all shield pixels.
- Runtime dust reuses `Dust_Dash_Small-Sheet.png` through a separate controller.

## Validation helpers

- `RelicKeeper.Tests.EditMode.TowerShieldChargeTests`: armor scaling, scope
  isolation, generic links, affixes, skill generation, preserved parry, preview,
  swept hits in both directions, forward pushback and movement restoration.
- `TowerShieldChargeRuntimeCheck.Run`: temporary Play Mode actors exercise the
  actual recipe, two enemy contacts, wall collision, animated VFX, mana,
  cooldown and restoration. It restores time scale/background settings and
  removes its temporary scene objects. Preview frames and report go to Temp.
- `TowerShieldChargeBuilder.Apply`: regenerates this feature's asset wiring.
- `TowerShieldChargeBuilder.ApplyVisuals`: updates the held sprite and body VFX
  while preserving existing skill balance, timing and other recipe steps.

Verified: compilation succeeded, 8 TowerShieldCharge tests and 41 related
EditMode tests passed, and the authored Play Mode recipe check passed with no
console errors. After the visual replacement, all 8 focused tests passed again;
the updated live check confirmed animated body VFX with no console errors.
`Docs/Art/Shields/TowerChargePreview.gif` shows the updated live capture.
