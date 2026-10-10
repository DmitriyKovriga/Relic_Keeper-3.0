---
name: ecc-unity-review
description: Review Relic Keeper Unity changes for concrete gameplay regressions, serialization breakage, lifecycle bugs, editor leakage, and relevant performance or UI issues. Use for requested reviews and substantial implementation self-review.
license: MIT
metadata:
  origin: affaan-m/ECC code-reviewer, adapted as a Codex skill
---

# Review with concrete evidence

Read [the project context](../../references/unity-context.md).

Inspect the requested diff, surrounding code, callers, data and relevant tests.
For uncommitted work, include staged, unstaged and new files and distinguish the
task's changes from pre-existing edits. Do not silently review a previous commit
as a substitute for an empty current diff. Keep review-only work read-only unless
the user also requests fixes.

Check the areas touched by the change:

- Gameplay correctness: calculation order, eligibility, caps, state transitions,
  runtime/preview consistency and save/load behavior.
- Serialization: existing field/enum identities, GUID/meta pairing, asset
  references, prefab overrides, migration and unintended YAML changes.
- Lifecycle: subscriptions, coroutine/async ownership, pooled reset, destroyed
  Unity objects, scene unload, static state and Addressables release ownership.
- Runtime/editor boundaries: `UnityEditor` references or debug-only UI entering
  the player build; Unity API use from unsupported threads.
- Hot paths: new allocations, LINQ/lookups or scans per frame and spawn bursts,
  supported by the call path or profiling evidence. Do not demand immutable
  copies of mutable gameplay objects as a universal style rule.
- UI/rendering: 480x270 bounds, pixel sharpness, EN/RU strings, optional FX,
  shared-material/property-block ownership and UI exclusion where relevant.
- Input/save/network security only where a real boundary exists. Ordinary loot
  RNG is not cryptography; web-specific OWASP checks do not apply automatically.

A finding needs an exact file/line, a concrete trigger and incorrect outcome,
and evidence that existing guards do not handle it. Prioritize user-visible
bugs and data loss; omit speculative warnings, arbitrary file-length rules and
style preferences. Zero findings is a valid result. Give actionable findings in
severity order, with relevant verification limits. Self-review is not an
independent-agent review; do not claim independent review unless one occurred.
