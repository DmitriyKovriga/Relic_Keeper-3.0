# Ground surfaces and Ice Skates

Ice Skates is an equipment skill in the Mystic Boots pool: 8 seconds, 35% more movement speed, 30 mana and a 15-second cooldown. Grounded acceleration/braking becomes slippery; air control, jumping, dodges, charges and existing movement locks keep their normal priority. Ground locomotion uses the standing pose while gliding.

The `IceSkates` recipe step references a `GroundSurfaceEffectSO`. Create other surfaces through **Create > RPG > Skills > Ground Surface**. The asset controls the sprite, patch width, lifetime, damage interval, weapon damage per second, channel conversions and optional contact stat modifiers. The skill editor exposes the asset and skating settings. The ice asset uses 3 seconds, 30% weapon damage per second, 100% conversion to cold and no extra contact modifiers. Cold ailments use the usual player chance and enemy protection rules.

`PlayerIceSkatesController` owns the timed speed buff independently of the recipe runner, so weapon swaps or another skill do not cancel it. Recasting refreshes it without stacking. Leaving the ground interrupts trail placement; small ground movements fill supported intervals, while teleports do not paint their path. Each patch clips at terrain edges, supports one-way platforms and slopes, and follows its supporting platform. Ice never adds a physical collider.

`GroundSurfaceSource` owns one contact clock per enemy across all its patches. Every 0.25 seconds of contact deals 7.5% weapon damage; fractional remaining contact is settled on leaving or expiry. Multiple patches and compound hurtboxes do not multiply damage. Pulse damage goes through `SkillStepRunner`'s existing conversion, hit, ailment and on-hit pipeline. Optional contact modifiers use one removable lease per target, released when the last patch is left. A new cast reuses the player's surviving source, including its damage clocks.

Ending the buff leaves existing patches for their remaining lifetime. Disabling/deleting the player removes both the buff and owned ground effects; missing/disabled support retires its patches. Ice fades over its final 0.35 seconds. Each source owns a material cloned from a build-retained unlit shader material; it does not depend on editor-only shader discovery.

Art is native 24x24 PixelLab output. Both textures use point filtering, no mipmaps or compression, and 24 PPU. The ground sprite uses a pivot aligned to its opaque upper edge. `IceSkatesBuilder.Apply` idempotently wires data, the pool, database and EN/RU name tables; it is a content-authoring action, not a runtime initializer.

Regression suite: `RelicKeeper.Tests.EditMode.IceSkatesTests`. Optional isolated Play Mode evidence: `RelicKeeper.Editor.Skills.IceSkatesPreview.Capture`, which writes 480x270 captures and an actual-cast report to `Temp/IceSkatesPreview` without saving scene changes.
