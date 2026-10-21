# Thunder Hammer / Громовой молот

Two-handed hammer primary, registered in `2HHammerSkillPool`: no cooldown and no mana cost. One heavy overhead strike deals 100% weapon damage in a 2 × 1.3 metre rectangle in front of the hero. The base sequence takes 1 second at Attack Speed 1; impact is at 45% after the windup. Attack Speed scales the complete sequence, and Area of Effect expands the shared hit area and effect.

Every complete 150 current Armor grants 5 added flat lightning damage to this skill only, using the existing stepped StatLinks system: 149 Armor gives no bonus, 150 gives 5, 300 gives 10, and 900 gives 30. Increased/flat Armor modifiers count. Existing weapon damage channels remain intact. The armor link, base coefficient, timing and hitbox are editable data.

Physical damage and lightning are channels in the same snapshot and hit the same targets once, including compound hurtboxes. They share the normal critical roll. This preserves physical stun buildup and lets lightning contribute to the existing shock chance and threshold rules. The skill neither guarantees shock/stun nor adds a second physical hit. In particular, Armor adds damage without adding Shock Chance; a shock build still needs the normal chance/application stats.

The primary uses the shoulder hold and authored `overhead` hand animation. The revised PixelLab art uses a narrow steel-colored downward wake, a small ivory contact flash, thin angular cyan branches, and falling stone chips. The opaque icon depicts a square iron hammer striking slate with electricity along its contact edge. A standard Animator clip lets the recipe scale visual playback to attack speed; no independent damage pulse is attached to the VFX. Dodging before impact cancels both damage channels.

## PixelLab sources

| Asset | Source |
|---|---|
| Opaque 32×32 icon | job `aaf3d61c-8667-4373-a451-c66e9fc5acbc`, gallery `4cff7204-3175-5624-8bde-d750ddfb2278` |
| Strike: nine 64×48 frames, 30 fps | effect `5b9475b2-0200-4e49-9da6-06b8a47d567d`, step `a9e15e77-c221-4408-8348-efbe47013b3a`, export `6f279182-4273-4d1f-a972-b6a2c58f8072` |

Point filtering, 24 PPU, uncompressed FullRect sprites and no mipmaps. `ThunderHammerBuilder.Apply` imports the art, builds the clip/controller/prefab and recipe, registers the skill and updates English/Russian labels without changing existing asset GUIDs.

## Checks

ThunderHammerTests cover the free primary and registration, opaque icon and animation, armor breakpoints and scope, damage preview parity, modified armor and shared critical hits, facing and compound hurtboxes, AoE growth, real enemy physical stun buildup and lightning shock threshold, cancellation, and both locales.

All 15 skill-specific checks passed. Live Play Mode verification with the actual recipe confirmed two front targets each received one physical-plus-lightning hit, while targets behind and beyond melee range received none. The overhead windup, cut and discharge were captured and inspected. The temporary preview helper was removed, play mode stopped and autosave/time/background settings restored.

Final verification: Unity compilation succeeded and all 59 checks passed across ThunderHammerTests, ShockThresholdTests, SkillAoeScaleTests, SkillDpsPreviewTests, SkillIconBackgroundTests and HorizonCrossSectionTests.

Visual refresh: use ThunderHammerBuilder.RefreshArt to rebuild the visual assets without resetting gameplay data or localization.
Visual revision validation: Unity compilation succeeded; all 16 ThunderHammerTests and SkillIconBackgroundTests passed. New live captures confirmed the narrower wake and compact discharge at the hit location, with the same 1,1,0,0 target hit results. Preview helper removed and editor settings restored.
