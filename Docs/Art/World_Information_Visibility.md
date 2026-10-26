# Quieter world information — V20

The Settings menu now has three independent presentation choices above the
control bindings. They save immediately with the existing presentation preferences
and can be changed while playing:

| Setting | Default | Alternative |
|---|---|---|
| Service names / Названия услуг | Nearby / Рядом | Always / Всегда |
| Enemy health bars / Здоровье врагов | Combat / В бою | Always / Всегда |
| Training DPS / Урон манекену | After damage / После урона | Always / Всегда |

## Behavior

Service names are fully visible within 2.5 world units of the player, fade
smoothly between 2.5 and 4, and disappear farther away. This does not change
interaction distance, prompt text, the label's authored position or its language.
The labels remain available when editing the scene. Dungeon service NPCs use
the same proximity behavior when they reuse `HubServiceNpc`.

Enemy health bars appear immediately when that enemy acquires a target, starts
an attack or takes damage. Ailments and a non-full stun meter also keep them
readable. They remain visible for four seconds after the last relevant activity,
then fade over 0.35 seconds. A damaged but idle enemy no longer keeps its bar
forever. Death clears the activity state; normal target rescans and event
unsubscription handle removed or reused actors. Always shows every living,
onscreen enemy's bar, including undamaged enemies.

Training DPS is hidden before the first damage. Every positive damage event,
including damage over time, reveals it immediately. It remains for five seconds
after the last damage and fades over 0.35 seconds. The existing rolling
ten-second damage calculation continues unchanged; hiding does not clear it.

Player health/mana bars, Mystic Shield, the main HUD, skill slots, cooldowns,
loot labels and interaction prompts retain their existing behavior.

## Implementation

The three booleans live in `GameplayPresentationSettings` as PlayerPrefs keys,
defaulting to contextual display. They do not modify character or world saves:
`presentation_nearby_service_names`, `presentation_combat_enemy_bars`, and
`presentation_recent_dummy_dps`. HUD scale, HUD opacity and attack opacity keep
their existing values and controls.

`WorldInformationVisibility` owns the shared presentation distances and timing.
Fades are evaluated from distance or unscaled timestamps, so they do not depend
on frame rate. Existing owners update their own label/bar visibility. There is
no additional scene manager, trigger, AI rule or combat calculation.

`WorldLocalizedLabel` multiplies its authored alpha by a nonserialized
presentation opacity. Localization refreshes preserve that value. Enemy bars use
a CanvasGroup on the existing enemy view; player views do not receive the new
opacity control. The manager unsubscribes health, death and damage callbacks
when targets disappear or the manager disables.

The Settings UXML reuses the existing presentation rows and buttons. Each new
button switches between its two choices. All seven labels/choices have real
English/Russian entries in MenuLabels plus the standard runtime fallbacks.
Open controls refresh when the locale or the preference changes. No new artwork
or technical maintenance buttons were added.

## Verification

`WorldInformationVisibilityTests` covers saved choices without changing HUD
preferences, continuous fades, service label placement/text, actual dummy damage
and rolling DPS, undamaged enemy aggro, damage, inactivity, death/reset, callback
cleanup, English/Russian table entries and hidden labels across locale changes.
Live Settings captures use the actual UXML, controller and styles at 480×270.
World-label captures use the live hub camera; the editor-only capture helper
restores the previous locale and removes its temporary UI.

Unity compilation and 60 focused EditMode checks passed across this feature,
hub layout/labels, dummy damage balance, loot filtering, status presentation,
HUD shortcuts and skill tooltips. English and Russian Settings layouts were
rendered in Play Mode at 480×270; all three real button callbacks changed and
restored their preferences. The new button text fits its allotted width in both
languages. A live hub capture confirmed distant labels and idle DPS were hidden.
Temporary UI, locale and preference keys were restored, and the Editor returned
to Edit Mode. No full dungeon traversal or standalone player build was run.
