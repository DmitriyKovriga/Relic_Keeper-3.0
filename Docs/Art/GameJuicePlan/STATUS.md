# Game juice implementation status

Started 2026-10-29. The HTML and `PLAN.md` remain the design proposal; this file
records implementation evidence separately.

## Current step — J10 optional early return

Completed 2026-10-30. The existing death summary keeps its appearance and statistics.
A quiet localized Return to hub button becomes available after one second and a
release of held confirmation controls. Click, Enter/Space or gamepad South can
shorten the wait; the six-second automatic return remains. The title/button fade
and a fully rendered black frame still precede the exactly-once return callback.
No death rules, recruitment, progress or save format changed. **16 focused tests
passed**, plus native 480x270 EN/RU, live locale switch, large values, actual UI
submit, held/repeated input, three sequences and rendered-cover checks. Native
early callbacks occurred at 2.206/2.202 seconds; automatic return at 6.020 seconds.
See [Step 12](Step12_J10_DeathReturn.md) and [preview](J10/DeathSummaryENRU.png).
Controlled callbacks do not remove a real character; ordinary combat/death and a
fresh standalone build remain separate checks. Next candidate: optional J09,
starting with one existing prop only if a useful reaction warrants it. J07/J08
remain user-owned and excluded.

## Previous step — J05 enemy anticipation and recovery

Completed 2026-10-30; refined after user review. The original red overhead attack
mark and accompanying flash are restored; replacement glyphs/placement changes
are removed. Its full-preparation timing fix and existing recovery are retained.
The requested projectile follow-up adds a short dashed aim line beside the
original mark, following the same launch ray until release/cancellation. It is
automatic for actual projectile attacks and retained with lower opacity in
reduced effects. Original restoration passed 64 focused tests; the latest
follow-up passed 30 targeted tests, plus native 480x270 moving-target left/right,
solo/crowd and normal/reduced checks. Current [projectile preview](J05/ProjectileAim/ProjectileAimPreview.gif)
and [states](J05/ProjectileAim/States.png) show the addition. See
[Step 11](Step11_J05_EnemyIntent.md). Ordinary combat/HUD review and a standalone
build remain separate. Next candidate: J10's repeated-attempt death/return audit,
only adding feedback for an observed gap.

## Previous step — J04 Shock buildup and active electricity

Completed 2026-10-30. Successful partial procs show a short cyan trace; crossing
the existing threshold gives one discharge and longer arcs. After user review,
smoothly flowing paths were changed to fast, held-shape lightning snaps with
irregular paired flashes and brief quiet gaps. Reduced mode has fewer/softer
snaps; the entry accent is shorter. Refreshes
do not replay activation. Ailment-owned presentation follows authoritative
expiry/reset and no longer writes body tint/material channels. Damage, chance,
threshold and duration rules remain unchanged. Reduced mode is quieter; no new
popup or HUD element was added. **68 focused tests passed**, plus native checks
for actual proc/threshold/refresh/expiry, animation, Ignite/freeze coexistence
and ten enemies. See [Step 10](Step10_J04_Shock.md),
[preview](J04/Shock/Snaps/ShockPreview.gif) and [state details](J04/Shock/Snaps/Detail.png).
The three-chain audit retained Mystic Shield's existing dedicated response and
charge/recharge display. Ordinary-combat review and a new standalone build remain
separate from the controlled Editor checks.

## Previous step — J04 Ignite burning presentation

Completed 2026-10-30 at the user's request: animated sprite-masked burning,
rising fire, warm glow and a small flickering surface light. Ignite recipients
share the same effect; stack refresh does not duplicate visuals. Source art,
hit-flash ownership, damage and spread rules remain unchanged. After the user's
screenshot, the fire was reshaped from the animated enemy silhouette and placed
behind the body. A second review replaced the aura-like shell with separate
surface-emitted flame tongues: staggered growth, upward curl, bright cores and
smaller foreground flames. Continuous motion replaces snapped coordinates;
the preview records its actual sampling times. A follow-up overlaps flame lives
and blends emission across sprite frames to reduce flicker. Ignite's light/glow
now share a slower visible breath; existing torch motion stays unchanged.
The latest preview records 96 frames and the light/glow/radius pulse samples.
Reduced effects is quieter. **29 focused tests passed**, with native rendering, animation/flip,
paired terrain-light, ten-enemy and cleanup checks. See
[Step 09](Step09_J04_IgniteBurn.md), [preview](J04/Ignite/FlameBreath/IgnitePreview.gif) and
[comparison](J04/Ignite/FlameBreath/IgniteComparison.png). Other J04 chains remain unaudited;
ordinary-combat review and a fresh standalone build are separate checks.

## Previous step — J06 room resolution and exits

Completed 2026-10-30. Portal unlock has a brief local pulse and remains immediately
usable. Unlock, full clear and level-up merge into one announcement; a brief
direction hint appears only for an offscreen exit. Kill thresholds and modifier
rewards are preserved. **85 focused tests passed**, plus native 480x270 Play Mode
checks for EN/RU, reduced effects, message overlap, room cancellation, duplicate
events, one-enemy and empty rooms. See [Step 08](Step08_J06_RoomResolution.md),
[controlled comparison](J06/RoomResolutionComparison.png) and
[runtime report](J06/RuntimeCheck.txt). Ordinary-run A/B evaluation and a fresh
standalone build remain separate from these controlled Editor checks.

## Previous step — J03 skill-state feedback

Completed 2026-10-30. Skills report their actual cast outcome to the HUD.
After user review, floating skill captions were removed: feedback now stays
within the skill slots, including rejection and readiness highlights.
The refinement passed **25 focused tests** and native EN/RU/reduced-effects
checks with no floating messages. See the current-behavior note in
[Step 07](Step07_J03_SkillStates.md). Accepted casts with at least a
three-second effective cooldown can
produce one ready cue once recovery and the current cast gates allow use.
Zero-cooldown primaries, initialization, HUD rebuilds and restored weapon skills
stay quiet. **84 focused tests passed**, plus native 480x270 EN/RU and reduced-
effects checks. See [Step 07](Step07_J03_SkillStates.md),
[native comparison](J03/SkillStateComparison.png), and
[enlarged detail](J03/SkillStateDetails.png). Casting rules and input buffering
remain unchanged. Ordinary-run A/B evaluation and a standalone rebuild remain
separate from these controlled Editor checks.

## Previous step — J02 transaction feedback

J02 adds local confirmation/rejection cues for equipment changes, crafting and
passive allocation. Crafting's existing tooltip now displays committed affix
changes, including lower rolls and removals. No timing, prices, points, crafting
rules or saves change. **80 focused tests passed**, with native 480x270 Play Mode
checks for equipment, EN/RU crafting, rejection, purge and passive allocation.
See [Step 06](Step06_J02_Transactions.md) and
[captured UI comparison](J02/UpgradeFeedbackComparison.png). Ordinary-play A/B
evaluation remains a user review; the old standalone baseline has not been rebuilt.

## Previous step — J01 loot presentation

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

J02's first slice is delivered; detailed validation and manual checks are in
[Step 06](Step06_J02_Transactions.md).

J03 is delivered; validation and manual checks are in
[Step 07](Step07_J03_SkillStates.md).

J06 is delivered; validation and manual checks are in
[Step 08](Step08_J06_RoomResolution.md).

J04's Ignite/Shock treatments are delivered and the three-chain audit is recorded
in [Step 10](Step10_J04_Shock.md). Mystic Shield retains its existing feedback.
J05's Knight/Mold presentation audit is delivered in [Step 11](Step11_J05_EnemyIntent.md).
Review these in ordinary combat. J10 is delivered in [Step 12](Step12_J10_DeathReturn.md).
The remaining J09 pilot is optional; J07/J08 remain user-owned and excluded.
