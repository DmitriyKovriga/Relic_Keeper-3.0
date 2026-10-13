# Warrior outfit variants, round 2

Requested: keep the face and hairstyle closer to the original or round 1 option 06.
These nine PixelLab masked inpainting jobs vary the clothing below the head.
The black region of `OutfitMask.png` freezes the source pixels; white pixels are
the editable outfit region. All rows above 13 and the central head rectangle
x=11..24, y=13..18 are protected. No generated pixels are manually repainted.

Options 01, 03, 05, 07 and 09 use `OriginalReference.png`.
Options 02, 04, 06 and 08 use `Variant06Reference.png` from the first round.
References, mask, native transparent outputs, seeds and job IDs are preserved.
Round 2 option 05 was selected and integrated into the Warrior. The first round
and all remaining candidates stay preserved. Movement sources and previews are
in `SelectedAnimation/`; integration notes are in `Docs/WarriorOutfitRefresh.md`.
