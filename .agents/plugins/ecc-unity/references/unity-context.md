# Relic Keeper context

This adaptation applies to the Relic Keeper 3.0 repository. Read the current root
`AGENTS.md` and any applicable nested instructions; user instructions take
precedence over this workflow. Confirm versions from `ProjectSettings/ProjectVersion.txt`
and `Packages/manifest.json` before using version-specific APIs.

The reviewed baseline is Unity 6000.3.11f1, URP 17.3.0 with Renderer2D,
Input System, Cinemachine 3, Localization EN/RU, TextMeshPro, uGUI and UI Toolkit.
There are no custom assembly definitions. Runtime code is in `Assembly-CSharp`;
editor and EditMode tests are in `Assembly-CSharp-Editor`.

## Changes that need particular care

- Inspect the working tree first and preserve unrelated edits. Search the affected
  runtime path, data, callers and tests before adding a parallel abstraction.
- Serialized field names, enum values, component/script identities, asset GUIDs
  and public APIs can be persistent contracts. Preserve them; use an explicit
  migration if a change genuinely requires one. Keep asset moves paired with
  their existing `.meta` files. Avoid incidental scene, prefab or material churn.
- Keep balance in existing data/configuration. Do not invent balance targets.
  Displayed diagnostics should use the same calculation as runtime behavior.
- Use existing lifecycle and ownership patterns. Check event unsubscribe,
  pooled-state reset, coroutine cancellation, Addressables handle ownership,
  Unity object lifetime and static state when relevant to the change.
- Keep Unity API calls on their supported thread. Inspect actual hot paths before
  changing allocation, lookup or pooling behavior; optimize against evidence.
  Immutability is useful for pure calculations, not a blanket rule for frame loops.
- Keep editor/debug-only code out of player assemblies using `Editor` folders or
  `UNITY_EDITOR` as appropriate. Do not introduce assembly definitions just to
  accommodate this workflow.

## UI and rendering

For runtime UI, use the existing `relic-keeper-pixel-ui` skill. Its installed
source is `~/.codex/skills/relic-keeper-pixel-ui/SKILL.md`; if unavailable, read
the project's comparable UI and report the missing skill. Read
`Assets/UI/Inventory/PixelArtPanelSettings.asset` before choosing dimensions.
Preserve the 480x270 logical canvas, integer placement, sharp pixel styling,
functional UXML/USS first, sprite decoration later, and EN/RU localization.
Visual changes need an actual Play Mode check in addition to relevant tests.
Keep optional FX safe to leave unwired and protect UI from camera effects.

## Verification boundaries

Use the existing `agents-unity-bridge` CLI from the project root, sequentially:
status, compilation, the narrowest relevant EditMode test class, console errors.
Confirm nonzero executed tests and report passed/failed/skipped counts.
An exact class filter avoids an accidental empty green result. This project has
no PlayMode test suite; manual Play Mode inspection is a separate activity.
`dump-asset` on a scene describes only the currently open scene.

Markdown, skill and plugin configuration changes need structural validation,
reference checks and installation/discovery verification; they do not need new
gameplay tests. Run Unity checks when Unity-imported files or code change.
If the Editor is unavailable, complete independent work and state exactly which
checks remain unverified. Never label a timeout, zero tests or an unexecuted
check as a pass. Do not create commits, recurring tasks, automatic memory writes,
or subagents solely because an upstream ECC workflow suggests them.
