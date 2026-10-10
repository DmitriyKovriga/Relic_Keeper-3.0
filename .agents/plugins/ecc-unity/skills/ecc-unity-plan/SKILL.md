---
name: ecc-unity-plan
description: Plan substantial Relic Keeper gameplay features, Unity tooling, migrations, or refactors by tracing runtime and serialized-data dependencies. Use for changes crossing systems or when the user asks for a plan.
license: MIT
metadata:
  origin: affaan-m/ECC planner, adapted as a Codex skill
---

# Plan a verifiable Unity change

Read [the project context](../../references/unity-context.md).

Translate the request into observable behavior and a few acceptance criteria.
Inspect the existing implementation, affected callers, ScriptableObjects,
prefabs/scenes, save representation and relevant tests. Resolve API or package
uncertainty with local source and official documentation before choosing a design.

Choose the smallest useful implementation slice and its dependency order.
Describe concrete files or symbols, behavior changes and verification. Call out
serialized/save migrations, runtime/editor boundaries and live visual checks
only when the feature needs them. Extend established patterns rather than
introducing a new framework to satisfy a generic architecture template.

For gameplay, consider only the relevant edge cases: boundaries in stat math,
empty or missing configuration, pooled reuse, death/despawn, scene transitions,
and save/load compatibility. For procedural generation, separate candidate
generation/validation from applying it, and preserve existing content on failure.
For UI, budget the complete 480x270 layout and localization before child sizing.

Give a short plan for ordinary multi-file work. A durable file under `Docs/`
is useful for a substantial design or when requested, not for every small edit.
If implementation is authorized, continue after the plan; do not add an approval
checkpoint for routine reversible work. If the user requests only planning,
deliver the plan without editing gameplay. Keep model choice and delegation
under the session's existing instructions.
