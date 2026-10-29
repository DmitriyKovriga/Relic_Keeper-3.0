# Relic Keeper — whole-game juice roadmap

Date: 2026-10-29. Revision 2: corrected centralized loot; J07/J08 user-owned.
Status: **planning proposal, no gameplay implementation**.
Review UI is Russian; this handoff is English. Scope: extend the feel of the whole
run after the atmosphere and combat/movement passes, without reopening completed
work or assuming that every possible polish feature is worth its upkeep.

## Direction

Working direction for discussion: a grounded, dangerous world; precise player
control; rewards and meaningful power changes that are easy to perceive.
Extend feedback across action -> consequence -> reward -> build change -> next
challenge. This is a design hypothesis, not a new confirmed visual-style mandate.

The highest-priority candidate slice is **J01 + J02 + J03 + J06**, implemented
incrementally. It covers rewards, transactions, skill states and room resolution.
After each slice, compare a normal run. A currently clear interaction may need no
new effect. Do not commit to all eight tracks or sum their prototype estimates.

J07 encounter rhythm and J08 elite/boss staging are excluded from this roadmap:
the user will handle them independently. Keep their IDs reserved; they are not
later implementation phases or prerequisites for the remaining proposals.

## Evidence and boundaries

- Atmosphere work is accepted as completed by the user. The atmosphere plan records specific implemented pilots and skipped options; this does not imply every biome has identical coverage.
- CombatMovementPlan/STATUS.md dated 2026-10-29 reports delivery of body-follow, enemy recoil, hit highlight, hit-stop, camera, hurt/defense/death and movement/input work. It reports 369 selected regression tests passed; those are historical results, not tests run for this new plan.
- Generic contact bursts (C05) were removed by user decision. Do not reintroduce them as a prerequisite. Audio remains deferred (C08); target-material variants remain optional (C06); authored attack-body key poses remain excluded (C03).
- Correction from user and verified delivery path: room loot is stored in one container by the next-room portal via WorldItemDropService -> RoomLootCollector.TryStore -> WorldLootCache. Storage precedes the existing cosmetic flight. Ground-drop hover/rarity visuals are a fallback and must not be treated as the normal mob-loot loop. Experience/currency pickups are separate existing systems.
- J01 preserves central collection and convenience. Improve intake, arrival and bounded accumulation cues; do not scatter loot, require individual ground pickup or make container access wait for effects. J07 encounter rhythm and J08 boss/elite staging are user-owned and removed from the agent roadmap.
- Crafting already refreshes UI and opens the crafted-item tooltip. Level-up already flashes the body, raises an arrow and shows a banner. HUD health/mana have increase/decrease presentation. These must not be sold as missing systems.
- Enemy attack warnings, status presentation and defense feedback exist. Quality/readability at normal speed is not proven by class names or source inspection.
- Room portal unlock and full clear are separate: RequiredPortalKills is max(0, (enemyCount + 1) / 2). Existing banner methods distinguish these outcomes. Reward modifiers decide what actually appears; not every room grants a chest.
- Death overlay already shows highest stage and kill count. Its message interval is six seconds in inspected source, followed by a covered-frame return sequence. No killer-attribution or retained-reward system is assumed.

Evidence is a focused Graphify discovery followed by source reads and existing
delivery reports. No new live gameplay recording, Unity compilation, runtime test
or hardware profile was performed for this document. Existing reports are evidence
of their stated scope only. Old unchecked production-roadmap boxes are not proof
that a feature is still absent. Source hashes in plan.json pin the inspected files.

## Cost model

Gain, setup and upkeep use subjective 1–5 scales (higher = more). Prototype days
are rough person-days for a narrow representative slice with existing art and
familiar project code, including basic verification; production coverage and new
art can cost substantially more. They are not measured results or commitments.
Upkeep means extra work for each new item, skill, enemy, biome or UI state.

## Order and decision gates

1. Record 10–15 minutes of an ordinary run: combat, loot, crafting/equipment,
   cooldown/failed input, exit/clear, then death/return. Identify unclear events;
   do not use another combat-only stress clip as the entire baseline.
2. Prototype J01/J02/J03/J06 separately. J06 is self-contained; J01's cache intake
   can coordinate with it, but neither must wait for a global presentation service.
3. If gaps remain in cause/effect and enemy readability, try J04/J05; audit J10
   during repeated attempts. Room composition and boss staging remain user-owned.
4. Only then consider one J09 reactive-prop pilot. Expand only if the
   effect remains noticeable in ordinary gameplay and justifies content upkeep.

## Candidate workstreams

### J01 — Exit loot-container feedback

Stage: first. Expected gain 4/5; setup 2/5; upkeep 2/5. Narrow prototype: 2–4 person-days. Boundary: presentation.

Preserve the room's single container near its exit. WorldItemDropService routes to RoomLootCollector.TryStore: the real rolled item is inserted before a cosmetic flight starts; do not delay ownership or interaction until arrival. Extend the existing flight and cache visuals, not loose ground pickup or a new reward chest. Prototype a brief visual-only intake response and a bounded valuable-arrival accent. A cosmetic arrival hook and small presentation metadata may be needed: Spawn currently accepts only start/target and its completion does not notify the cache. Keep those separate from inventory transfer, Changed events and reward creation. Cancel safely when the box/room disappears or its contents have already been taken. Do not show new-loot celebrations for player-discarded items or ordinary UI refreshes; carry an origin flag only if needed to distinguish them.

Candidate treatments (not a mandatory bundle):

- Start here: brief visual-only intake compression/opening and inward light. Merge frequent arrivals; do not move the collider or interaction target.
- Second accent: one rarity-qualified arrival pulse with shape as well as color. Rate-limit it; rarity is not an upgrade guarantee. Hidden and player-discarded items never trigger the rare-drop celebration.
- Optional: two or three bounded accumulation states driven by filter-visible contents, recomputed after removal/filter changes. No implied capacity, endless brightness growth or changes to interaction size.
- Optional: tune the existing flight arc and trail convergence. Cap decorative streams during bursts; representation may be grouped while every actual item remains stored exactly once. Avoid a new offscreen VFX framework.

Acceptance: There remains exactly one room container, with the same item identities/count and location policy. It remains usable while flights run or the portal is locked. A burst of twenty items merges feedback without queuing twenty animations. Hidden items cause no flight, rarity accent or accumulation change; changing the filter recomputes visible state without claiming a new drop. Taking all loot or leaving the room mid-flight loses/duplicates nothing and leaves no orphan effect. Test full inventory and simultaneous arrival while the cache UI is open.

Source touchpoints (inspect callers and actual serialized wiring before implementation):

- [Assets/Scripts/Items/World/WorldItemDropService.cs](../../../Assets/Scripts/Items/World/WorldItemDropService.cs)
- [Assets/Scripts/Items/World/RoomLootCollector.cs](../../../Assets/Scripts/Items/World/RoomLootCollector.cs)
- [Assets/Scripts/Items/World/WorldLootCache.cs](../../../Assets/Scripts/Items/World/WorldLootCache.cs)
- [Assets/Scripts/Items/World/WorldLootCacheFlightVisual.cs](../../../Assets/Scripts/Items/World/WorldLootCacheFlightVisual.cs)
- [Assets/Tests/EditMode/Editor/RoomLootCollectorTests.cs](../../../Assets/Tests/EditMode/Editor/RoomLootCollectorTests.cs)

### J02 — Upgrade transactions

Stage: first. Expected gain 4/5; setup 2/5; upkeep 2/5. Narrow prototype: 2–4 person-days. Boundary: presentation.

Prototype one equipment swap, one orb operation and one passive allocation using committed results. Keep existing crafted-item tooltip and level-up feedback. For randomized affixes show actual before/after changes without claiming a universal better/worse result. Do not add a broad stat-diff engine for the whole character in the first slice.

Acceptance: Successful and rejected operations look different. Repeated crafting does not queue animations or move the pointer target; displayed differences match the committed item, including removed or worsened affixes. Keyboard navigation and both locales remain usable.

Source touchpoints (inspect callers and actual serialized wiring before implementation):

- [Assets/UI/Inventory/InventoryUI.Crafting.cs](../../../Assets/UI/Inventory/InventoryUI.Crafting.cs)
- [Assets/UI/PassiveTree/PassiveTreeUI.cs](../../../Assets/UI/PassiveTree/PassiveTreeUI.cs)
- [Assets/Scripts/Visuals/PlayerLevelUpFeedback.cs](../../../Assets/Scripts/Visuals/PlayerLevelUpFeedback.cs)

### J03 — Skill state feedback

Stage: first. Expected gain 4/5; setup 2/5; upkeep 1/5. Narrow prototype: 1–3 person-days. Boundary: presentation.

Audit existing state cues, then add only missing transitions. Read actual cast acceptance/rejection and current cooldown/mana; do not infer success from raw input. A structured rejection reason may need a small runtime extension. Avoid ready pulses for zero-cooldown primaries, initialization, weapon swaps and mere HUD rebuilds. Do not change input buffering, mana, cooldown or cancellation rules.

Acceptance: No accepted-cast cue on rejection. No ready cue on opening a menu or restoring a swapped weapon. Repeated denied presses remain bounded and communicate the correct reason; meaningful long-cooldown readiness is visible without watching a timer continuously.

Source touchpoints (inspect callers and actual serialized wiring before implementation):

- [Assets/UI/SkillSlots/UISkillSlot.cs](../../../Assets/UI/SkillSlots/UISkillSlot.cs)
- [Assets/UI/PlayerHUD/HUDController.cs](../../../Assets/UI/PlayerHUD/HUDController.cs)
- [Assets/Scripts/Skills/PlayerSkillManager.cs](../../../Assets/Scripts/Skills/PlayerSkillManager.cs)
- [Assets/Scripts/Skills/Logic/SkillBehaviour.cs](../../../Assets/Scripts/Skills/Logic/SkillBehaviour.cs)

### J04 — Build cause and effect

Stage: second. Expected gain 5/5; setup 3/5; upkeep 3/5. Narrow prototype: 3–6 person-days. Boundary: presentation.

Audit three existing chains before authoring. Present application, sustained state and meaningful transition with distinct bounded cues, using actual status/resource events. Preserve the implemented defense response and its ownership. Do not present DoT ticks as fresh direct hits, show one effect per poison stack, invent a fifth hit-damage channel or add a new proc mechanic.

Acceptance: In an unlabelled clip the reviewer can distinguish application, ongoing effect and major transition. Ten afflicted enemies and repeated procs do not obscure attack intent. A fully readable existing chain requires no new effect.

Source touchpoints (inspect callers and actual serialized wiring before implementation):

- [Assets/Scripts/StatusEffects/StatusEffectPresentation.cs](../../../Assets/Scripts/StatusEffects/StatusEffectPresentation.cs)
- [Assets/Scripts/StatusEffects/ShockVisualController.cs](../../../Assets/Scripts/StatusEffects/ShockVisualController.cs)
- [Assets/Scripts/StatusEffects/IgniteVisualController.cs](../../../Assets/Scripts/StatusEffects/IgniteVisualController.cs)
- [Assets/Scripts/Stats/MysticShieldController.cs](../../../Assets/Scripts/Stats/MysticShieldController.cs)

### J05 — Enemy intent and recovery

Stage: second. Expected gain 5/5; setup 3/5; upkeep 3/5. Narrow prototype: 3–5 person-days. Boundary: presentation; timing redesign needs separate balance approval.

Choose two enemy families and compare existing anticipation/active/recovery at normal speed and in crowds. Adjust presentation against existing authoritative attack windows. Use timing and silhouette, not color alone. Preserve gameplay commitments and existing hit-recoil ownership; new stagger or longer recovery belongs to a separate balance proposal.

Acceptance: The player can identify the dangerous action and existing recovery window at 480x270 in both effect settings. Telegraphs do not claim an opening that gameplay does not offer. If the current timings are themselves the problem, stop treating that as cosmetic work.

Source touchpoints (inspect callers and actual serialized wiring before implementation):

- [Assets/Scripts/Enemy/EnemyAttackTelegraphVfx.cs](../../../Assets/Scripts/Enemy/EnemyAttackTelegraphVfx.cs)
- [Assets/Scripts/Enemy/EnemyAnimationBridge.cs](../../../Assets/Scripts/Enemy/EnemyAnimationBridge.cs)

### J06 — Room resolution and exits

Stage: first. Expected gain 4/5; setup 2/5; upkeep 1/5. Narrow prototype: 1–3 person-days. Boundary: presentation.

Extend current portal activation and banner handling; keep unlock and clear separate. Prefer a local portal cue, with optional brief nonblocking navigation hint only if the exit is offscreen. Coordinate overlapping messages rather than stacking banners. Use only rewards already authorized by the room modifiers. Preserve the kill threshold and reward count; never delay portal interaction until cosmetic completion.

Acceptance: Unlock can be recognized while enemies remain alive. Full clear, unlock and level-up coinciding do not overwrite essential information. Empty rooms and no-reward rooms are truthful; returning to a room does not replay its reward or unlock celebration.

Source touchpoints (inspect callers and actual serialized wiring before implementation):

- [Assets/Scripts/Dungeon/RoomController.cs](../../../Assets/Scripts/Dungeon/RoomController.cs)
- [Assets/Scripts/Dungeon/DungeonPortal.cs](../../../Assets/Scripts/Dungeon/DungeonPortal.cs)
- [Assets/UI/Dungeon/RoomClearedBanner.cs](../../../Assets/UI/Dungeon/RoomClearedBanner.cs)

### J09 — Selective world reactions

Stage: later. Expected gain 3/5; setup 3/5; upkeep 4/5. Narrow prototype: 3–5 person-days. Boundary: presentation.

One biome, one or two visual-only prop families. Verify that an existing prop is not already reactive before adding behavior. Reactions never change collision, traversal, rewards or targeting; no physics simulation on every decoration. Author distance limits and disable/reset behavior. Expand only after an A/B clip shows meaningful benefit during ordinary movement.

Acceptance: A nearby reaction is perceptible without distracting from enemies. Reentry and reused objects restore correctly; crowded rooms do not update every offscreen prop. Stop the pilot if the benefit is mainly visible in a close-up demonstration.

Source touchpoints (inspect callers and actual serialized wiring before implementation):

- [Docs/Art/VisualAtmospherePlan/PLAN.md](../../../Docs/Art/VisualAtmospherePlan/PLAN.md)
- [Docs/Art/Environment_Animations.md](../../../Docs/Art/Environment_Animations.md)
- [Docs/Art/Ambient_Particles.md](../../../Docs/Art/Ambient_Particles.md)

### J10 — Run ending and return

Stage: second. Expected gain 4/5; setup 2/5; upkeep 2/5. Narrow prototype: 2–4 person-days. Boundary: presentation.

Extend the current death overlay and return flow only where a normal repeated-attempt test exposes friction. Add a safe user-paced continuation/skip if desired, retaining the existing covered-frame transition and exactly-once callback. Show only verified existing persisted progress. Do not invent killer attribution, currency retention, unlocks or full-run telemetry.

Acceptance: First and repeated deaths are readable and responsive. Early/repeated continue input cannot run the transition twice or reveal an unloaded room. Statistics match the existing run state, and EN/RU layout fits the logical canvas.

Source touchpoints (inspect callers and actual serialized wiring before implementation):

- [Assets/UI/DeathScreen/DeathScreenOverlay.cs](../../../Assets/UI/DeathScreen/DeathScreenOverlay.cs)

## Presentation rules and evaluation

- Order competing feedback: imminent danger > player defensive state > important
  confirmed action/resource change > rare reward > routine reward > ambient motion.
  Do not hide any essential warning when decoration is denied.
- Match amplitude to significance and frequency. Routine actions remain quiet;
  rare transitions may carry more weight. Never repeat a ready cue each frame or
  celebrate every DoT tick as a weapon hit.
- Keep input and authoritative outcomes immediate. Animations confirm results;
  they do not hold items, mana, portal access or save writes hostage. Preserve
  current body/weapon separation, collision and input rules.
- Use shared profiles per semantic event, with local ownership/reset. Extend
  existing paths before introducing a generic event bus, tween framework or
  dependency. No global presentation-manager rewrite is a prerequisite.
- Respect 480x270/24 PPU, pixel stability, tooltip readability and EN/RU. Keep UI
  outside world color/camera effects. Avoid whole-window elastic motion.
- Existing Reduced combat effects is not a universal graphics setting. Audit
  each new path explicitly; routine semantic cues still work when decorative
  motion is reduced. Do not claim existing settings automatically cover new FX.
- Validate repeated events, full inventory, failed/rejected actions, weapon swaps,
  overlapping clear/level-up, pause, room transition and pooled reuse where relevant.
- Use identical route/build/events for A/B; show normal-speed clips and test muted
  as audio is outside scope. Ask what happened and why before explaining it.
  Keep an effect only if it improves recognition, satisfaction or flow without
  more missed warnings or longer compulsory waiting. These are evaluation criteria,
  not a claim that any proposed track has already passed.
- Runtime implementation requires the narrow meaningful EditMode checks and live
  Play Mode visual review under project instructions. This document changes none
  of the runtime, saves, assets or balance data.

## Deliberately deferred

Audio; target-material catalogs; authored normal-attack body clips; generic impact
bursts; full destructibility; full stat/proc telemetry; mandatory introductions;
automatic camera zoom/slow motion; global stagger/lunge/input-rule redesign.
These are scope decisions, not claims that such features could never be useful.

## Primary references

The specific priorities, scores and implementations above are project-specific
design judgments; the references inform general principles only.

- [Jeremiah Reid — Juice Your Turns](https://juiceyourturns.com/). Read the author's public slide text, not the linked full video. Small changes in motion, scale and color can improve interaction; presentation should not block actions.

- [Chris Bourassa — A Torch in the Dark](https://media.gdcvault.com/gdc2016/Presentations/Bourassa_Chris_a%20torch%20in.pdf). Read the primary 19-page slide deck; not the full talk. A coherent creative direction helps a small team choose valuable features and cut expensive distractions. This is a general principle, not evidence for specific Relic Keeper gains.
