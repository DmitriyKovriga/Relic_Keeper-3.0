# Execution

Execution is an active dagger skill in the existing dagger skill pool. It deals
normal weapon damage in a forward rectangle and grants that hit **+10 percentage
points of Execution Threshold**. It uses the same attack-speed timing and zero
mana/cooldown baseline as Venom Strike, without Venom Strike's poison modifiers.

`ExecutionThreshold` is appended to `StatType`, preserving serialized values.
Only the base and Flat layers contribute. Thresholds add together and are clamped
to 0–100%. A successful direct hit must deal positive damage after mitigation and
leave the enemy strictly below `maximum health * threshold / 100`. Exactly 10%
health is not executed by a 10% threshold. Damage over time and zero-damage hits
do not execute. Bosses use the same rule. Death uses the existing enemy death,
loot, XP and kill-event pipeline.

The stat has Light/Medium/Strong Flat-only affix families, each with five tiers.
The Medium family is included in the physical one-handed weapon affix pool used
by Adventurer's Dagger. Its T1 roll is 1–2%; T5 is 4–6%. Existing items remain
unchanged. New rolls can get the affix. English and Russian names/descriptions,
stat labels and affix text use the existing localization tables.

Art was created through the PixelLab MCP:

- 32×32 skull-and-dagger icon: job `7ed7b108-3355-4731-b583-f8440d222b41`,
  gallery asset `f17ca422-1de4-5475-9d41-50145542866f`. Final PixelLab pixel-tool
  pass `f8d3393d-7e4c-41d1-829c-c1fdaf8cf720` adds an oxblood background, bone
  shading, steel highlights and ember eyes.
- Nine-frame, 56×32 steel crescent with a crimson wake and impact glint: effect
  `c5e6e112-9424-4086-a8a3-ee80b39a061f`, export
  `2f9ec639-18a8-4539-a0fa-5d5c73421f18`. The original spritesheet and JSON are
  retained beside the skill. Point-filtered, uncompressed sprites use 24 PPU.
  The 24 FPS clip is retimed by the existing attack-speed system to the recipe's
  8–36% VFX window. Its brightest edge arrives near the 14% damage step; the last
  frame is blank so the effect finishes cleanly.

`Tools > Skills > Build Execution` regenerates the Unity imports, animation,
prefab, skill/recipe and affix family registrations without replacing art or
creating duplicate pool entries. Balance lives in the recipe and
`AffixValueBalance`; skill descriptions read the scoped recipe modifier.
`Tools > Skills > Refresh Execution Art` updates only texture imports, animation,
prefab and the visual recipe step, preserving gameplay and localization data.
