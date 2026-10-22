# Frost-induced Jolt / Морозный толчок

Cold staff special skill: 15-second cooldown, 25 mana, 250% weapon damage converted entirely to cold. Uses spell action speed. One centered hit at 30% of the sequence reaches enemies on both sides within a base radius of 1.75 units. The hit tests an ellipse, avoiding the corners of the broad-phase box. Area of Effect uses the project's standard scaling: full contribution horizontally, half vertically, without moving the center forward.

Base knockback rating is 750. Each complete positive 1% ColdResist adds 10 flat Pushback for this skill only (75% gives 1500 total before any existing Pushback). Negative resistance adds no bonus. Uses the current ColdResist stat value, including modifiers; the link does not change the character's stats or benefit other skills. Target PushbackResist, center-overlap dead zone, and frozen-target immunity follow the existing knockback rules. Freeze chance/duration and refreeze immunity also use normal rules; freezing locks movement as usual.

Registered in the cold staff special pool and item database, with English/Russian localized names and descriptions. Automatic tooltips display the data-driven stat link. Rebuild via `RelicKeeper.Editor.Skills.FrostInducedJoltBuilder.Apply`; refresh only art with `RefreshArt`. Existing GUIDs are preserved.

## PixelLab assets

- Opaque 32×32 icon: job `2aae1548-8272-4501-8d42-0bd65ead7856`, gallery `10c2dc8c-f8a4-5abd-9a13-d272bfbeeede`.
- Wave: effect `34b74dce-4073-4be6-a326-809e46f95e1a`, editable step `44cc0434-4d93-4d18-8edf-d16caaa1afad`, export `dd9ff01a-2736-4033-8572-ed1e14105d56`.
- Ten 104×56 frames at 30 fps, one-shot, with origin at (52,30). Fractured, flattened frost wave with broken pressure arcs, ten shaded ice splinters, snow streaks, and angular wind trails; point filtered, no compression/mipmaps, 24 pixels per unit. The world effect stays at the cast position and scales with Area of Effect.

## Validation

Seventeen skill-specific EditMode cases cover costs, registration, opaque icon, stat-link thresholds and scope, modified resistance and existing Pushback, 250% cold conversion, facing-independent outward hit origins, compound hurtboxes, centered Area of Effect scaling and corner exclusion, crit/spell scaling and preview agreement, normal freeze chance/duration, and localized tooltip resolution.

Final Unity compilation succeeded. All 77 selected EditMode tests passed with zero failures or skips, including knockback, area scaling, Frost Wall, damage previews, descriptions/highlighting, and icon backgrounds. The Editor error log was empty.

An isolated Play Mode preview used the mage, cold staff, actual EnemyHealth/EnemyLocomotion2D targets, and manually advanced locomotion while time was paused. Nearby targets on both sides took one 250-cold hit and moved outward; the distant target was untouched. The effect was inspected at 480×270. Temporary preview code was removed and the prior autosave setting restored.

Visual revision: replaced the uniform oval with broken frost arcs, ten faceted ice splinters, angular wind trails, and snow streaks. Kept the same dimensions, anchor, frame count, timing, and gameplay data. Rechecked the revised effect in Play Mode; compilation and all 17 skill tests passed, with no Editor errors.
