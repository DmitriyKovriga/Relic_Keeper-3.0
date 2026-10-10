---
name: ecc-unity-verify
description: Verify completed Relic Keeper Unity changes with compilation, exact EditMode test filters, console errors, diff review, and live visual checks when needed. Use after implementation or when the user asks to validate a change.
license: MIT
metadata:
  origin: affaan-m/ECC verification-loop, adapted for Unity Bridge
---

# Verify against the real Editor

Read [the project context](../../references/unity-context.md).

Choose checks from the actual changed files. For gameplay/editor/Unity-imported
changes, run bridge operations sequentially from the project root:

```powershell
agents-unity-bridge get-status --timeout 15
agents-unity-bridge compile --timeout 60
agents-unity-bridge run-tests --mode EditMode --filter RelicKeeper.Tests.EditMode.CooldownRecoveryWordingTests --timeout 60
agents-unity-bridge get-console-logs --limit 20 --filter Error --timeout 30
```

The test above is a real example, not a universal smoke test: substitute the
exact relevant class after inspecting its source. Confirm nonzero test count and
passed/failed/skipped results. Diagnose compile failures before interpreting
tests. Inspect console timestamps/stack traces to separate existing errors from
new ones without clearing away evidence. Run nearby classes only when affected.

For UI or rendering changes, exercise the affected flow in Play Mode and inspect
at 480x270, including EN/RU text and live locale switching when relevant.
Check status before `play`, which toggles the current state. Preserve the user's
Editor scene/session; do not discard unsaved scene work to set up a check. If a
visual check cannot be completed, state that compilation and tests do not prove
layout or rendering correctness.

For skill/plugin/documentation-only changes, validate metadata, referenced paths,
installed discovery and the changed configuration instead of adding gameplay
tests. Do not run npm builds, generic secret greps or coverage tooling just to
populate an upstream six-phase report.

Review task changes with `git diff --check`, relevant staged/unstaged diffs and
new files. Confirm no unrelated assets, `.meta` churn or generated files entered
the patch. Do not stage, commit, publish or update memory as a verification step.

Report what changed and why, the checks actually performed with test counts,
and material limitations. Use clear pass/fail/not-run distinctions. A status
timeout means the Editor is unavailable for this check, not that the game is
broken or that validation passed. Complete independent work, avoid repeated
blind retries and report the command that still needs execution.
