---
name: unity-bridge
description: Use the installed agents-unity-bridge CLI to inspect Relic Keeper's live Unity Editor, compile, run filtered EditMode tests, read bounded console logs, or inspect assets. Use when real Unity Editor state or validation is needed.
---

# Relic Keeper Unity Bridge

Use the existing CLI from the project root. The Unity package and CLI are
already installed; inspect `agents-unity-bridge --help` before using unfamiliar
commands. Do not reinstall/update the bridge as a routine validation step.

```powershell
agents-unity-bridge get-status --timeout 15
agents-unity-bridge compile --timeout 60
agents-unity-bridge run-tests --mode EditMode --filter RelicKeeper.Tests.EditMode.CooldownRecoveryWordingTests --timeout 60
agents-unity-bridge get-console-logs --limit 20 --filter Error --timeout 30
```

Replace the example test class with the narrowest relevant class that exists.
Confirm nonzero executed tests and passed/failed/skipped counts. Run operations
sequentially: the bridge uses one project command channel. Keep output bounded
with filters/limits and retain exit codes rather than piping errors away.

The Editor must be open with this project and responsive. If status times out,
check process/state once, complete work that does not depend on the Editor, and
report the unavailable validation. Do not queue compile/tests behind a stalled
status request or claim success from a stale response file.

For focused asset inspection, use commands such as:

```powershell
agents-unity-bridge search-assets --query Player --type Prefab --limit 10 --timeout 30
agents-unity-bridge get-asset-info --asset Assets/UI/Inventory/PixelArtPanelSettings.asset --timeout 30
agents-unity-bridge dump-asset --asset Assets/UI/Inventory/PixelArtPanelSettings.asset --timeout 30
```

Read-only dependency analysis can help before asset changes. An unused-asset
candidate is not proof an asset is safe to delete: Addressables and runtime
loading can be additional roots. `dump-asset` on `.unity` only describes the
currently open scene. Preserve scene/prefab work and existing GUIDs.
`play` and `pause` toggle state, so inspect status first. Builds, asset creation
and scene changes must belong to the user's requested task; inspection alone
does not imply permission for those extra actions.
