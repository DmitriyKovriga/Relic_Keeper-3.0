# Final combined playthrough — prepared, review pending

Prepared 2026-10-30. The current normal-scene Windows development player is built
and running in **Relic_Keeper Juice Baseline**. It uses the existing isolated
diagnostic save, normal combat/loot rules and the opt-in recorder.

## Standalone blocker found and fixed

The first smoke run exposed a legitimate post-death loading state: the save had
hostel characters but no active character. `GameSaveManager.LoadGame` queries
`CharacterDatabaseSO.GetCharacterByID` with the absent active character's null
class ID before entering its existing required-recruitment branch. The database's
dictionary lookup threw `ArgumentNullException`, causing the load catch to call
`StartNewGame` instead of opening recruitment.

`GetCharacterByID` and `ContainsCharacter` now treat null/empty IDs as missing,
returning null/false. Valid IDs and save data/schema are unchanged. New tests
reproduced two null-key failures before the fix. Afterward, compilation and
**25 EditMode tests passed, 0 failed/skipped** across
`CharacterPartyManagerSaveTests`, `DeathProgressTests` and `DeathScreenFlowTests`.
The new cases include a no-active-character save retaining its hostel character
and highest-stage record, plus null/empty/unknown database lookups.

The corrected standalone build then loaded the same isolated save and opened the
required Tavern selection. Its player log contains the expected recruitment
message and no load-error/default-character fallback. This is a real standalone
startup check, not only a synthetic party-manager test.

## Build, isolation and evidence

Both builds used `RelicKeeper.Editor.Combat.GameJuiceBaselineBuild.Build` with
MainMenuScene/HubScene and normal rules. The first build succeeded in 135.289 s
but failed the save-loading smoke; the corrected build succeeded in 104.491 s.
The builder restores the company/product/preloaded settings after compilation.

- Main save SHA-256 remained
  `DF3EE83C407AE06D35F3A887EFFBCECD7FC0F1FBC7421F60AE88EE6CDE321044`.
- ProjectSettings SHA-256 remained
  `7B5D086AB83ACA2C6644D5A640AC20A9A12F497C0D785153858F867F36D8A47C`.
- Diagnostic save: `%USERPROFILE%/AppData/LocalLow/DefaultCompany_Diagnostics/Relic_Keeper Juice Baseline`.
  Existing diagnostic saves were backed up before launch. The main save was not
  copied into, reset by, or used as the playtest save.
- [Recruitment smoke](FinalReview/RecruitmentSmoke.jpg) is an actual game-recorder
  frame. [Hub renderer report](FinalReview/HubRendering.txt) records supported
  material/shader groups. Native computer use verified the menu/recruitment window.
- Full preparation manifests, backups and player logs are local/ignored under
  `Logs/GameJuiceFinalReview/20261030-113924/`.

The build generated a new Addressables `link.xml`/meta and explicit default light
fields in the ten room prefabs. The preflight showed those source files clean.
Only default additions (`_breathDepth: 0`, `_breathSpeed: 1`, `_overrideTint: 0`)
were eligible for removal; exact diff validation preceded restoring the preflight
room bytes. Backups remain with the build preparation. No authored lighting or
room changes were discarded. The compiled defaults and explicit defaults are
equivalent. Pre-existing J09 test/documentation changes are preserved.

## Recording and next action

The failed first smoke (`20261030-114325`) is excluded from human-play evidence.
The corrected smoke (`20261030-115608`) expired while idle, so it was restarted
before handoff. The fresh capture is `Logs/GameJuiceBaseline/20261030-144251/`;
[Handoff.json](FinalReview/Handoff.json) identifies the setup boundary. Do not
count startup, the long idle gap, or smoke checks as an ordinary playthrough.

Capture is at up to four JPEG samples per second, with actual unscaled times in
`frames.csv`. This supports whole-run state/readability review, not high-speed
lightning timing, input latency or a performance benchmark. It stops after twenty
minutes; the game keeps running. Closing the game closes the recording stream.

Play for 10–15 minutes in the open window:

1. Recruit a hero. Enter Mortfall and fight normally; observe attack marks/aim
   lines, loot flights/bubbles/cache arrivals and room unlock/clear feedback.
2. Swap equipment, try a passive allocation/craft if resources allow, and retry a
   skill while it is on cooldown. Compare normal/reduced effects where practical.
3. Include death/return and recruitment. The playtest save has normal consequences;
   the main save remains separate. Watch both early confirmation and automatic
   return if a second attempt is practical.
4. Close the playtest window and reply **done** in chat, with any confusing or
   distracting moments. Completion is reported in chat, not by an in-game Done
   button. The detailed route remains in [Step 13](Step13_J09_Review.md).

At preparation time, final ordinary-play review was pending. The completed
human review and its passive-tree follow-up are now recorded in
[Step 15](Step15_FinalReview.md). This preparation report does not itself claim
a performance pass.
