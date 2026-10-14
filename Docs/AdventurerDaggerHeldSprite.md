# Adventurer dagger held sprite

The weapon had no InHandSprite. The dedicated held sprite is now assigned to the existing weapon asset; item ID, inventory icon, stats and skill pool are preserved.

The revision uses the user's enlarged original dagger as the actual PixelLab MCP edit_image_pixen input, rather than the rejected sword-shaped generated sprite. Job: 131478d4-77ed-4628-ba79-4961102f32ef. Gallery asset: b5c83656-aac8-597c-a96e-e047772880e5. The hooked reddish brown handle and curved, tapered steel blade follow that reference. The opaque silhouette is 5x12 pixels (previous revision 10x20), on a transparent 20x20 canvas. No painted hand is included.

Runtime asset: Assets/Scripts/Items/Weapon/LowTier/OneHandedWeapon/Daggers/Adventurer's Dagger/AdventurerDaggerHeld.png. Import: 24 PPU, point filtering, no compression/mipmaps, full rectangle. Native grip pivot: (12.5, 15.5) in bottom-left pixel coordinates. The blade-down reference art uses 180 degrees of tilt correction to match the existing reverse-grip stance. The weapon's vertical socket correction is 3/24 units, lowered from the previous 8/24 to align the hooked handle with the hand while clearing the floor.

Source: Docs/Art/Weapons/AdventurerDaggerHeldSource.png. Rejected previous versions are retained alongside it. Reimport with AdventurerDaggerVisualBuilder.Apply. Controlled Play Mode preview uses WarriorMovementPreview.CaptureRogueRuntimeViews with the actual dagger stance and both facings.

Validation: compilation passed; RogueMovementAnimationTests 8 passed, 0 failed/skipped. Controlled 480x270 Play Mode captures inspect both facings in run, jump, fall and landing with the actual dagger stance. Runtime preview: Docs/Art/Weapons/AdventurerDaggerHeldRuntime.gif.
