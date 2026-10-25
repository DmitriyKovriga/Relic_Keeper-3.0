# Visual depth and atmosphere — planning review

Date: 2026-10-24. Source revision: `a151bd0e`. Status: **proposal, no runtime implementation**.
The review below describes the original baseline. Subsequent implementation:
color-profile foundation for V01/V02/V03 is in `../World_Color_Profiles.md`;
the V04 contact-shadow pass is in `../Contact_Shadows.md`.
The V05 hub/Mortfall room 001 pilot is in `../Environment_Contact_Shading.md`.
V08 background-plane mist is implemented in `../Depth_Mist.md`.
V09 hub/Mortfall layered parallax is implemented in `../Layered_Parallax.md`.
V06 forge/torch lighting, terrain blocking and flame motion are in `../Local_Lighting.md`.
The V11 chimney-smoke / Mortfall ceiling-drip slice is in `../Environment_Animations.md`.
The V12 forge embers / tree leaves / torchlit dust slice is in `../Ambient_Particles.md`.
V14 is skipped by user decision; the forge-work-area and broken-winch pilot was removed.
V15 earth/stone movement and landing effects are implemented in `../Surface_Movement.md`.
This review uses the two supplied screenshots and saved project sources/assets.
No new live Editor capture, Play Mode comparison or GPU profile was performed.
Screenshot observations and proposed visual gains are art-direction judgments,
not measured improvements or evidence of the precise runtime state in the images.

## Recommended direction

Put the character into a coherent space through **value hierarchy, contact,
shared lighting logic, and layered background distance**. Use a restrained
world color profile to tie those decisions together. Darkening the whole screen
or adding particles everywhere is unlikely to solve the current composition.

Begin with one hub composition and one dungeon room. Compare still images and
movement before applying the result to every biome. Preserve the existing pixel
art and 24 PPU / 480×270 reference grid. The UI remains a separate overlay.

## What the screenshots suggest

- Hub: mountains already establish a distant scale. The dense tree band behind
  the buildings has a similarly strong dark mass; the playable plane and nearby
  background compete. The forge is an obvious opportunity for a warm visual
  anchor and for light shared by ground, architecture and character.
- Dungeon: cave silhouettes, masonry and enemies sit in similar cool/dark
  ranges. Platforms are readable, but the middle distance has few distinctive
  landmarks. More deliberate gaps, niches and local light could turn a tile
  layout into a specific place.
- In both views, grounding, common shadow colors and a clear focal hierarchy
  are more relevant to integration than a bright outline around the hero.
- Motion, actual parallax strength, fog speed and temporal shimmer cannot be
  judged from these two still frames. Existing systems must be evaluated in
  motion before deciding they are absent or need replacement.

## Confirmed project facilities and constraints

1. Unity **6000.3.11f1**, URP **17.3.0**, Renderer2D. Renderer2D already has the
   active Relic Atmosphere feature. Its checked full-screen pass blends fog,
   darkness and vignette and supports player reveal; it does not sample scene
   color for a full color-grading operation. Do not describe it as an existing LUT.
2. Saved `HubScene.unity` Main Camera references
   `Assets/Visuals/RelicFX/Preview/AtmosphereBasic.asset`, **not AtmosphereHub**.
   Its darkness/fogOpacity/vignette are 0.35/0.30/0.20; reveal radius/softness
   are 10/8 world units. With a 480-wide reference at 24 PPU, radius 10 is half
   a reference viewport width. The large clear area is worth checking before
   increasing fog: it can remove much of the effect around the player.
   This is a saved-asset finding, not a guarantee that the screenshots used it.
3. Main Camera's saved `m_RenderPostProcessing` is 0. URP asset supports HDR.
   Standard grading/bloom needs its own verified camera/profile wiring.
   The custom atmosphere feature is separate from the standard post-process toggle.
4. The hub is a layered Aseprite environment with already animated trees,
   independent fog crossfading, and roof/foliage sway. Do not rebuild these as
   duplicate systems. HubFogVisual opacity is 0.4 of its source alpha, not an
   assumption that 40% of the entire screen is fog.
5. `HubArt.mat` uses Sprite-Unlit-Default; the Buildings layer uses the custom
   unlit HubBuildingWind shader. The custom Relic Sprite FX shader also does not
   currently calculate the 2D shape-light contribution. Therefore adding Light2D
   does not automatically illuminate all current artwork. Preserve roof sway,
   sprite flash, dissolve, status effects and atlas behavior in any material pilot.
6. `WorldLight2DSetup` adds the configured lit sorting layers to **all found lights**.
   Simply authoring a light with a smaller layer target set may not be enough.
   Selective world/background lighting needs a deliberate policy before rollout.
   Global Light 2D in the saved scene has intensity 1.
7. `GroundingVisualController` already provides contact shadows, run/landing/
   jump/dash effects; PlayerMovement and EnemyEntity add it automatically.
   Start with tuning and surface presets, not a second shadow/dust component.
8. `ParallaxEffect` exists; **1 follows the camera, 0 stays world-fixed**.
   The saved DungeonSharedBackground uses 0.9 on X and Y. DungeonController
   swaps/alines one background renderer and restores it on hub return. Multi-layer
   biome backgrounds require extending that lifecycle and resetting each anchor.
   The checked hub environment prefab has no ParallaxEffect component: source
   artwork layers exist, but a multi-speed hub rig is not thereby confirmed.
9. AtmosphereProfile declares backgroundMist, shafts, wispStrength, lantern and
   motes. A RelicDepthMist shader exists. No checked runtime C# binding consumes
   those new fields for depth mist/lantern/motes; the current full-screen feature
   does not pass them. Treat these as partial groundwork requiring integration,
   not as working inspector sliders. A matching property/asset alone proves no effect.
10. Render order intentionally places Hero above ordinary environment. A source
    art group named Foreground is not proof of hero occlusion. True foreground
    overlap needs a render-stack category/policy and readability behavior.
11. Hub layout uses composite Buildings and TreesAndPlants drawing layers. A
    light or occlusion that needs an individual object may require a mask or
    artwork separation; per-object editing is not automatically cheap.
12. Existing working changes in Addressables link files, HubPalette and
    ProjectSettings were left intact. This task writes only this documentation
    directory and unmodified copies of the user reference screenshots.

## How to read the estimates

Visual gain: 1 minor accent, 3 noticeable improvement in context, 5 changes the
scene's spatial reading or identity. This is expected art-direction impact,
not a promised score from users. Effects interact; scores must not be added.

Setup: 1 existing setting/prefab tuning, 2 small reusable content/preset work,
3 a modest reusable system or artwork split, 4 renderer/material integration,
5 a new substantial content-production pipeline.

Maintenance: 1 a common preset with little per-room work, 2 a reusable prefab
with occasional content checks, 3 authoring/verification per biome or room type,
4 recurring geometry/material integration, 5 recurring work per asset/animation.

Prototype days are rough working-day estimates for an experienced developer,
using existing art, for **one hub view plus one representative dungeon room**.
They exclude new painted/animated assets unless explicitly stated, broad content
rollout and hardware certification. They are not measured project estimates.
Shared work means the rows should not simply be summed. GPU cost must be measured
on a chosen target device; maintenance difficulty and rendering cost are different.

## Option matrix

| ID | Option | Visual gain / 5 | Setup / 5 | Maintenance / 5 | Prototype days |
|---|---|---:|---:|---:|---|
| V01 | Разделить яркость и контраст планов | 5 | 2 | 1 | 1–2 |
| V02 | Общий цветовой профиль мира | 4 | 2 | 1 | 0.5–2 |
| V03 | Согласовать героя с палитрой локации | 4 | 2 | 2 | 1–2 |
| V04 | Довести контактную тень | 3 | 1 | 1 | 0.25–0.5 |
| V05 | Нарисованные тени в местах контакта | 4 | 2 | 2 | 1–2 + арт |
| V06 | Локальный 2D-свет от источников | 5 | 4 | 3 | 3–6 + проверка материалов |
| V07 | Художественные световые пятна | 3 | 2 | 2 | 0.5–1.5 + арт |
| V08 | Дымка между планами | 5 | 3 | 2 | 2–3 |
| V09 | Несколько скоростей параллакса | 4 | 3 | 2 | 1–3 + разделение арта |
| V10 | Редкие элементы переднего плана | 4 | 3 | 3 | 1–3 + арт |
| V11 | Точечная анимация окружения | 3 | 2 | 2 | 1–2 + арт |
| V12 | Редкие частицы воздуха | 3 | 2 | 2 | 1–2 |
| V13 | Вариации поверхностей и крупных форм | 4 | 3 | 3 | 2–4 + арт |
| V14 | Ориентиры и история места | 5 | 3 | 3 | 1–3 на набор + арт |
| V15 | Реакция поверхности на движение | 3 | 2 | 2 | 0.5–2 |
| V16 | Редкие световые лучи | 3 | 3 | 2 | 1–3 + арт |
| V17 | Слабое выборочное свечение | 2 | 2 | 2 | 0.5–1.5 |
| V18 | Настоящие отбрасываемые 2D-тени | 4 | 4 | 4 | 3–6 + интеграция комнат |
| V19 | Normal maps для 2D-поверхностей | 3 | 5 | 5 | 5–10 + существенный арт |
| V20 | Спокойнее информационный слой | 3 | 2 | 2 | 1–3 |

## Candidate art directions

| Direction | Hub | Dungeon | Tradeoff |
|---|---|---|---|
| Cool world, warm inhabited places — recommended | Cool forest and distant mountains, warm forge/windows, quieter sky | Cool stone/cave depth, occasional amber lamps and distinct magical accents | Familiar restrained palette; requires actual focal placement, not one orange filter |
| Quiet misty dark fantasy | Muted greens, pale remote landscape, sparse warm points | Layered silhouettes and broken light through old structures | Strong mood; avoid crushing platform/enemy contrast |
| More colorful graphic adventure | Clear warm/cool blocks, brighter local accents | Distinct chamber color motifs and restrained dark masses | More immediately lively; needs palette discipline to avoid a busy combat screen |

These are alternative directions, not three effects to enable together. None
requires replacing all current art as a first step. Choose by a representative
side-by-side capture after the basic spatial hierarchy is improved.

## Suggested packages and order

**First experiment — base integration:** V01/V02/V03/V04/V05/V08/V09. Tune
existing facilities, separate background distance, use a shared palette and
contact. If layering requires too much art work, test V01/V04/V05 first on the
existing assets. V02 stays restrained while the material/value relationships settle.

**Current chosen direction:** base + V06/V11/V12. Light has visible sources
and a few ambient events. V14 landmarks are skipped by user decision.
V06 begins with a material-compatibility pilot, not a global material swap.

**Alternative with lower lighting integration cost:** base + V07/V11/V14.
Authored light patches work with the current unlit artwork but do not provide
true local illumination on a moving actor. This is a different tradeoff, not
an equivalent technical replacement for V06. Avoid doubling V06 and V07 everywhere.

After the first comparison, consider V10/V13/V15/V16 in the locations that need
them. V17 is finishing polish. Defer V18/V19 until the base scene already reads
well. Full dynamic shadows, normal maps, day/night simulation, depth-of-field,
motion blur, chromatic aberration and full-screen grain are weak first investments
for the stated problem. A dark vignette is not a substitute for visual depth.

## Implementation boundary for the next coding task

- Implement the package/direction authorized by the subsequent user request.
  This document is a reviewable proposal, not implementation authorization;
  it introduces no extra approval gate when the later request is already clear.
- Reuse AtmosphereController/Profile, render-stack conventions, grounding and
  parallax instead of introducing parallel managers for each effect.
- Keep two distinct concepts: a global/world grade and a layer-placed background
  haze. The current screen-space reveal/fog should not be assumed to know object depth.
- For the initial light pilot: one forge/torch, one wall/floor section, one player
  class and one enemy. Verify all required material features survive, then widen.
- Make one profile per biome/location and explicitly apply/restore it during
  DungeonController transitions. No room should inherit a stale hub tint/fog.
- Package lamps, smoke, ambient emitters and contact darkening into shared
  prefabs. Each source owns its visuals; avoid manually paired disconnected objects.
- For new background layers, preserve seams and anchor reset at room transitions,
  camera bounds, hub return, jumps and teleports. Do not parent moving background
  art to a collider root or shift composite Buildings as a parallax plane.
- Respect existing MPB ownership and shared materials; do not create material
  instances every frame or overwrite status-effect properties.
- UI remains an overlay independent of world grading. No grading pass may change
  HP bar colors or inventory readability accidentally. World-space labels need
  an explicit decision because they are already rendered with the world.

## Acceptance examples for a vertical slice

1. Static comparisons at the same camera/player pose show clearly separated
   distant, middle and playable planes without losing the existing platform edge.
2. Player, weapon and enemy silhouettes remain readable in both the brightest
   VFX burst and the darkest room region; color alone is not required for detection.
3. At the forge, warmth is attached to a visible source. If V06 is chosen, a
   passing actor and nearby surface respond consistently; for V07 the documented
   absence of actor illumination is accepted rather than claimed otherwise.
4. Moving laterally, jumping, dashing and teleporting reveal no new seams,
   detached shadows, subpixel shimmer or large camera-locked fog patterns.
5. Fog does not cover a melee windup, small projectile or landing edge. Keep
   foreground forms away from NPC prompts and compulsory jump trajectories.
6. Hub -> dungeon -> next room -> hub restores background layers, profiles,
   anchors and ambient emitters. Disable/reenable and restart do not duplicate effects.
7. Compare a clean view and a crowded room on a named target machine; record
   CPU/GPU frame times, draw calls and transparent overdraw with effects on/off.
   No FPS, millisecond cost or universal light count is promised by this plan.
8. A second room can inherit most of the result by choosing a profile and
   placing a few shared prefabs. If each wall needs a bespoke mask, simplify
   the approach before rollout. That is the maintenance success criterion.

When implementing, use Unity Bridge compile and narrow relevant EditMode checks
such as GroundShadowTests, HubFogTests and RelicVisualFxRenderTests when affected,
plus manual Play Mode comparisons. No Unity tests were needed or claimed for
this planning-only task.

## Option details

### V01: Разделить яркость и контраст планов

Дальний фон светлее/спокойнее по контрасту, средний план плотнее, игровой план с самым ясным силуэтом. Разнести крупные массы, а не повышать контраст всей картинки.

- Hub: Сделать лес за зданиями менее плотным, сохранить тёмные проёмы и читаемый край земли.
- Dungeon: Отделить дальнюю пещеру от кладки, врагов и границы платформ.
- Reuse / prerequisite: Существующие слои рисунка и render stack; доступ к отдельным планам.
- Ongoing work: Правило и несколько общих материалов на биом; новая комната наследует их.
- Main tradeoff: Сплошное затемнение фона может снова склеить силуэты; проверять в оттенках серого.

### V02: Общий цветовой профиль мира

Мягкая коррекция температуры, контраста и насыщенности мира; отдельный профиль хаба и подземелья. HUD остаётся исходным.

- Hub: Прохладные тени леса и тёплые охристые акценты у жилья.
- Dungeon: Холодный фон с локальными янтарными источниками; сохранить цвет атак.
- Reuse / prerequisite: URP Color Adjustments/Color Curves либо целевая коррекция материалов. На сохранённой Main Camera штатная постобработка выключена.
- Ongoing work: Один профиль на биом и проверка переходов; не индивидуальный фильтр каждой комнаты.
- Main tradeoff: Глобальный фильтр сам по себе не создаёт расстояние и контакт. Не применять один сильный tint ко всему кадру.

### V03: Согласовать героя с палитрой локации

Согласовать тени тела, оружия и окружения; держать читаемый силуэт. Для первой версии достаточно постоянного мягкого оттенка биома, без новой обводки.

- Hub: Слегка прохладные тени героя рядом с зелёно-синим лесом.
- Dungeon: Герой сохраняет контраст, но не выглядит освещённым другим дневным небом.
- Reuse / prerequisite: SpriteFxController и общие материалы уже есть; выяснить текущий материал конкретного персонажа.
- Ongoing work: Проверять три класса, оружие, flash и статусы в каждом профиле; не перекрашивать каждый кадр вручную.
- Main tradeoff: Слишком яркая обводка усиливает эффект наклейки. Общий tint не заменяет реакцию на локальный свет.

### V04: Довести контактную тень

Настроить существующую тень по ширине стоп, плотности и расстоянию до поверхности; сравнить стояние, склон, прыжок и край платформы.

- Hub: Небольшой плотный контакт под ногами и у основания неподвижных объектов.
- Dungeon: Контакт на узких уступах без тени, зависшей над нижним этажом.
- Reuse / prerequisite: GroundingVisualController и GroundShadowTests уже существуют; не создавать вторую систему.
- Ongoing work: Общие пресеты размеров. Проверка нового типа тела, а не каждой комнаты.
- Main tradeoff: Одна большая чёрная овальная тень под всем героем выглядит отдельным стикером.

### V05: Нарисованные тени в местах контакта

Общие затемняющие спрайты/тайлы под карнизами, у стен, под бочками, в стыках кладки. Это художественная имитация occlusion, не SSAO.

- Hub: Тень под соломенной крышей и у примыкания зданий к земле.
- Dungeon: Тёмные нижние стороны навесов, углубления ниш, контакт камня и пола.
- Reuse / prerequisite: Небольшой набор спрайтов и правила размещения; текущие Sorting Layers.
- Ongoing work: Добавлять тень в состав префаба декора или автотайла, а не расставлять заново по всей комнате.
- Main tradeoff: Не затемнять верхнюю кромку платформы до потери читаемости.

### V06: Локальный 2D-свет от источников

Кузница, лампы, факелы и магические объекты освещают совместимые поверхности и героя, оставляя прохладный заполняющий свет.

- Hub: Огонь кузницы окрашивает ближайшую стену, землю и проходящего героя.
- Dungeon: Читаемые островки света у факелов и ниш вместо равномерной темноты.
- Reuse / prerequisite: HubArt и HubBuildingWind сейчас unlit; Relic Sprite FX также не рассчитывает 2D-light response. Нужен совместимый путь с сохранением sway/flash/status FX.
- Ongoing work: Единые light-prefab и lit-материалы; новый тип материала и комнаты требует визуальной проверки.
- Main tradeoff: Простое переключение на Sprite-Lit может потерять существующие эффекты. Много перекрытий света повышает стоимость рендера.

### V07: Художественные световые пятна

Привязанные к источнику мягкие цветные пятна на стене/полу, emission внутри огня. Дешёвый художественный вариант для статичного окружения.

- Hub: Тёплая зона у кузницы и окна, без сложных отбрасываемых теней.
- Dungeon: Янтарное пятно за факелом или свет от рунической ниши.
- Reuse / prerequisite: Существующие unlit материалы и общие спрайты; настоящего освещения героя этот вариант не даёт.
- Ongoing work: Включать пятно в префаб источника; перемещение декора переносит световой акцент.
- Main tradeoff: Не складывать с V06 до пересвета. Световой спрайт должен быть связан с видимым источником.

### V08: Дымка между планами

Разместить редкую дымку между дальним фоном и игровым слоем. Герой и кромки платформ остаются перед ней; это не серый fullscreen-слой поверх всех.

- Hub: Полоса низкого тумана перед дальним лесом, просветы между деревьями.
- Dungeon: Разные по глубине силуэты пещеры и провалы, а не одинаковая пелена на врагах.
- Reuse / prerequisite: HubFogVisual уже работает в хабе. RelicDepthMist.shader есть, но runtime-привязка к параметрам профиля в проверенном C# не найдена.
- Ongoing work: Один фоновый слой/пресет на биом; новая геометрия обычно не требует ручных масок.
- Main tradeoff: Если слой окажется перед всеми персонажами, контраст упадёт и глубина снова исчезнет.

### V09: Несколько скоростей параллакса

Отдельные скорости для неба, гор, дальнего леса и ближнего фона. В подземелье — хотя бы дальняя пещера и промежуточные силуэты.

- Hub: Горы почти неподвижны на экране, лес смещается заметнее, здания остаются привязаны к земле.
- Dungeon: Провалы пещеры и ближние колонны двигаются по-разному.
- Reuse / prerequisite: ParallaxEffect уже есть. DungeonController меняет один общий background renderer; для нескольких слоёв его надо расширить.
- Ongoing work: Набор фоновых слоёв на биом; проверка швов, вертикального движения и входа в каждую комнату.
- Main tradeoff: У существующего скрипта 1 означает движение вместе с камерой. Нельзя сдвигать коллизии и весь HubEnvironment.

### V10: Редкие элементы переднего плана

Крупные тёмные формы у краёв кадра: ветви, корни, край арки. Они создают ощущение пространства перед камерой.

- Hub: Одна ветвь или край навеса у края кадра вместо сплошной рамки.
- Dungeon: Края колонн и корни только вне линии боя и маршрута прыжка.
- Reuse / prerequisite: Герой сейчас намеренно сортируется выше обычного мира. Реальное перекрытие героя требует отдельного решения render stack/occluder fade.
- Ongoing work: Проверка каждой композиции, особенно узких платформ, NPC и дальних телеграфов.
- Main tradeoff: Название Foreground в исходном арте не гарантирует слой перед героем. Начать с неперекрывающих краевых форм.

### V11: Точечная анимация окружения

Дым трубы, редкая капля, покачивание вывески, нерегулярное пламя. Разная фаза и спокойный ритм.

- Hub: Дым над тёплым домом; существующие деревья, туман и крыши уже анимируются.
- Dungeon: Капля под сводом, огонь в одной нише, редкое осыпание пыли.
- Reuse / prerequisite: Aseprite/Animator и существующий HubBuildingWind; часть эффекта уже реализована.
- Ongoing work: Несколько общих петель в префабах. Новую локацию не заполнять уникальными аниматорами.
- Main tradeoff: Не раскачивать кладку и весь мир. Статичный скриншот не показывает, насколько текущих анимаций достаточно.

### V12: Редкие частицы воздуха

Пыль в освещённой нише, искры у огня, несколько листьев. Частицы привязаны к месту и источнику.

- Hub: Искры кузницы и редкие листья, без постоянного снега из частиц.
- Dungeon: Пыль только в световом пучке или возле разрушенного свода.
- Reuse / prerequisite: Пиксельные спрайты и существующие правила VFX; motes в AtmosphereProfile пока не подтверждены работающим runtime-проходом.
- Ongoing work: Общие emitter-префабы, лимит видимых эффектов и сброс при переходе комнаты.
- Main tradeoff: Сплошной слой частиц снижает читаемость и создаёт overdraw, а не глубину.

### V13: Вариации поверхностей и крупных форм

Чередовать целые и разрушенные фрагменты, корни, влажные участки, трещины и редкий мох. Важнее крупные различия силуэта, чем мелкий шум.

- Hub: Разный контакт земли со стеной и локальные дорожки у сервисов.
- Dungeon: Разбитая арка, заваленная ниша, корни в одном участке вместо одинаковых стен везде.
- Reuse / prerequisite: Работа художника и несколько повторно используемых правил/префабов.
- Ongoing work: Набор тайлов и декора на биом плюс правила генерации; потребуется проверять новые формы комнат.
- Main tradeoff: Слишком частые мелкие детали конкурируют с врагами и делают сцену пёстрой.

### V14: Ориентиры и история места

**Skipped by user decision.** The experimental forge props and broken winch were removed; do not reinstall this pilot when continuing the plan.

Один запоминающийся объект на композицию: сломанный механизм, корни вокруг алтаря, огромная запертая дверь. Понятная функция и масштаб мира.

- Hub: Кузница как световой и смысловой центр; следы использования склада и таверны.
- Dungeon: Один древний объект и следы разрушения, которые связывают фон и платформы.
- Reuse / prerequisite: Композиция и арт; освещение/дымка усиливают результат, но не заменяют объект.
- Ongoing work: Собирать небольшие повторяемые наборы. Новому биому нужен собственный набор, не каждому помещению новый рисунок.
- Main tradeoff: Глобальный фильтр не исправит отсутствие смыслового центра кадра.

### V15: Реакция поверхности на движение

Настроить уже существующие пыль, приземление и след рывка под поверхности: земля, камень, пепел.

- Hub: Небольшая земляная пыль при старте и посадке.
- Dungeon: Каменная крошка на посадке, минимум пыли на обычном беге.
- Reuse / prerequisite: GroundingVisualController уже автоматически добавляется игроку и врагам; добавить/проверить surface-пресеты вместо дубликата.
- Ongoing work: Общие категории поверхности, не отдельный VFX для каждой комнаты.
- Main tradeoff: Не превращать каждый шаг в взрыв. Эффект лучше оценивать в движении.

### V16: Редкие световые лучи

Пиксельные пучки от конкретного окна или трещины, за героями и врагами. Для первого шага достаточно художественного спрайта/плоскости.

- Hub: Слабый косой свет из проёма, если источник оправдан композицией.
- Dungeon: Луч из разлома потолка с пылью только внутри него.
- Reuse / prerequisite: RelicDepthMist.shader содержит заготовку лучей; автоматическое подключение профиля не подтверждено.
- Ongoing work: Пара префабов с точкой источника; проверять, не проходит ли пучок через глухую стену.
- Main tradeoff: Полноценная объёмная симуляция для этого результата не обязательна.

### V17: Слабое выборочное свечение

Небольшой bloom только у огня, рун и некоторых магических эффектов; обычные поверхности и HUD остаются резкими.

- Hub: Чуть больше свечения от огня кузницы.
- Dungeon: Ограниченный ореол руны; враги и мелкие платформы не размываются.
- Reuse / prerequisite: HDR включён в URP asset, но постобработка Main Camera выключена. Проверить совместимость с Pixel Perfect и UI.
- Ongoing work: Профиль на биом и проверка новых HDR/VFX-материалов.
- Main tradeoff: Bloom не освещает поверхность сам по себе. Большие ореолы скрывают пиксельную графику.

### V18: Настоящие отбрасываемые 2D-тени

ShadowCaster2D на выбранных крупных формах и несколько источников. Начать с одной арки и одной лампы.

- Hub: Большая тень от навеса рядом с лампой, если выбран динамический свет.
- Dungeon: Тень колонны/арки, привязанная к видимому факелу.
- Reuse / prerequisite: V06 и совместимые lit-материалы. Физические коллайдеры не следует автоматически считать хорошими теневыми силуэтами.
- Ongoing work: Новая геометрия, разрушаемые объекты и генерация требуют обновления caster-форм и контроля стоимости.
- Main tradeoff: Для множества мобов и комнат это большой постоянный расход поддержки; сначала измерить GPU.

### V19: Normal maps для 2D-поверхностей

Нормали дают свету ощущение объёма на камне, герое и крупных объектах. Начать с одного материала, не всей игры.

- Hub: Небольшой объём у отдельных каменных элементов.
- Dungeon: Рельеф кладки рядом с факелом.
- Reuse / prerequisite: V06; дополнительные текстуры и проверенный процесс экспорта/атласов.
- Ongoing work: Подготовка и сверка карт для новых тайлов, оружия и кадров анимации; затраты растут вместе с контентом.
- Main tradeoff: Случайные автонормали часто дают пластиковый вид. Слабый первый шаг для задачи общей глубины.

### V20: Спокойнее информационный слой

Проверить постоянную заметность полосок, подписей сервисов и служебного DPS. Вне боя часть данных может становиться менее настойчивой.

- Hub: Чуть меньше постоянных надписей поверх поселения.
- Dungeon: Информация не спорит с силуэтами врагов и световым центром комнаты.
- Reuse / prerequisite: Отдельный UI-проход по существующему pixel-ui workflow; не менять интерфейс в рамках этого плана.
- Ongoing work: Единые правила состояний и проверка всех окон, боя и локализации.
- Main tradeoff: Не прятать нужные cooldown/HP и не уменьшать читаемость текста ради красоты.


## Evidence and references

Graphify MCP was used to locate AtmosphereController, RelicAtmosphereFeature,
WorldLight2DSetup, GroundingVisualController, HubFogVisual and ParallaxEffect.
Their sources and serialized scene/material/profile references were read directly.
The graph is discovery data, not proof of scene wiring. Exact snapshot hashes
for the central inspected files are stored in plan.json.

Project references (paths relative to repository root):

- Docs/Hub20Layout.md — composite source layers, fog, sway and layout constraints.
- Docs/RelicVisualFX.md — current sprite FX and camera/UI atmosphere contract.
- Assets/Scripts/Visuals/RENDER_STACK_GUIDE.md — ordering and actor visibility.
- Assets/Scenes/HubScene.unity — saved camera, profile, global light and background.
- Assets/Settings/Renderer2D.asset — active custom feature.
- Assets/Scripts/Visuals/Atmosphere/ — actual feature/controller/profile consumers.
- Assets/Shaders/RelicFX/RelicAtmosphere.shader and RelicDepthMist.shader.
- Assets/Editor/Hub/Hub20Builder.cs and HubBuildingWindBuilder.cs — materials/layout.
- Assets/Scripts/Dungeon/DungeonController.cs — one-background transition lifecycle.

Official Unity 6.3 documentation checked on 2026-10-24:

- [Color Adjustments](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/Post-Processing-Color-Adjustments.html): tone, exposure, contrast, tint and saturation of the rendered image.
- [2D light-compatible shaders](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/PrepShader.html): Sprite-Lit and custom Sprite Lit shader paths; an unlit sprite is not made light-reactive merely by placing a Light2D.
- [Light 2D reference](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/2DLightProperties.html): light shape/blending and supporting options. These facilities do not determine their measured cost in this project.
