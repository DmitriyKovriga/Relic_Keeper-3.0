# Warrior selected outfit

Selected: round 2 option 05, `Docs/Art/WarriorVariantsRound2/Warrior-05.png`. Original head pixels were protected by an inpainting mask during outfit generation. All candidates remain preserved.

The accepted calm run and five takeoff poses are reskinned through PixelLab `transfer_outfit`, job `a91b4bd6-e4ab-4801-af5e-8d5d44197a50`, seed 35105. Sources and all generated frames are preserved in `SelectedAnimation/`. Fall holds the final jump pose; double jump restarts at frame 3. Existing timing and landing compression remain unchanged.

The PixelLab reference uses a 32x32 padded canvas. Runtime import removes only its transparent two-pixel border, retaining 28x28 at 24 PPU with point filtering and uncompressed full rectangles. Any remaining transparent bottom padding is shifted above the figure; opaque pixels are never scaled or clipped. Frame-specific hand anchors keep equipment attached.

Rebuild using `RelicKeeper.Editor.VFX.WarriorOutfitVisualBuilder.AssembleGeneratedFrames`. Existing animation texture GUIDs, character identity, starting stats and passive tree remain preserved. The selected base replaces the portrait, first run frame and landing rest pose.

Validation: compilation passed; 14 Warrior, 9 Mage and 8 Rogue movement tests passed. Controlled Play Mode captures exercise bow, axe, forward jump, double jump and both facing directions. The original Apply Refined Warrior Movement menu also delegates to the selected outfit profile while its base asset is present.
