# J02 — upgrade transaction feedback

Implemented 2026-10-29 as a presentation prototype for equipment, orb crafting
and passive allocation, following the roadmap's first-priority order.

## Behavior

- A committed equipment change from Ctrl-click or a drop into an equipment slot
  briefly outlines the changed slot in gold with a small `+` marker. A rejected
  equipment attempt marks its target/source in red with `!`. Inventory rebuilds,
  opening a window, weapon-preset refresh and returning an item to its own slot
  do not produce a confirmation.
- A successful orb operation uses the same local gold cue and the existing
  crafted-item tooltip. Its affix section shows actual old > new rolls, additions
  prefixed `+`, and removals prefixed `-`. Unchanged affixes remain visible.
  Lower rolls are shown literally; the UI does not declare the result better.
  The receipt belongs to the currently displayed craft result and is replaced,
  not queued, on repeated crafting. Switching language updates its text.
- A rejected orb operation uses the red cue and preserves the existing refund/
  consumption rules. The last committed result is not overwritten by a failed
  attempt. Clicks without an item target retain their existing cancel behavior.
- A successful passive allocation outlines its node after allocation has actually
  committed. A denied allocation has the red cue. Already-owned nodes and preview
  mode stay quiet. Existing connection/point updates continue immediately.

The shared cue has one active overlay per UI owner: no animation queue, no target
translation/scale, and no input interception. Success lasts 0.6 seconds, rejection
0.45 seconds with two flashes; repeated same-target rejection is throttled to
0.2 seconds. A new rejection always replaces an intervening success. Timers use
unscaled time, since these windows pause the world. Closing inventory, disabling
or detaching the UI cancels its cue. Reopening the passive tree clears any old cue.

The existing Alt inspection still displays the current item's normal detailed
range/tier rows; comparison tooltips also retain their normal affix presentation.
Craft history does not leak into the equipped-item comparison. Existing keyboard
bindings, tooltip pin behavior, selection and pointer targets remain in use.

Implementation extends InventoryUI.Crafting/DragDrop, the existing item tooltip
and PassiveTreeUI/Renderer. CraftingChangeSnapshot copies the rolled values
around one operation; it is not a character-wide stat-difference engine.
No gameplay/event-bus rewrite, new bitmap art, player controls, save fields,
crafting prices, RNG rules, inventory transfer rules or point costs were added.
Code-built labels use the project's EN/RU localization fallbacks.

## Verification

Compilation succeeded. **80 focused EditMode tests passed, 0 failed/skipped**:
CraftingChangeSnapshotTests, UiTransactionFeedbackTests, CraftingOrbEffectsTests,
CraftingOrbApplyModeTests, CraftingOrbInventoryTargetTests, InventoryAutoEquipTests,
PassiveTreeNodeClickTests, ItemTooltipScreenFitTests, ItemTooltipInspectTests,
ItemTooltipCompareTests and TooltipPinPolicyTests.

Eleven added cases cover immutable lower-roll snapshots, removal/purge, replacement,
unchanged rolls, range/additional modifiers, duplicate sources, Alt inspection,
bounded nonblocking overlays, success/rejection distinction, disposal and the
rejection-after-success throttle edge case. Existing tests cover the surrounding
crafting/equipment/click/tooltip rules.

An opt-in Editor check uses the actual inventory UXML/USS/art, tooltip and
transaction callbacks on a separate 480x270 render target. It uses disposable
inventory/item/tree data; live singletons and the account crafting wallet are
restored between every synchronous callback. The world is paused through the
normal pause service. It verifies:

| Case | Result |
|---|---|
| Equipment swap | Actual Ctrl-equip swap succeeds; confirmation runs while paused. |
| Rare reroll | Four old rolls of 30 become committed rolls of 1–8; tooltip reflects them. |
| Live EN/RU | Receipt text changes language, remains inside 480x270, and the craft target stays fixed. |
| Empty currency | Operation rejects, currency stays zero, red `!` replaces gold. |
| Purge | All four affixes are removed; all removed rolls remain visible in the result. |
| Passive allocation | Exactly one point is spent; node becomes allocated before confirmation. |
| No-point allocation | Next node rejects, with no point/state mutation. |
| Cleanup | Overlay expires despite the paused game clock. |

[Runtime report](J02/RuntimeReport.txt),
[UI comparison](J02/UpgradeFeedbackComparison.png),
[English receipt](J02/Craft-lower-en.png),
[Russian receipt](J02/Craft-lower-ru.png),
[rejection](J02/Craft-rejected-ru.png),
[purge](J02/Craft-purged-ru.png),
[passive confirmation](J02/Passive-allocated.png),
[passive rejection](J02/Passive-rejected.png).
These are actual Unity UI frames from controlled operations, not an ordinary-run
A/B recording or a performance measurement. The tree fixture uses three nodes
and the project's real theme; it does not replace game-tree artwork.

The final bounded console-error query was empty; earlier diagnostic attempts
required correcting fixture setup (a prohibited flat attack-speed affix, a missing
drag ghost and reflection access to a public field). Those failed attempts are
not counted as passing checks. The final fixture report above passed.
The original save SHA-256 remained
`442e728fc515c02ab17d8a6bdafe595926e20b2dec41f7cabe29908edd6b50db`.
No scene or project-setting changes were introduced. Graphify's code-only cache
was refreshed, with its existing namespace-collision warnings.

The referenced relic-keeper-pixel-ui skill was absent from the installed skills
and repository. The ECC Unity context explicitly provides the fallback: inspect
the comparable project UI and preserve its 480x270 layout, sharp styling and
EN/RU behavior. That fallback was used; no missing-skill approval was inferred.

## Manual check

1. Open inventory with I. Ctrl-click a backpack item to equip/swap it, or drag it
   into a valid equipment slot. Its committed destination should briefly flash
   gold without moving. Try an invalid equipment destination for the red cue.
2. Open crafting with K. Right-click an orb/relic, then click an eligible item
   in the craft slot or backpack. Read the before > now values in its tooltip;
   try a reroll and purge/removal where available. Hold Shift for repeated use.
   Repeated crafting should replace the receipt and keep the target in place.
3. Attempt a craft that the selected relic cannot apply to. Look for red `!` and
   check that no currency was spent. Alt inspection and tooltip locking retain
   their existing functions. Switch EN/RU to check the last result in both locales.
4. Open the passive tree with T. Spend a point on an available node. Then try a
   locked node or a node without points: it should show rejection and spend none.
   Close/reopen the window; the old confirmation should not replay.

The standalone baseline executable was not rebuilt. Ordinary-play evaluation,
larger item catalogs, full gamepad navigation and a six-affix long-text stress
capture have not been newly audited. J03 skill-state feedback is next; J06 follows.
