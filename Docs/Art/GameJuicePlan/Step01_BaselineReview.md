# Step 01 — ordinary-run baseline review

Reviewed 2026-10-29, after the user completed the isolated playthrough.
This is a presentation baseline, not a balance, performance or complete QA pass.

## Recording and scope

Human-driven ordinary gameplay in the normal-scene Windows development build.
The reviewed segment contains 2,585 screen samples, ending at 672.371 seconds
(11:12), frame 2584. Later idle/unfocused samples are excluded, including an
approximately four-minute sampling gap after the playthrough. An immutable
`ReviewedFrames.csv` snapshot is saved in the ignored review folder below.
It includes three character lives, hub equipment/market use, Mortfall combat,
room transitions, loot, passive-tree use, deaths and returns to recruitment.
It is not the previous combat stress fixture. No diagnostic input driver,
invulnerability, cooldown acceleration or extra drops were used.

The recorder sampled at up to 4 fps. It records images and actual unscaled
timestamps, not inputs or authoritative gameplay events. It can establish
visible states and sequences; it cannot prove the absence of sub-frame glitches,
identify a rejected keypress, or measure input latency or GPU performance.

Raw local capture: `Logs/GameJuiceBaseline/20261029-152839/` (ignored; may also
contain later idle samples).
Review contact sheets: `Logs/GameJuiceBaseline/Review-20261029-152839/` (ignored).
Selected evidence: [contact sheet](Baseline/OrdinaryRunEvidence.png),
[sample summary](Baseline/OrdinaryRunSummary.json).
Times below are seconds from the recording start, rounded to the nearest second.

## Observed coverage

| Time | Observation | Implication |
|---|---|---|
| 5–35 | Hub, storage/market, item and skill tooltips, equipment changes, initial dungeon condition selection. | Equipment and granted skills already have detailed inspection UI. Preserve it. |
| 40–90 | First room combat, visible damage/resources/statuses and cooldown numbers; Portal Open banner around 60; next-room choice around 90. | Portal unlock already has a visible announcement and can precede full clear. |
| 95–147 | Second room combat; Portal Open around 110; exit loot sphere visible around 135–136. At 137, cache UI contains boots; at 138, the cache is empty and boots appear in the equipped feet slot. | The centralized exit-cache loop is used in real play. Extend this container rather than replacing it with scattered drops. Images do not identify item IDs or prove mid-flight interaction timing. |
| 142–147 | Room Cleared, then Character Level Up roughly a second later, followed by the next-room choice. | Both resolution outcomes already exist. Include clear/level-up overlap in J06 checks; this is not evidence that either message was completely lost. |
| 100, 150, 255, 600 | Passive-tree windows, available points, selected/inspected nodes. | The normal build-adjustment loop is represented. Exact committed stat differences are not logged by this recorder. |
| 187–194 | First death; readable summary shows highest stage 7 and 23 kills; return to recruitment. | Existing death statistics and return sequence work visibly in this attempt. |
| 215–416 | Another character/run, repeated combat and room transitions, equipment inspection; second death shows 60 kills, then returns to the hub. | The feedback is seen repeatedly rather than only once in a fixture. |
| 420–672 | Third character/run, spell/ailment-heavy fights, level-up and passive-tree use; third death shows 67 kills; recruitment is visible by 668. | Frequent status damage already fills the combat space. New routine reward cues should remain bounded and quiet. |

The original game save's SHA-256 still matched its pre-test value after this
session: `442e728fc515c02ab17d8a6bdafe595926e20b2dec41f7cabe29908edd6b50db`.

## Findings and order

**J01 remains the first feature.** The sphere and its question-mark marker can
be found near the exit and used while fighting continues. The current source
starts a cosmetic flight only after storing the item, but the flight ends by
hiding its own renderers/trail; it never tells the cache that it arrived. The
cache's existing glow is an idle pulse, not an arrival response. Thus a specific
missing connection is verified in source. The sampled recording does not by
itself prove that every arrival was missed by the player.

The first implementation slice should add a brief intake response using the
existing container art, animated independently of its interaction collider.
Merge arrivals in bursts, keep storage immediate, and cancel presentation when
the relevant item is taken/hidden or the room disappears. A bounded valuable-
arrival accent follows only if it remains clear in the normal route. No global
presentation manager or new container type is needed.

**J02 follows:** the equipment/tooltip flow is already readable when inspected.
Use committed transaction results for any additional confirmation. The recording
does not establish an orb craft or its rejected case; cover those with the J02
prototype rather than claiming they passed here.

**J03 follows:** cooldown numbers and mana changes are already visible. The
recording has no input trace, so it cannot establish which presses were rejected
for cooldown or mana. Verify those against actual cast outcomes during J03;
do not infer rejection from a countdown alone or add ready pulses to primaries.

**J06 follows:** preserve the separate Portal Open and Room Cleared outcomes.
Audit local exit readability and the observed clear/level-up overlap. Existing
banners do not justify another compulsory pause or an unconditional reward chest.

J10's existing summaries/covered return are represented by three deaths; no new
death presentation is justified as a prerequisite for J01. J07/J08 remain user-
owned. This review does not reopen completed atmosphere/combat work.

## Coverage limits carried into feature checks

- No verified orb operation or deliberately rejected crafting transaction.
- No input telemetry proving cooldown/mana rejection, nor EN/RU switching.
- No controlled full-inventory, twenty-drop burst, filter change, simultaneous
  arrival with cache UI open, or room-exit-mid-flight check. These belong to J01's
  regression/runtime acceptance, not to an inferred pass from ordinary footage.
- At 4 fps, fine timing and individual 0.42-second flights require a focused
  higher-rate check during implementation.

Step 01's baseline review is complete with these explicit limits. No J01–J10
gameplay presentation feature has yet been implemented.
