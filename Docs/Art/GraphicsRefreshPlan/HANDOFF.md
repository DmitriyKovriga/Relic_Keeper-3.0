# Implementation-chat starter

Use this as the initial instruction in the implementation chat:

---

Implement the Relic Keeper graphics-refresh plan in
`Docs/Art/GraphicsRefreshPlan/PLAN.md`. Read it and the repository's AGENTS.md
first. The source snapshot is `source-audit.json`; recheck relevant files if the
checkout has changed. Use Graphify MCP for focused cross-file discovery, then
inspect the actual sources/assets. Follow the existing Unity bridge workflows.

The user wants better graphics and considers existing combat/movement adequate.
Preserve atmosphere and feedback work, all playable geometry and one-box loot
collection by the exit. Room pacing and boss staging remain user-owned. Audio,
combat timing, new reactive systems and normal-attack body clips are outside scope.

Start with G00–G01: capture current Mortfall Room 001 at native 480x270 and produce
three concrete art alternatives of that same view:

- A: monumental stone ruins;
- B: collapsed fortress;
- C: burial complex.

Keep the real terrain silhouette, camera/framing, actor scale, HUD and comparison
lighting stable. Show the original plus A/B/C and explain which small modular kit
each needs. Concepts are visual proposals, not proof of Unity implementation.
Do the preparatory work before asking for the user's art-direction choice. Do
not pick a style silently or implement all three. Once the user chooses, record
it and proceed with the selected kit/pilot within the authorized scope.

The wall rule and source tiles are shared across ten rooms. Preserve smart-wall
support, fixed-rule/GUID contracts, sprite pivots, collision and the accepted
detail layer. Test alternative base art in an isolated preview or scoped mapping
before changing shared assets. The new architecture must be visual only and
should reuse existing render/color/lighting and authoring facilities.

Produce real reusable pixel assets and a Room 001 pilot, not a flattened scene
illustration. Native world art is 24 PPU on the existing grid. Measure at 1x,
verify seams in motion, and keep a manifest of actual assets and placements.
Treat the UI art pass as a separate later branch after selecting the environment
language; preserve layout, controls, localization and current transaction cues.

Maintain `STATUS.md` for delivered work and `DECISIONS.md` for chosen direction.
Keep implementation evidence distinct from proposals. Run the narrow relevant
EditMode checks after Unity-imported changes and perform real Play Mode visual
checks. Preserve user edits and the main save. Record exact test counts and
verification boundaries; do not report old checks as fresh validation.

Begin with the actual baseline and three comparable concepts.

---

This file only provides a starter prompt. No separate chat was created or sent
a message by the planning task.
