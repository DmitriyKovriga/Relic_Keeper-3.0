# Rogue base-sprite options

Nine separate PixelLab Pro edits of the original rogue resting sprite. These
were prepared as design candidates. The user selected option 01 (Classic) for
the rogue; its animation integration is documented in `Docs/RogueMovementRefresh.md`.
Options 02, 04, 05 and 06 are favorites reserved for possible future characters.
The original 28 x 28 reference was padded with two transparent pixels on each
side to meet the model's 32 x 32 minimum. It was not rescaled.

`Comparison.png` presents options 01-09 at the same nearest-neighbor scale,
with the original reference and a native-size thumbnail for each candidate.
`Rogue-01.png` through `Rogue-09.png` are the original transparent AI outputs.
No generated pixels were manually painted or recolored.

| Option | Direction |
| --- | --- |
| 01 | Classic: closest polish of the original |
| 02 | Scout: fitted vest and practical wrist wraps |
| 03 | Scarf: exposed face and stronger scarf silhouette |
| 04 | Veteran: firmer face and worn gear |
| 05 | Soft cowl: loose cowl around the original hair |
| 06 | Short mantle: small rear shoulder covering |
| 07 | Light leather: cloth, leather and a chest strap |
| 08 | Tidy hair: swept fringe and a tiny tied-back section |
| 09 | Stealth cloth: quieter layered gray fabric |

`manifest.json` records generation IDs and requested directions. Keep the nine
original outputs unchanged for reuse; the game integration removes only the
transparent margin from option 01 and keeps its pixels at native resolution.
