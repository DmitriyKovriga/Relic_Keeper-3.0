---
name: ecc-unity-tests
description: Add meaningful NUnit EditMode regression tests for Relic Keeper gameplay math, state transitions, save compatibility, or editor algorithms. Use for behavior changes needing regression coverage or an explicit TDD request.
license: MIT
metadata:
  origin: affaan-m/ECC tdd-workflow, adapted for Unity Test Framework
---

# Test the behavior that matters

Read [the project context](../../references/unity-context.md).

Find the nearest existing tests and their fixture conventions. Tests belong in
`Assets/Tests/EditMode/Editor`, namespace `RelicKeeper.Tests.EditMode`, with
NUnit and the project's existing Unity Test Framework. Do not add a standalone
.NET, Jest, Playwright or PlayMode test setup for this workflow.

For a logic bug, prefer a small regression test that fails for the actual bad
input/state, then implement the fix and run the same class again. For new
testable behavior, define the expected result before implementation. Record a
real failing result when it can be run; unrelated compilation failures or an
unavailable Editor are not proof that a regression test reproduces the bug.
An explicit TDD request calls for the red/green cycle; otherwise an unavailable
Editor should not prevent independent implementation, but the result remains
unverified until the tests actually execute.

Cover boundaries relevant to the change rather than mirroring implementation:
modifier order and caps, eligibility/weights, save round trips, lifecycle state
transitions, deterministic generator invariants and preserving data on rejected
candidates. Use deterministic seeds and controlled time where needed. Avoid
flaky random samples when an exact probability helper can be asserted.

Clean up created Unity objects and restore mutated static/global state using the
fixture's teardown conventions. Test public behavior or existing test seams;
do not change stable serialized APIs merely to make testing easier.

After compilation, run the exact class filter with the bridge, substituting a
class that actually exists:

```powershell
agents-unity-bridge compile --timeout 60
agents-unity-bridge run-tests --mode EditMode --filter RelicKeeper.Tests.EditMode.CooldownRecoveryWordingTests --timeout 60
agents-unity-bridge get-console-logs --limit 20 --filter Error --timeout 30
```

Commands are sequential. Confirm executed count, failures and skips. Add nearby
regression classes when the dependency analysis warrants them. Broaden or repeat
only for a new change, failure or unresolved concern. Report behavior guaranteed
and any gap; use actual coverage only if configured and measured. There is no
new coverage quota or checkpoint-commit requirement. Configuration, documentation
and minor reversible presentation edits need proportionate checks, not artificial
new unit tests. UI/rendering still requires live visual validation when changed.
