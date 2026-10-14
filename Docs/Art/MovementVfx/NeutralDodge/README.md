# PixelLab MCP neutral dodge clips

Generated via the configured PixelLab MCP endpoint using tools/call, not the built-in image generator and not rigid Unity sprite rotation. Exact native base sprites are centered on transparent padded canvases without rescaling before upload. Raw returned frames are stored unchanged under each hero folder. Runtime import packs them without painting, rotating or regrounding individual frames.

- Warrior: animate_image, job cfddb69c-f305-4161-a230-64d798363446, gallery asset c6c85b00-fd57-5baa-b4de-9d3c946d223b.
- Rogue: animate_image, job 06fc21e8-e215-48a2-8f32-81817db9f339, gallery asset 2ac12b14-c0e1-5547-a43c-9dd28e2212ae.
- Mage accepted retry: animate_image_pixminimax, job 9fd7daf6-6ddd-4bb3-8a65-24f50f7bd01d, gallery asset 8247b0b6-a2e6-5af4-a721-7711addf954f.
- Mage rejected first attempt: animate_image, job acc51230-7c8e-47fb-a8df-a23c5c8e3cbb. It hopped instead of turning; archived in MageRejectedHop.zip and not used by the game.

All jobs request eight generated frames, transparent background, and the same native reference as both first and last target. PixelLab returns the input plus eight generated frames. Native inputs: warrior/rogue 28x28 padded to 40x40; mage 32x40 padded to 56x56. No gameplay timing or balance changed.

Motion prompt: one complete quick evasive somersault in place around torso center; crouch and tuck both knees, turn through diagonal/horizontal compact poses and upside down with feet above head, uncurl and return to the exact upright starting pose. Empty hands close to torso, no weapon or particles, no horizontal travel. Preserve face, hair/hat, outfit, proportions, native pixels and palette. Dynamic authored poses rather than rigid rotation of a standing image. Mage retry used the shorter explicit full forward somersault prompt with side view.
