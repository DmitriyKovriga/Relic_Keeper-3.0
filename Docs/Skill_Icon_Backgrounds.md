# Skill icon backgrounds

All assigned skill icons have opaque backgrounds. The audit covered every `SkillDataSO` in Assets: 61 skill references, 60 unique icons. Five icons had transparency and were replaced in place; the other 55 unique icons were unchanged.

The PixelLab pixel-art workbench painted only the transparent pixels, with a dark shaded backplate suited to each icon. Every original opaque foreground pixel was checked for exact RGBA preservation. Canvas dimensions, sprite GUIDs/local IDs, import settings and all existing skill references remain unchanged.

| Icon | PixelLab output | Preserved foreground pixels |
|---|---|---:|
| Needle Prick | `3d2dc0cc-bf65-44dd-ad2c-e43e566237f1` | 735 |
| Mana Spear | `26af8a47-4b03-46c1-aa53-a53c2ed150bd` | 302 |
| Circular Saw | `dc116ed6-8c74-4fc8-a20e-5ea8b7865d21` | 353 |
| Retreating Thrust | `b0aa4676-45ae-49c1-926b-415a032cbf96` | 466 |
| Torn Edge | `64609801-7ee5-4ef5-927e-143e59940143` | 508 |

Use `Tools/Skills/Audit Icon Backgrounds` to export the current assigned sprites and an alpha report to the ignored `.agents-unity-bridge/SkillIconBackgrounds/` folder. `SkillIconBackgroundTests` checks that every assigned icon is fully opaque, including sprites within larger source textures. Empty skill icon assignments are outside this background-only change.

New skill art should include its background in the icon asset. VFX/projectile textures retain their intended transparency.

Validation: the focused EditMode test passed across all 60 unique assigned sprites. All five replacements were reviewed in live Play Mode using UISkillSlot at 32×32 and enlarged 2×. No sprite metadata changed; preview objects/helper were removed and autosave restored.
