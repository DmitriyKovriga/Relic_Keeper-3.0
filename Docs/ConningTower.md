# Conning Tower / Боевая рубка

Two-handed axe special, registered in `AxeSkillsPoolRB` and the item database. Cooldown: 10 seconds. Mana: 25 once per cast.

Three full sweeps around the player deal 100%, 100%, then 350% weapon damage. Each swing independently queries enemies on both sides, so an enemy can enter/leave between strikes and compound hurtboxes cannot duplicate one swing's hit. Normal attack/melee modifiers, critical hits and ailments apply independently to each hit. There are no forced bleeds, invulnerability or additional pushback.

At base Attack Speed 1 the sequence lasts 1.6 seconds (`SkillSpeedMultiplier = 0.625`), with hits at 18%, 42% and 80% of the pipeline: approximately 0.288, 0.672 and 1.28 seconds. Attack Speed scales the complete sequence. The final windup is longer. Centered elliptical hitboxes have horizontal radii 2.2 metres for the first two sweeps and 2.6 for the final sweep, with half-height radii 1.1 and 1.3 metres. Their centre is 0.4 metres above the owner. Area of Effect uses normal project scaling and never shifts these sweeps forward.

The recipe uses movement lock, windup, the shared SpinAround duration step and circle damage steps. See [Shared spin presentation](SpinAround.md) for the current character animation and blade-trail implementation. Its opt-in `RadialSweep` query rejects targets outside the ellipse; other circle steps retain their existing targeting/offset behavior. Movement is locked while committing to the attack. The existing dodge/dash system cancels active skills and releases the lock, forfeiting all unlanded hits. Weapon changes/disable also use normal cancellation. Cancellation does not refund mana or cooldown, and a new cast always begins with strike one.

The automatic EN/RU tooltip now recognises optional `ComboStrike`/`ComboStrikeCount` metadata on damage steps. It lists all three coefficients instead of deduplicating equal first/second hits, and explains dodge cancellation. Other damage descriptions retain their existing format. Direct-damage preview totals the complete 550% sequence through the existing recipe preview.

## PixelLab art

All art is PixelLab-authored. The 32×32 icon has a fully opaque shaded background. VFX remain transparent, with Point filtering, 24 pixels per unit and no compression/mipmaps. Animation playback follows cast speed.

| Asset | Effect/gallery | Source |
|---|---|---|
| Icon | `a1509972-0996-50ba-bf0f-bdde8fd23701` | job `cce51fbb-44c6-431f-a281-b9a8883fdd0a` |

The current Steel and Heavy PixelLab trails, all three character sequences, provenance and rebuild instructions are documented in [Shared spin presentation](SpinAround.md).

## Validation

42 tests passed across ConningTowerTests, TornEdgeTests, SkillAoeScaleTests, SkillDpsPreviewTests and SkillIconBackgroundTests. Coverage includes both-side targeting independent of facing, symmetric AoE expansion, elliptical corner rejection, wider finisher, compound hurtboxes, escape before finisher, crit math, sequence preview/timing, scheduler cancellation after zero/one/two landed hits, icon and EN/RU text.


Live verification at 480×270: enemies 1.8 metres left and right both received 100 + 100 + 350 = 550 damage. Distant and elevated enemies outside the sweep received zero. All three centered PixelLab sweep animations and EN/RU tooltips were inspected. Temporary preview helper removed, play mode stopped and autosave restored.
