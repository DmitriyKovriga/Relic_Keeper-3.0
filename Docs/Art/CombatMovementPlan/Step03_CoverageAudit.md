# Attack body motion and enemy recoil — coverage audit

Follow-up: [the custom melee expansion](Step03_MeleeExpansion.md) now adds the
ten melee gaps below, bringing eligibility to 23 gameplay skills plus Debug
Swing. This document preserves the **pre-expansion** audit and catalog.

Checked 2026-10-27 against the current working tree. This is a source/recipe audit, not a live playthrough of every skill. No gameplay or skill data was changed for this audit.

## Result

Player body follow-through is not universal. Of 74 SkillDataSO assets (including utility and debug skills), 14 qualify: 13 gameplay skills plus Debug Swing. This is recipe eligibility; dodge, dash, spin and stasis can suppress the pose during play.

Eligible gameplay skills: Cleave, Cold Slash, Execution, Extraction, Launcher Tower Toss, Needle Prick, Tectonic Press, The Weight of a Giant, Thunder Hammer, Torn Edge, Tremor, Venom Strike, Wave Cut.

Enemy recoil has broader coverage: positive direct hits reaching EnemyHealth.TakeDamage call CombatHitFeedback.Confirm. The common melee runner, projectiles, legacy SkillDamageDealer and TectonicShockwave use this receiver path. Lightning Rod emits through the runner; Frost Wall retaliates through projectiles. Recoil does not require a skill to qualify for player body motion or to have attack metadata.

Normal EnemyEntity.Setup initializes the recoil renderer. A standalone EnemyHealth without that setup/component does not receive the visual layer. Dodged/zero-damage hits, damage-over-time/pure-damage ticks, dead/hidden/frozen targets do not produce this recoil; lethal hits defer to death presentation. Boss recoil is reduced. These are intentional receiver rules, not missing skill registrations.

## Gaps and existing special animations

- Crimson Hook and Retreating Thrust use custom damage steps outside the supported list.
- Lunge Slash and Tower Charge use persistent contact damage; Ground Shake emits a ground projectile. They do not acquire the new body layer.
- Dexterous Lunge and Stunning Blow lack WeaponRecovery; Ninja Stub lacks WeaponStrike. Falling Anvil uses a separate ground/air pipeline.
- Spectacular Parry uses its parry step rather than a supported direct-damage step.
- Horizon Cross-section, bow attacks and thrown chakrams are excluded from this melee body layer. Wand/staff spell-mode skills are also excluded.
- Sweep and Conning Tower (triple sweep) retain their existing SpinAround animation. Generic body follow is deliberately excluded to avoid combining two body poses.

A sensible expansion order is custom melee first, then separate ranged/casting release poses. Simply removing the eligibility checks would leave special phase callbacks missing and can conflict with movement/spin presentation.

## Catalog

The table describes the new player body layer only. “Excluded” does not mean the skill lacks its existing animation or enemy hit recoil.

| Skill asset name | New player body layer | Eligibility reason |
|---|---|---|
| [A Fan of Sickles](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Chakram/lb/FanOfSickles/FanOfSicklesSkill.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile |
| [Aerial Volley](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Bow/AerialVolley/AerialVolleySkill.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile, SpawnProjectile, SpawnProjectile |
| [Arcane Reprise](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Shields/ArcaneReprise.asset) | Excluded | Spell mode |
| [Archer Focus](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Gloves/Evasion/ArcherFocusSkill.asset) | Excluded | Equipment skill |
| [Arrow Artillery](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Bow/Artillery/ArrowArtillery.asset) | Excluded | Custom/ranged pipeline: ArrowRain |
| [Big Cold Ball](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Cold/BigColdBallSkill.asset) | Excluded | Spell mode |
| [Big Fire Ball](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Fire/Adventurers/BigFireBallSkill.asset) | Excluded | Spell mode |
| [Big Lightning Ball](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Lightning/BigLightningBallSkill.asset) | Excluded | Spell mode |
| [Blink](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Boots/Evasion/BlinkSkill.asset) | Excluded | Equipment skill |
| [Circular Saw](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Chakram/rb/CircularSaw/CircularSawSkill.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile |
| [Cleave](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Axe/LeftButton/CleaveLB.asset) | Eligible | Standard phases and supported melee damage |
| [Cold Slash](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Universal2H/Slash/SlashSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Conning Tower](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Axe/ConningTower/ConningTowerSkill.asset) | Excluded | Uses its existing spin animation |
| [Crimson Hook](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Axe/CrimsonHook/CrimsonHookSkill.asset) | Excluded | Damage step is outside supported list |
| [Debug Swing](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Items/Debug/DebugSwingSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Dexterous Lunge](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Sword/DexterousLungeSkill.asset) | Excluded | Missing standard phases: WeaponRecovery |
| [Double Discharge](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Lightning/DoubleDischarge/DoubleDischargeSkill.asset) | Excluded | Spell mode |
| [Electric Beam](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Wand/ElectricBeamSkill.asset) | Excluded | Spell mode |
| [Endless Battery](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/BodyArmor/EndlessBatterySkill.asset) | Excluded | Equipment skill |
| [Evasionary](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Helmet/Evasion/EvasionarySkill.asset) | Excluded | Equipment skill |
| [Execution](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Dagger/Execution/ExecutionSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Extraction](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Axe/Extraction/ExtractionSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Falling Anvil](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/FallingAnvil/FallingAnvilSkill.asset) | Excluded | Custom/ranged pipeline: FallingAnvil |
| [Fire Ball](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Fire/Adventurers/FireBallSkill.asset) | Excluded | Spell mode |
| [Flaming Spear](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Fire/FlamingSpear/FlamingSpearSkill.asset) | Excluded | Spell mode |
| [Frost Anabiosis](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Shields/FrostAnabiosis.asset) | Excluded | Missing standard phases: WeaponWindup, WeaponStrike, WeaponRecovery |
| [Frost Wall](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Cold/FrostWall/FrostWallSkill.asset) | Excluded | Spell mode |
| [Frost-induced Jolt](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Cold/FrostInducedJolt/FrostInducedJoltSkill.asset) | Excluded | Spell mode |
| [G.D.S.](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Items/Unique/GodsHelmet/GodOfStrenghtSkill.asset) | Excluded | Equipment skill |
| [Ground Shake](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/GroundShake/GroundShakeSkill.asset) | Excluded | Damage step is outside supported list |
| [Haste](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Gloves/Armore/Hand of Iron Will/HasteSkill.asset) | Excluded | Equipment skill |
| [Homing Weapon Throw](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Chakram/lb/HomingWeaponThrow.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile |
| [Horizon Cross-section](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Sword/HorizonCrossSection/HorizonCrossSectionSkill.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile |
| [Ice Skates](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Boots/Mystic/IceSkates.asset) | Excluded | Equipment skill |
| [Icecle Shot](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Cold/IcecleShotSkill.asset) | Excluded | Spell mode |
| [Icy Palisade](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Cold/IcyPalisade/IcyPalisadeSkill.asset) | Excluded | Spell mode |
| [Launcher Tower Toss](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Shields/LauncherTowerToss/LauncherTowerToss.asset) | Eligible | Standard phases and supported melee damage |
| [Lightning Arc](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Lightning/LightningArcSkill.asset) | Excluded | Spell mode |
| [Lightning Rod](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Lightning/LightningRod/LightningRodSkill.asset) | Excluded | Spell mode |
| [Lucky Shot](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Bow/LuckyShot/LuckyShotSkill.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile |
| [Lunge Slash](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Sword/Adventurer/LungeSlash/LungeSlashSkill.asset) | Excluded | Damage step is outside supported list |
| [Mana Spear](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Wand/ManaSpear/ManaSpearSkill.asset) | Excluded | Spell mode |
| [Might Of Destruction](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Helmet/Armore/MightOfDestructionSkill.asset) | Excluded | Equipment skill |
| [Mystic Consume](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Gloves/Mystic/MysticConsumeSkill.asset) | Excluded | Equipment skill |
| [Mystic Haste](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Boots/Mystic/MysticHasteSkill.asset) | Excluded | Equipment skill |
| [Mystic Impulse](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Helmet/Mystic/MysticImpulseSkill.asset) | Excluded | Equipment skill |
| [Near Fan](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Bow/NearFan/NearFanSkill.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile |
| [Needle Prick](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Dagger/NeedlePrick/NeedlePrickSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Ninja Stub](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Dagger/NinjaStubSkill.asset) | Excluded | Missing standard phases: WeaponStrike |
| [Orbital Weapon Throw](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Chakram/rb/OrbitalWeaponTrowRBSkill.asset) | Excluded | Damage step is outside supported list |
| [Pendulum](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Chakram/lb/Pendulum/PendulumSkill.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile |
| [Piercing Shot](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Bow/PiercingShot/PiercingBowShotSkill.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile |
| [Plasma Field](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Helmet/Mystic/PlasmaFieldSkill.asset) | Excluded | Equipment skill |
| [Prismatic Cycle](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Wand/PrismaticCycle/PrismaticCycleSkill.asset) | Excluded | Spell mode |
| [Retreating Thrust](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Sword/Adventurer/RetreatingThrust/RetreatingThrustSkill.asset) | Excluded | Damage step is outside supported list |
| [Rush](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Boots/Evasion/RushSkill.asset) | Excluded | Equipment skill |
| [Seeker Skill](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Wand/SeekerSkill.asset) | Excluded | Spell mode |
| [Simple Bow Shot](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Bow/SimpleBowShotSkill.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile |
| [Spectacular Parry](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Shields/SpectacularParry.asset) | Excluded | Damage step is outside supported list |
| [Stunning Blow](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Hammer/StunningBlowSkill.asset) | Excluded | Missing standard phases: WeaponRecovery |
| [Sweep](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Axe/RightButton/SweepRB.asset) | Excluded | Uses its existing spin animation |
| [Tectonic Press](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/TectonicPress/TectonicPressSkill.asset) | Eligible | Standard phases and supported melee damage |
| [The Weight of a Giant](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/WeightOfAGiant/WeightOfAGiantSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Thunder Hammer](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/ThunderHammer/ThunderHammerSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Torn Edge](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/TornEdge/TornEdgeSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Tower Charge](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Shields/TowerCharge.asset) | Excluded | Damage step is outside supported list |
| [Tremor](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/Tremor/TremorSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Vail Of Evasion](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/BodyArmor/Evasion/VailOfEvasionSkill.asset) | Excluded | Equipment skill |
| [Venom Strike](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Dagger/VenomStrike/VenomStrikeSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Vitality Force](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/BodyArmor/Armore/VitalityForceSkill.asset) | Excluded | Equipment skill |
| [Warrior Step](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/Boots/WarriorStepSkill.asset) | Excluded | Equipment skill |
| [Wave Cut](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Sword/Adventurer/WaveCut/WaveCutSkill.asset) | Eligible | Standard phases and supported melee damage |
| [Weapon Throw](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/1HWeapon/Axe/WeaponThrowSkill.asset) | Excluded | Custom/ranged pipeline: SpawnProjectile |
| [Winter Storm](D:/Work/GameDev/Relic_Keeper-3.0/Assets/Resources/Skills/2HWeapon/Staff/Cold/WinterStorm.asset) | Excluded | Missing standard phases: WeaponWindup, WeaponStrike, WeaponRecovery |

## Source checkpoints

- `Assets/Scripts/Visuals/AttackBodyFollow.cs`: exact recipe eligibility and phase sampling.
- `Assets/Scripts/Skills/Logic/SkillStepRunner.cs`: accepted cast ownership and standard phase callbacks.
- `Assets/Scripts/Enemy/EnemyHealth.cs`: receiver confirmation after damage mitigation.
- `Assets/Scripts/Combat/ConfirmedHit.cs`: direct-hit filter and recoil dispatch.
- `Assets/Scripts/Enemy/EnemyEntity.cs`: recoil setup.
- `Assets/Scripts/Visuals/EnemyHitRecoil.cs`: visibility, freeze, lethal and boss presentation rules.

All recipe step GUIDs in the 74-asset inventory resolved. Unity compilation/tests were not rerun because this audit changes documentation only; the implementation verification remains recorded in Step03_BodyAndRecoil.md.
