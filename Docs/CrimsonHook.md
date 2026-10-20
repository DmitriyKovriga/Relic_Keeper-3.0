# Crimson Hook

Two-handed axe special: 5-second cooldown, 10 mana. Registered in
`AxeSkillsPoolRB` (used by Axe of Settler) and the item database, with EN/RU
labels and automatic descriptions.

Initial editable tuning in `Step_CrimsonHook`: 100% weapon damage, 6 m reach,
1.5 m width, 0.35-second pull, 0.2 m gap between player and enemy bodies.
Area of Effect scales the width only; reach remains fixed. Enemies are hit
once each, including those with multiple hurtboxes. Solid terrain blocks the
grapple and also clips displacement if an obstruction appears during a pull.

Only successful hits pull. The normal attack pipeline handles weapon damage,
criticals, ailments, and hit effects. Ordinary pushback is disabled for this
skill so it does not push targets away before pulling them in.

Heavy targets use existing PushbackResist: the initial immunity threshold is
70%, covering Knights. Below the threshold, resistance reduces pull distance.
Bosses, explicitly anchored enemies (`Movement.PullImmune`), frozen targets, and static/fixed/unsimulated bodies
take damage but remain in place. Pulls preserve vertical movement, stop short
of the caster, and clear on freezing, disabling, forced stop, or reinitialization.
New knockback replaces a pending pull.

Zero walking speed alone does not prevent pulling: Mold is stationary AI but
can be displaced on solid and one-way platforms. The training dummy explicitly
sets `PullImmune` to remain anchored.

## PixelLab artwork

All new art was generated through PixelLab MCP, with runtime movement of the
authored hook and tiled tethers:

- Icon: gallery `eac1fce0-794e-5b9b-b208-4f9f0d9e2fdc`, job `4fbd2f02-b8a9-4abe-9176-fd96b591662e`.
- Hook blade: gallery `29c2b3ca-d352-5c5a-b476-f98cbe60ca5a`, job `efbc4f7a-7e67-4a00-a789-7c17cbc60c8d`.
- Crimson tether: gallery `3f7878ba-4044-5ead-b944-3ce8a2a5ab3c`, job `2ccb3284-691e-4dbc-8b55-7f3f5ada39b0`.

`Tools/Skills/Build Crimson Hook` rebuilds the imports, visual prefab, step,
recipe, skill, pool/database registrations and localized labels.

Tests: `RelicKeeper.Tests.EditMode.CrimsonHookTests`. Live verification at
480x270 confirmed two Zombies gathered near the player, a Knight damaged
without displacement, an immediate 10-mana cost, and EN/RU tooltip layout.
