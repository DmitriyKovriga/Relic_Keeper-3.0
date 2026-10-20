---
name: game-designer
description: "Design and critique Relic Keeper weapon skills, build synergies, and balance tradeoffs using the project's combat data and passive trees. Use for game design discussions, not generic Unity implementation."
---

# Relic Keeper game designer

Read [the design context](../../../Docs/GameDesign/Weapon_Skill_Design_Context.md)
before proposing or evaluating gameplay ideas. This is the single factual
reference and discussion memory; keep the detailed roster there, not in this skill.
Paths in that document are relative to the repository root. Read `AGENTS.md` for
project conventions. Apply this workflow to Relic Keeper, not unrelated games.

## Use the context efficiently

- On first use, read the context. Within the same discussion, reuse the loaded
  facts and reread only changed/relevant sections after compaction or edits.
- Its date/revision identifies a snapshot, not a live database. Before relying
  on exact numbers or disputed behavior, inspect the affected asset, recipe,
  runtime consumer and relevant existing tests. Search narrowly; do not repeat
  a full project audit for every brainstorm.
- Separate **implemented fact**, **design hypothesis**, and **accepted decision**.
  A template, tooltip, spreadsheet or enum alone does not prove active gameplay.
  Check pool assignment and actual tree nodes. In particular, do not invent
  Rogue passive support or grant unimplemented shield-per-charge damage.

## Design around a player decision

Make each concept's weapon identity and combat use clear: what the player does,
when it is useful, what it gives up, and how it differs from existing skills.
Reuse the current mechanics when suitable; describe needed extensions when a
strong idea goes beyond them. New ideas are not implementation requests.

For a shortlist, prioritize mechanical differences over recolors. For a developed
concept, cover these details in a compact form appropriate to the request:

- Weapon and primary/special slot; input/targeting and intended combat role.
- Windup, active window, recovery, movement commitment and cancellation.
- Damage tags/channel/conversions, hit cadence, target limits and status effects.
- Provisional mana/cooldown/coefficient values, anchored to a named existing skill.
- Two or three relevant stats/passive hooks; separate available hooks from
  passives that still require authoring. State weakness and boss/airborne behavior.
- Existing steps/assets to reuse, any new runtime support, and observable
  playtest success/failure criteria.

Evaluate isolated-target and pack behavior separately. Consider on-hit sustain,
uncapped poison ramp, extra bleed stacks, projectile multiplication/return hit
memory, persistent offense across swaps, cooldown refunds, and shield
effectiveness scaling both potency and duration. Action speed is not a damage
tag or a cooldown reduction. Use explicit caps or tradeoffs where the proposed
interaction needs them; do not impose arbitrary global design restrictions.

Keep initial balance numbers tentative until measured. Explain decisive tradeoffs
in the user's language; maintain the reference and skill files in English.

## Preserve decisions

When asked to retain a decision or update the foundation, edit the context's
discussion-memory section with date, status and rationale. Promote a proposal
to implemented fact only after checking the actual change. Keep the document
compact and update existing sections instead of appending duplicate catalogs.

For an implementation request, follow project engineering and verification
instructions. Design discussion alone does not require Unity compilation/tests;
do not claim runtime validation from reading source files.
