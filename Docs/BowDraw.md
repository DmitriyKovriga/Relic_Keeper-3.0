# Bow draw and Simple Bow Shot

`bow_stance` now defaults to `bow_draw` (the appended BowDraw enum value 4; existing enum numbers are preserved). The swing is selectable in the Skills Editor and its arrow offset/pullback are editable in the Swing tab.

The bow remains visible. A visual-only arrow is created before windup, pulls back four native pixels while the bow moves back one pixel, and is replaced by the ordinary damage projectile at release. Its world position is used as the projectile origin, so release does not jump to an unrelated muzzle offset. Facing mirrors through the existing player hierarchy. Cancellation, disable, and switching styles hide the held arrow; the renderer is reused on subsequent casts.

Simple Bow Shot's recipe winds up from 0 to 55% of the attack pipeline, releases at 55%, and recovers through 85%. Timing follows the existing attack-speed system. The arrow uses the ordinary projectile damage, collision, projectile-count, speed, pierce, fork and chain systems. Mana/cooldown and bow damage values are preserved. The new BowSkillPool is assigned to Adventurer's Bow, so newly generated bows grant this skill.

Arrow art: PixelLab drawing `36f2b57f-4755-471f-b60b-a11a9350e969`, 16 x 7 pixels, right-facing, 24 PPU with point filtering. Assets are under `Assets/Resources/Skills/2HWeapon/Bow`. `Tools > Skills > Apply Simple Bow Shot` reapplies the authored setup; it is an explicit setup tool, not an automatic importer.

Verification: BowDrawTests cover mirrored pulling, visual-only windup, cancellation/reuse, stance selection, exact release position, projectile direction, and melee style switching. In Play Mode, `RelicKeeper.Editor.Skills.BowShotPreview.CaptureRuntimeViews` exercises the runtime hand animation and recipe launch in an isolated mirrored preview, samples arrow travel for display, and writes frames to `%TEMP%/codex-bow-shot`. This preview does not replace checking a full player cast in dungeon combat.
