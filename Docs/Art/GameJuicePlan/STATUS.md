# Game juice implementation status

Started 2026-10-29. The HTML and `PLAN.md` remain the design proposal; this file
records implementation evidence separately.

## Current step — J01 intake, rarity accents and staged loot flight implemented

The user completed an 11:12 ordinary-play session. Its baseline review is
complete, with coverage limits recorded in [Step 01](Step01_BaselineReview.md).
The first J01 treatment is implemented: a short merged intake response on the
existing loot sphere, with immediate storage, fixed interaction, quiet discards
and cancellation for taken/hidden loot. See [Step 02](Step02_J01_Intake.md) and
the [Play Mode preview](J01/IntakePreview.gif). The requested rare wave/stars and
unique 2x effect are now delivered in [Step 03](Step03_J01_Rarity.md), with
65 passing focused tests and live rare/unique/batch/discard checks.
The follow-up in [Step 04](Step04_J01_LootFlight.md) adds rise/hover/rarity flash
before travel to the sphere, and an orange-red unique burst at twice the rare
burst's width and height. Its combined focused suite passed 73 tests; five live
checks include open-cache arrivals. [Current preview](J01/FlightReveal/LootFlightPreview.gif).
The user's next refinement restores the original item bubble (rarity-colored
circle plus actual item icon) throughout the rise, hover and flight. See
[Step 05](Step05_J01_ItemBubble.md) and the
[updated preview](J01/ItemBubble/ItemBubblePreview.gif). The 73-test suite remains
green, with five repeated live checks now verifying visible item bubbles.
Accumulation states and flight-stream grouping remain optional.
J07/J08 remain user-owned and excluded.

The first normal-scene Windows development build exposed a prerequisite defect:
the main menu/UI rendered correctly but world sprites were magenta. A runtime
material snapshot found 28 renderers with null materials in HubScene. This is
not an atmosphere-strength or loot-feedback observation.

The existing color-preview save guard handled scene/prefab saves and Play Mode
snapshots, but did not suspend temporary HideAndDontSave material bindings during
player builds. A build guard and two regression cases have been added. The
WorldColorProfilesTests filter passed 18 tests, 0 failed/skipped. A clean normal-
scene player build then succeeded and the live hub rendered correctly; its
material report no longer contains null materials. The combined color-profile
and appearance-window filters subsequently passed 26 tests, 0 failed/skipped.
The rendering blocker is resolved in the checked development build. Comparison:
[before/after](Baseline/BuildRendering.png),
[before material report](Baseline/Before-rendering.txt),
[after material report](Baseline/After-rendering.txt).

The baseline builder uses the real MainMenuScene and HubScene with separate
company/product identifiers. It does not change combat, loot, cooldowns, enemy
AI or immortality. Its save path is isolated from the user's game. The original
save's SHA-256 before testing was
`442e728fc515c02ab17d8a6bdafe595926e20b2dec41f7cabe29908edd6b50db`.
It still matched after the build/live checks.

An opt-in development-player recorder (`-game-juice-baseline`) samples the full
game screen, including UI, at up to 4 fps. `frames.csv` records actual unscaled
timestamps; sampling must not be described as a performance measurement or a
high-frame-rate combat capture. Raw captures are under ignored
`Logs/GameJuiceBaseline/<UTC timestamp>/`.

The first corrected rendering check was `Logs/GameJuiceBaseline/20261029-112116/`.
The actual user-driven baseline is `Logs/GameJuiceBaseline/20261029-152839/`:
the reviewed segment is 2,585 samples ending at 672.371 seconds (frame 2584),
including three lives. Later idle/unfocused samples are excluded. See the
[review](Step01_BaselineReview.md) and [selected frames](Baseline/OrdinaryRunEvidence.png).
Earlier attempts and rendering-blocker time are not counted as ordinary play.
Automated keyboard input did not reliably reach Unity, so the human drove the
baseline. This was a tool limitation, not evidence of a game input defect.
Combat, cache collection/equipment, portal unlock/full clear, passive-tree use,
and three deaths/returns are represented. Orb crafting, rejected inputs and
controlled edge cases remain separate feature checks, as documented in Step 01.

To repeat the capture, invoke the Editor build method
`RelicKeeper.Editor.Combat.GameJuiceBaselineBuild.Build`, then launch
`Builds/GameJuiceBaseline/GameJuiceBaseline.exe -game-juice-baseline
-screen-fullscreen 0 -screen-width 960 -screen-height 540` from the project root.
The actual save directory is
`%USERPROFILE%/AppData/LocalLow/DefaultCompany_Diagnostics/Relic_Keeper Juice Baseline`.
The optional recorder stops sampling after 20 minutes; the game keeps running.
Normal shipping players contain no recorder implementation. No recording starts
in the Editor or without the explicit development-player flag.

## Baseline route and remaining feature checks

The ordinary route has been recorded with normal rules and an isolated save.
Keep the following coverage checklist for later A/B passes; Step 01 distinguishes
observed events from unverified cases:

1. Enter a normal Mortfall run; fight and observe damage/resource consequences.
2. Observe drops travelling to the exit container; open it before every flight
   has finished, inspect and take loot.
3. Equip one item, perform an available orb operation, and allocate a passive
   point if the character has one. Record unavailable prerequisites as such.
4. Use a cooldown skill; retry during cooldown and with insufficient mana.
5. Observe the portal-unlock threshold and, separately, full room clear. Move to
   the next room through the ordinary portal.
6. Allow a character death and inspect the existing summary and return to hub.

Do not count startup/loading/diagnostic-blocker time as ten minutes of gameplay.
Do not substitute the previous combat stress fixture for this route.

## Delivered slice and next feature

J01 intake and its flight/rarity follow-ups are delivered and checked in real Mortfall Play Mode, including a
twenty-item burst/full inventory, a quiet discard and arrival while the real
cache window is open. Detailed validation and limits are in Step 02. The old
standalone baseline executable has not been rebuilt with this feature.

Next: J02, then J03 and J06, each separately reviewed in ordinary gameplay. Later
J04/J05/J10 depend on actual remaining gaps; J09 is last.
