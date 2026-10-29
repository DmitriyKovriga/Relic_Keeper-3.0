"""Build the Russian review page and English planning handoff. No gameplay edits."""
from pathlib import Path
from html import escape as h
import hashlib
import json

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
DATE = "2026-10-29"

def item(id, title, name, gain, setup, upkeep, days, stage, baseline, idea, example,
         maintenance, scope, acceptance, hooks, boundary="presentation"):
    return dict(id=id, title=title, name=name, gain=gain, setup=setup, upkeep=upkeep,
                prototypeDays=days, stage=stage, baseline=baseline, idea=idea,
                example=example, maintenance=maintenance, scope=scope,
                acceptance=acceptance, hooks=hooks, boundary=boundary)

ITEMS = [
    item("J01", "Бокс у портала приятно собирает добычу", "Exit loot-container feedback", 4, 2, 2, "2–4", "first",
         "Лут мобов сразу записывается в один бокс у портала. RoomLootCollector запускает декоративный золотой перелёт для видимого предмета; сейчас бокс имеет фоновое пульсирующее свечение. Предмет уже хранится внутри до завершения перелёта. Россыпь на земле — запасной путь, не основа комнатного лута.",
         "Сохранить один удобный бокс и усилить ощущение, что он принимает и накапливает добычу. Первый прототип: отклик поглощения на прилёт и один редкий акцент ценной находки. Остальные варианты ниже — для выбора, не обязательный комплект.",
         "От моба идёт уже существующий перелёт → перед боксом след стягивается к точке приёма → бокс коротко откликается и успокаивается. Пачка добычи даёт один более плотный отклик. Бокс можно открыть в любой момент.",
         "Общие профили обычного/ценного поступления. Нужен косметический сигнал прилёта: сейчас FlightVisual не сообщает о нём боксу и не получает редкость. Объединять частые прилёты; скрытый фильтром лут не выдавать свечением, счётчиком или сменой состояния.",
         "Preserve the room's single container near its exit. WorldItemDropService routes to RoomLootCollector.TryStore: the real rolled item is inserted before a cosmetic flight starts; do not delay ownership or interaction until arrival. Extend the existing flight and cache visuals, not loose ground pickup or a new reward chest. Prototype a brief visual-only intake response and a bounded valuable-arrival accent. A cosmetic arrival hook and small presentation metadata may be needed: Spawn currently accepts only start/target and its completion does not notify the cache. Keep those separate from inventory transfer, Changed events and reward creation. Cancel safely when the box/room disappears or its contents have already been taken. Do not show new-loot celebrations for player-discarded items or ordinary UI refreshes; carry an origin flag only if needed to distinguish them.",
         "There remains exactly one room container, with the same item identities/count and location policy. It remains usable while flights run or the portal is locked. A burst of twenty items merges feedback without queuing twenty animations. Hidden items cause no flight, rarity accent or accumulation change; changing the filter recomputes visible state without claiming a new drop. Taking all loot or leaving the room mid-flight loses/duplicates nothing and leaves no orphan effect. Test full inventory and simultaneous arrival while the cache UI is open.",
         ["Assets/Scripts/Items/World/WorldItemDropService.cs", "Assets/Scripts/Items/World/RoomLootCollector.cs", "Assets/Scripts/Items/World/WorldLootCache.cs", "Assets/Scripts/Items/World/WorldLootCacheFlightVisual.cs", "Assets/Tests/EditMode/Editor/RoomLootCollectorTests.cs"]),
    item("J02", "Улучшение билда ощущается действием", "Upgrade transactions", 4, 2, 2, "2–4", "first",
         "Крафт уже обновляет инвентарь и открывает tooltip результата. Повышение уровня имеет flash, стрелку и баннер. Это развитие существующей подачи, а не новая система прогрессии.",
         "После экипировки, крафта или покупки пассивки подтверждать конкретный результат: короткий акцент слота/нода и выделение действительно изменившихся строк. Не трясти всё окно и не считать каждое изменение улучшением.",
         "Применил сферу → видишь, какие аффиксы изменились. Взял notable → акцент большого нода; обычная малая пассивка получает более тихий отклик.",
         "Общие состояния success/change/reject. Основная цена — корректно сопоставлять старые и новые данные, включая отрицательный результат и EN/RU.",
         "Prototype one equipment swap, one orb operation and one passive allocation using committed results. Keep existing crafted-item tooltip and level-up feedback. For randomized affixes show actual before/after changes without claiming a universal better/worse result. Do not add a broad stat-diff engine for the whole character in the first slice.",
         "Successful and rejected operations look different. Repeated crafting does not queue animations or move the pointer target; displayed differences match the committed item, including removed or worsened affixes. Keyboard navigation and both locales remain usable.",
         ["Assets/UI/Inventory/InventoryUI.Crafting.cs", "Assets/UI/PassiveTree/PassiveTreeUI.cs", "Assets/Scripts/Visuals/PlayerLevelUpFeedback.cs"]),
    item("J03", "Умения отвечают на намерение игрока", "Skill state feedback", 4, 2, 1, "1–3", "first",
         "У слотов уже есть затемнение cooldown, число секунд и цена маны. Наличие этих элементов не доказывает, что попытка применения и переход к готовности хорошо читаются в бою.",
         "Проверить и при необходимости добавить локальный отклик принятого применения, единичный акцент окончания значимого cooldown и понятный отказ при нехватке маны. Для спамящейся основной атаки постоянные импульсы не нужны.",
         "Нажал специальную атаку без маны → отозвались её слот и стоимость; умение восстановилось → один спокойный акцент готовности, а не мигание до следующего нажатия.",
         "Одна логика состояний для шести слотов. Отказ должен знать реальную причину; частые повторные нажатия объединяются.",
         "Audit existing state cues, then add only missing transitions. Read actual cast acceptance/rejection and current cooldown/mana; do not infer success from raw input. A structured rejection reason may need a small runtime extension. Avoid ready pulses for zero-cooldown primaries, initialization, weapon swaps and mere HUD rebuilds. Do not change input buffering, mana, cooldown or cancellation rules.",
         "No accepted-cast cue on rejection. No ready cue on opening a menu or restoring a swapped weapon. Repeated denied presses remain bounded and communicate the correct reason; meaningful long-cooldown readiness is visible without watching a timer continuously.",
         ["Assets/UI/SkillSlots/UISkillSlot.cs", "Assets/UI/PlayerHUD/HUDController.cs", "Assets/Scripts/Skills/PlayerSkillManager.cs", "Assets/Scripts/Skills/Logic/SkillBehaviour.cs"]),
    item("J04", "Игрок видит, как работает его билд", "Build cause and effect", 5, 3, 3, "3–6", "second",
         "Уже есть StatusEffectPresentation, отдельные эффекты ignite/shock, HUD статусов и отклик защиты. Не предлагается заново создавать всю индикацию состояний.",
         "Проверить три показательные цепочки: применение яда и его продолжение; накопление/срабатывание shock; расход и возврат Mystic Shield. Дорабатывать только места, где причина и результат теряются.",
         "После атаки понятно, что яд продолжает работать; накопление shock отличается от срабатывания. Эффект щита сообщает о защитном ресурсе, а не выглядит ещё одним попаданием.",
         "Дороже общего UI: каждая механика имеет свои состояния. Ограничить пилот тремя цепочками, переиспользовать цвета/формы; не плодить текст и вспышки на каждом тике.",
         "Audit three existing chains before authoring. Present application, sustained state and meaningful transition with distinct bounded cues, using actual status/resource events. Preserve the implemented defense response and its ownership. Do not present DoT ticks as fresh direct hits, show one effect per poison stack, invent a fifth hit-damage channel or add a new proc mechanic.",
         "In an unlabelled clip the reviewer can distinguish application, ongoing effect and major transition. Ten afflicted enemies and repeated procs do not obscure attack intent. A fully readable existing chain requires no new effect.",
         ["Assets/Scripts/StatusEffects/StatusEffectPresentation.cs", "Assets/Scripts/StatusEffects/ShockVisualController.cs", "Assets/Scripts/StatusEffects/IgniteVisualController.cs", "Assets/Scripts/Stats/MysticShieldController.cs"]),
    item("J05", "У врага читаются намерение и уязвимость", "Enemy intent and recovery", 5, 3, 3, "3–5", "second",
         "EnemyAttackTelegraphVfx уже рисует предупреждение, а реакции попадания сделаны. Нельзя считать врагов лишёнными телеграфов.",
         "На двух частых врагах проверить различимость подготовки, опасного действия и восстановления. Усилить движение силуэта или ритм предупреждения там, где один красный сигнал не объясняет, что сейчас произойдёт.",
         "Тяжёлый противник собирается перед ударом и заметно завершает его; игрок видит момент для ответа. Дальний враг не выглядит так же, как враг, готовящий контактный рывок.",
         "Настройка на семейство поведения, а не на каждый скин. Цена растёт с числом уникальных атак; предупреждения нельзя растворять в декоративной сочности.",
         "Choose two enemy families and compare existing anticipation/active/recovery at normal speed and in crowds. Adjust presentation against existing authoritative attack windows. Use timing and silhouette, not color alone. Preserve gameplay commitments and existing hit-recoil ownership; new stagger or longer recovery belongs to a separate balance proposal.",
         "The player can identify the dangerous action and existing recovery window at 480x270 in both effect settings. Telegraphs do not claim an opening that gameplay does not offer. If the current timings are themselves the problem, stop treating that as cosmetic work.",
         ["Assets/Scripts/Enemy/EnemyAttackTelegraphVfx.cs", "Assets/Scripts/Enemy/EnemyAnimationBridge.cs"], "presentation; timing redesign needs separate balance approval"),
    item("J06", "Комната отпускает игрока после боя", "Room resolution and exits", 4, 2, 1, "1–3", "first",
         "Баннеры открытия портала и зачистки уже есть. В RoomController портал открывается примерно после половины убийств, а полная зачистка — отдельное событие. Портал включается через SetActive.",
         "Развести два момента визуально: выход стал доступен и бой полностью завершён. Добавить порталу короткое локальное проявление; при зачистке связать существующее уведомление с уже положенной наградой.",
         "На середине боя выход мягко проявился и остаётся доступным. После последнего врага приходит более спокойное завершение; поздний level-up не стирает важную информацию о выходе.",
         "Общий профиль портала и правила приоритетов сообщений. Проверять комнаты без награды/врагов и ситуацию, когда события совпали.",
         "Extend current portal activation and banner handling; keep unlock and clear separate. Prefer a local portal cue, with optional brief nonblocking navigation hint only if the exit is offscreen. Coordinate overlapping messages rather than stacking banners. Use only rewards already authorized by the room modifiers. Preserve the kill threshold and reward count; never delay portal interaction until cosmetic completion.",
         "Unlock can be recognized while enemies remain alive. Full clear, unlock and level-up coinciding do not overwrite essential information. Empty rooms and no-reward rooms are truthful; returning to a room does not replay its reward or unlock celebration.",
         ["Assets/Scripts/Dungeon/RoomController.cs", "Assets/Scripts/Dungeon/DungeonPortal.cs", "Assets/UI/Dungeon/RoomClearedBanner.cs"]),
    item("J07", "У забега есть нарастание и передышки", "Encounter rhythm", 5, 3, 3, "3–6", "second",
         "Есть комнаты, модификаторы и спавн наград. Распределение напряжения в реальном забеге этим просмотром не измерено: повторяемость пока гипотеза для проверки.",
         "Сравнить один небольшой маршрут: плотный бой, более просторная комната с другой геометрией, затем выразительная цель/награда. Варьировать существующие сочетания врагов и дистанции, а не только их количество.",
         "Комната с дальниками требует движения по вертикали; следующая даёт короткую передышку и выбор снаряжения. Сильный бой выделяется на фоне спокойных участков.",
         "Не требует новых эффектов, но требует дизайнерского плейтеста при изменении пулов врагов, билда игрока и экономики наград.",
         "First record the density, traversal time and reward intervals of one normal route. If contrast is weak, author an isolated three-room comparison with existing enemies and geometry. This is a content/balance experiment, not a hidden change to the encounter generator, enemy counts, reward frequency or progression.",
         "At comparable character power, the route has recognizably different demands without adding unavoidable damage or empty travel. Check melee and ranged builds and compare time-to-clear, damage taken and perceived pacing; do not accept novelty alone as a win.",
         ["Assets/Scripts/Dungeon/RoomController.cs", "Assets/Scripts/Dungeon/DungeonRoomServiceSpawner.cs"], "content/balance experiment; unselected"),
    item("J08", "Редкие сильные моменты: элита и босс", "Elite and boss peaks", 4, 4, 4, "4–8", "later",
         "Смерти и финальный удар уже доработаны. BossRoomController активирует выход при смерти босса; это не аудит всех атак, фаз или постановки существующих боссов.",
         "Выбрать одного существующего босса: короткое узнаваемое появление, ясный переход существующей фазы, завершение и выдача положенной награды. Элите достаточно менее громкого узнаваемого акцента.",
         "Игрок понимает, что начался особый бой, но управление остаётся у него. Победа ощущается завершённым эпизодом, а не исчезновением большого обычного врага.",
         "Высокая цена: отдельные атаки, размеры, комнаты и особые состояния. Поэтому один босс после улучшений, которые работают во всём забеге.",
         "Stage one existing boss encounter using its real supported states. Do not assume a phase system or add a phase solely to justify an effect. Reuse lethal-hit feedback and portal/reward cues. No mandatory camera takeover, global slow motion, invulnerability, delayed loot or new boss behavior in the presentation pilot.",
         "Entry, any actual phase transition and victory remain readable with reduced decoration. Death and portal activation execute once, including immediate high-damage kills; repeated attempts do not force a long introduction.",
         ["Assets/Scripts/Dungeon/BossRoomController.cs", "Docs/Art/CombatMovementPlan/Step09_LethalHits.md"]),
    item("J09", "Мир локально отвечает на присутствие", "Selective world reactions", 3, 3, 4, "3–5", "later",
         "Свет, parallax, туман, частицы окружения и поверхности уже развивались в предыдущем плане. Степень реактивности отдельных декораций в игре не проверена.",
         "Попробовать один-два объекта: трава отклоняется при проходе, подвесная деталь качнулась от близкого удара. Только визуальные реакции рядом с игроком; сначала без разрушения и выпадения лута.",
         "Проход через небольшой куст оставляет короткое движение веток. Каменная кладка остаётся неподвижной — реагирует подходящий объект, а не вся сцена.",
         "Нужны правила размещения, сброс состояния, дистанция обновления и проверка каждого нового набора декора. Разрушаемость резко увеличивает цену.",
         "One biome, one or two visual-only prop families. Verify that an existing prop is not already reactive before adding behavior. Reactions never change collision, traversal, rewards or targeting; no physics simulation on every decoration. Author distance limits and disable/reset behavior. Expand only after an A/B clip shows meaningful benefit during ordinary movement.",
         "A nearby reaction is perceptible without distracting from enemies. Reentry and reused objects restore correctly; crowded rooms do not update every offscreen prop. Stop the pilot if the benefit is mainly visible in a close-up demonstration.",
         ["Docs/Art/VisualAtmospherePlan/PLAN.md", "Docs/Art/Environment_Animations.md", "Docs/Art/Ambient_Particles.md"]),
    item("J10", "Конец попытки сохраняет чувство прогресса", "Run ending and return", 4, 2, 2, "2–4", "second",
         "DeathScreenOverlay уже показывает достигнутый этап и убийства, имеет fade и шестисекундную фазу сообщения. Новый экран статистики с нуля не нужен.",
         "Проверить темп повторных смертей и возможность быстро продолжить после первого знакомства. Показывать только реальный сохранённый результат; возвращение в хаб должно быть понятным и отзывчивым.",
         "После смерти видишь известный итог, можешь его прочесть или быстрее вернуться к игре. Если проект действительно сохраняет конкретное продвижение, оно ясно отделено от потерянного за попытку.",
         "Невысокая цена при использовании уже доступных данных. Причина смерти, история урона и полная аналитика забега — отдельные дорогие системы.",
         "Extend the current death overlay and return flow only where a normal repeated-attempt test exposes friction. Add a safe user-paced continuation/skip if desired, retaining the existing covered-frame transition and exactly-once callback. Show only verified existing persisted progress. Do not invent killer attribution, currency retention, unlocks or full-run telemetry.",
         "First and repeated deaths are readable and responsive. Early/repeated continue input cannot run the transition twice or reveal an unloaded room. Statistics match the existing run state, and EN/RU layout fits the logical canvas.",
         ["Assets/UI/DeathScreen/DeathScreenOverlay.cs"]),
]

# Stable IDs remain recorded, but these are no longer agent workstreams.
EXCLUDED = [dict(id=i['id'], name=i['name'], status="user-owned; excluded from agent roadmap",
                 reason="User will handle this independently; do not schedule or implement.")
            for i in ITEMS if i['id'] in {'J07', 'J08'}]
ITEMS = [i for i in ITEMS if i['id'] not in {'J07', 'J08'}]

LOOT_VARIANTS = [
    ("Приём добычи · начать здесь", "Короткое сжатие/раскрытие только визуала бокса и стягивание света внутрь. Частые прилёты объединяются, силуэт быстро успокаивается. Поддержка низкая.",
     "Start here: brief visual-only intake compression/opening and inward light. Merge frequent arrivals; do not move the collider or interaction target."),
    ("Ценная находка · второй акцент", "Один заметный импульс на приём предмета выбранной редкости. Короткая форма/символ дополняет цвет; сигнал не обещает, что вещь сильнее надетой. Поддержка низкая–средняя.",
     "Second accent: one rarity-qualified arrival pulse with shape as well as color. Rate-limit it; rarity is not an upgrade guarantee. Hidden and player-discarded items never trigger the rare-drop celebration."),
    ("Видимое накопление · опционально", "Два-три состояния внутреннего свечения или рисунка: добычи стало больше. Без раздувания бокса, шкалы вместимости и постоянного усиления яркости. Состояние учитывает только видимое содержимое и уменьшается после изъятия. Поддержка средняя.",
     "Optional: two or three bounded accumulation states driven by filter-visible contents, recomputed after removal/filter changes. No implied capacity, endless brightness growth or changes to interaction size."),
    ("Перелёт собирается в поток · опционально", "Доработать существующую дугу и затухание следа перед приёмом. При массовом луте несколько визуальных потоков могут представлять всю пачку, а содержимое бокса остаётся точным. Без новых иконок по всей арене. Поддержка средняя.",
     "Optional: tune the existing flight arc and trail convergence. Cap decorative streams during bursts; representation may be grouped while every actual item remains stored exactly once. Avoid a new offscreen VFX framework."),
]

SOURCES = [
    dict(title="Jeremiah Reid — Juice Your Turns", url="https://juiceyourturns.com/", status="Read the author's public slide text, not the linked full video.", takeaway="Small changes in motion, scale and color can improve interaction; presentation should not block actions."),
    dict(title="Chris Bourassa — A Torch in the Dark", url="https://media.gdcvault.com/gdc2016/Presentations/Bourassa_Chris_a%20torch%20in.pdf", status="Read the primary 19-page slide deck; not the full talk.", takeaway="A coherent creative direction helps a small team choose valuable features and cut expensive distractions. This is a general principle, not evidence for specific Relic Keeper gains."),
]

BASELINE = [
    "Atmosphere work is accepted as completed by the user. The atmosphere plan records specific implemented pilots and skipped options; this does not imply every biome has identical coverage.",
    "CombatMovementPlan/STATUS.md dated 2026-10-29 reports delivery of body-follow, enemy recoil, hit highlight, hit-stop, camera, hurt/defense/death and movement/input work. It reports 369 selected regression tests passed; those are historical results, not tests run for this new plan.",
    "Generic contact bursts (C05) were removed by user decision. Do not reintroduce them as a prerequisite. Audio remains deferred (C08); target-material variants remain optional (C06); authored attack-body key poses remain excluded (C03).",
    "Correction from user and verified delivery path: room loot is stored in one container by the next-room portal via WorldItemDropService -> RoomLootCollector.TryStore -> WorldLootCache. Storage precedes the existing cosmetic flight. Ground-drop hover/rarity visuals are a fallback and must not be treated as the normal mob-loot loop. Experience/currency pickups are separate existing systems.",
    "J01 preserves central collection and convenience. Improve intake, arrival and bounded accumulation cues; do not scatter loot, require individual ground pickup or make container access wait for effects. J07 encounter rhythm and J08 boss/elite staging are user-owned and removed from the agent roadmap.",
    "Crafting already refreshes UI and opens the crafted-item tooltip. Level-up already flashes the body, raises an arrow and shows a banner. HUD health/mana have increase/decrease presentation. These must not be sold as missing systems.",
    "Enemy attack warnings, status presentation and defense feedback exist. Quality/readability at normal speed is not proven by class names or source inspection.",
    "Room portal unlock and full clear are separate: RequiredPortalKills is max(0, (enemyCount + 1) / 2). Existing banner methods distinguish these outcomes. Reward modifiers decide what actually appears; not every room grants a chest.",
    "Death overlay already shows highest stage and kill count. Its message interval is six seconds in inspected source, followed by a covered-frame return sequence. No killer-attribution or retained-reward system is assumed.",
]

def write_outputs():
    paths = sorted({p for i in ITEMS for p in i["hooks"]} | {"Docs/Art/CombatMovementPlan/STATUS.md", "Docs/Art/CombatMovementPlan/Step16_ReducedEffects.md"})
    assert len(ITEMS) == 8 and len({i['id'] for i in ITEMS}) == 8
    assert {i['id'] for i in EXCLUDED} == {'J07', 'J08'}
    assert all((ROOT / p).is_file() for p in paths), [p for p in paths if not (ROOT / p).is_file()]
    hashes = {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in paths}
    plan = dict(date=DATE, revision=2, status="proposal; no implementation selected", baseline=BASELINE, items=ITEMS, excluded=EXCLUDED,
                lootVariants=[dict(title=v[0], description=v[1], scope=v[2]) for v in LOOT_VARIANTS], sources=SOURCES, sourceHashes=hashes)
    (OUT / 'plan.json').write_text(json.dumps(plan, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    md = """# Relic Keeper — whole-game juice roadmap

Date: 2026-10-29. Revision 2: corrected centralized loot; J07/J08 user-owned.
Status: **planning proposal, no gameplay implementation**.
Review UI is Russian; this handoff is English. Scope: extend the feel of the whole
run after the atmosphere and combat/movement passes, without reopening completed
work or assuming that every possible polish feature is worth its upkeep.

## Direction

Working direction for discussion: a grounded, dangerous world; precise player
control; rewards and meaningful power changes that are easy to perceive.
Extend feedback across action -> consequence -> reward -> build change -> next
challenge. This is a design hypothesis, not a new confirmed visual-style mandate.

The highest-priority candidate slice is **J01 + J02 + J03 + J06**, implemented
incrementally. It covers rewards, transactions, skill states and room resolution.
After each slice, compare a normal run. A currently clear interaction may need no
new effect. Do not commit to all eight tracks or sum their prototype estimates.

J07 encounter rhythm and J08 elite/boss staging are excluded from this roadmap:
the user will handle them independently. Keep their IDs reserved; they are not
later implementation phases or prerequisites for the remaining proposals.

## Evidence and boundaries

""" + '\n'.join('- ' + b for b in BASELINE)
    md += """

Evidence is a focused Graphify discovery followed by source reads and existing
delivery reports. No new live gameplay recording, Unity compilation, runtime test
or hardware profile was performed for this document. Existing reports are evidence
of their stated scope only. Old unchecked production-roadmap boxes are not proof
that a feature is still absent. Source hashes in plan.json pin the inspected files.

## Cost model

Gain, setup and upkeep use subjective 1–5 scales (higher = more). Prototype days
are rough person-days for a narrow representative slice with existing art and
familiar project code, including basic verification; production coverage and new
art can cost substantially more. They are not measured results or commitments.
Upkeep means extra work for each new item, skill, enemy, biome or UI state.

## Order and decision gates

1. Record 10–15 minutes of an ordinary run: combat, loot, crafting/equipment,
   cooldown/failed input, exit/clear, then death/return. Identify unclear events;
   do not use another combat-only stress clip as the entire baseline.
2. Prototype J01/J02/J03/J06 separately. J06 is self-contained; J01's cache intake
   can coordinate with it, but neither must wait for a global presentation service.
3. If gaps remain in cause/effect and enemy readability, try J04/J05; audit J10
   during repeated attempts. Room composition and boss staging remain user-owned.
4. Only then consider one J09 reactive-prop pilot. Expand only if the
   effect remains noticeable in ordinary gameplay and justifies content upkeep.

## Candidate workstreams
"""
    for i in ITEMS:
        md += f"\n### {i['id']} — {i['name']}\n\n"
        md += f"Stage: {i['stage']}. Expected gain {i['gain']}/5; setup {i['setup']}/5; upkeep {i['upkeep']}/5. Narrow prototype: {i['prototypeDays']} person-days. Boundary: {i['boundary']}.\n\n"
        md += i['scope'] + '\n'
        if i['id'] == 'J01':
            md += '\nCandidate treatments (not a mandatory bundle):\n\n' + '\n'.join('- ' + v[2] for v in LOOT_VARIANTS) + '\n'
        md += '\nAcceptance: ' + i['acceptance'] + '\n\nSource touchpoints (inspect callers and actual serialized wiring before implementation):\n\n'
        md += '\n'.join(f"- [{p}](../../../{p})" for p in i['hooks']) + '\n'
    md += """
## Presentation rules and evaluation

- Order competing feedback: imminent danger > player defensive state > important
  confirmed action/resource change > rare reward > routine reward > ambient motion.
  Do not hide any essential warning when decoration is denied.
- Match amplitude to significance and frequency. Routine actions remain quiet;
  rare transitions may carry more weight. Never repeat a ready cue each frame or
  celebrate every DoT tick as a weapon hit.
- Keep input and authoritative outcomes immediate. Animations confirm results;
  they do not hold items, mana, portal access or save writes hostage. Preserve
  current body/weapon separation, collision and input rules.
- Use shared profiles per semantic event, with local ownership/reset. Extend
  existing paths before introducing a generic event bus, tween framework or
  dependency. No global presentation-manager rewrite is a prerequisite.
- Respect 480x270/24 PPU, pixel stability, tooltip readability and EN/RU. Keep UI
  outside world color/camera effects. Avoid whole-window elastic motion.
- Existing Reduced combat effects is not a universal graphics setting. Audit
  each new path explicitly; routine semantic cues still work when decorative
  motion is reduced. Do not claim existing settings automatically cover new FX.
- Validate repeated events, full inventory, failed/rejected actions, weapon swaps,
  overlapping clear/level-up, pause, room transition and pooled reuse where relevant.
- Use identical route/build/events for A/B; show normal-speed clips and test muted
  as audio is outside scope. Ask what happened and why before explaining it.
  Keep an effect only if it improves recognition, satisfaction or flow without
  more missed warnings or longer compulsory waiting. These are evaluation criteria,
  not a claim that any proposed track has already passed.
- Runtime implementation requires the narrow meaningful EditMode checks and live
  Play Mode visual review under project instructions. This document changes none
  of the runtime, saves, assets or balance data.

## Deliberately deferred

Audio; target-material catalogs; authored normal-attack body clips; generic impact
bursts; full destructibility; full stat/proc telemetry; mandatory introductions;
automatic camera zoom/slow motion; global stagger/lunge/input-rule redesign.
These are scope decisions, not claims that such features could never be useful.

## Primary references

The specific priorities, scores and implementations above are project-specific
design judgments; the references inform general principles only.
"""
    for s in SOURCES:
        md += f"\n- [{s['title']}]({s['url']}). {s['status']} {s['takeaway']}\n"
    (OUT / 'PLAN.md').write_text(md, encoding='utf-8')
    build_html()

def build_html():
    cards = ''
    stage_labels = dict(first='Первый набор', second='Следующая волна', later='Выборочный пилот')
    for i in ITEMS:
        cards += f'''<article id="{i['id']}" data-stage="{i['stage']}">
          <div class="meta"><b>{i['id']}</b><span>{stage_labels[i['stage']]}</span></div>
          <h3>{h(i['title'])}</h3>
          <div class="scores"><span>Эффект <b>{i['gain']}/5</b></span><span>Сделать <b>{i['setup']}/5</b></span><span>Поддерживать <b>{i['upkeep']}/5</b></span></div>
          <p class="cost">Узкий прототип: {i['prototypeDays']} чел.-дн.</p>
          <p>{h(i['idea'])}</p><blockquote>{h(i['example'])}</blockquote>
          {('<ul class="treatments">' + ''.join('<li><b>' + h(v[0]) + '.</b> ' + h(v[1]) + '</li>' for v in LOOT_VARIANTS) + '</ul>') if i['id']=='J01' else ''}
          <details><summary>Что уже есть и сколько поддерживать</summary><p><b>Основа.</b> {h(i['baseline'])}</p><p><b>Поддержка.</b> {h(i['maintenance'])}</p></details>
        </article>'''
    source_links = ' · '.join(f'<a href="{h(s["url"])}">{h(s["title"])}</a>' for s in SOURCES)
    page = '''<!doctype html><html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Relic Keeper · Сочность всей игры</title><style>
:root{color-scheme:dark;--bg:#141b20;--panel:#202b32;--ink:#edf0e8;--muted:#b8c8cc;--line:#3b5059;--gold:#edc77c;--mint:#8cd2be}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:16px/1.6 system-ui,sans-serif}main{max-width:1200px;margin:auto;padding:54px 30px 70px}a{color:var(--mint);text-underline-offset:4px}h1,h2,h3{line-height:1.2}h1{font-size:clamp(32px,5vw,57px);max-width:850px;margin:14px 0 22px}h2{font-size:29px;margin:50px 0 20px}h3{font-size:23px;margin:18px 0}.eyebrow,.meta{color:var(--gold);font-size:13px;letter-spacing:.06em}.lead{font-size:20px;max-width:900px;color:var(--muted)}.note{border-left:3px solid var(--gold);background:var(--panel);padding:18px 23px;margin:28px 0}.chain{display:grid;grid-template-columns:repeat(5,1fr);gap:8px;margin:30px 0}.chain div{padding:18px 13px;border:1px solid var(--line);border-top:3px solid var(--mint)}.chain small{display:block;color:var(--muted);margin-top:8px}.steps{display:grid;grid-template-columns:repeat(4,1fr);gap:14px}.step{background:var(--panel);padding:20px;border:1px solid var(--line)}.step b{color:var(--gold)}.step p{margin-bottom:0}.filters{display:flex;gap:10px;flex-wrap:wrap;margin:24px 0}button{font:inherit;cursor:pointer;color:var(--ink);background:var(--panel);border:1px solid var(--line);border-radius:5px;padding:8px 15px}button[aria-pressed=true]{background:var(--gold);color:#1a2225;border-color:var(--gold)}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}article{background:var(--panel);padding:26px;border:1px solid var(--line);scroll-margin-top:20px}article[hidden]{display:none}.meta{display:flex;justify-content:space-between;gap:12px}.meta span{color:var(--muted);letter-spacing:0}.scores{display:flex;flex-wrap:wrap;gap:6px;font-size:13px}.scores span{border:1px solid var(--line);padding:5px 8px}.scores b{color:var(--gold)}.cost{color:var(--muted);font-size:13px}blockquote{margin:20px 0;padding:0 0 0 16px;border-left:2px solid var(--mint);color:var(--muted)}details{border-top:1px solid var(--line);padding-top:15px;font-size:14px}summary{color:var(--mint);cursor:pointer}.warning{color:var(--gold);font-size:14px}.rules{max-width:940px}.rules li{margin:12px 0}footer{color:var(--muted);border-top:1px solid var(--line);margin-top:45px;padding-top:20px;font-size:13px}nav{display:flex;gap:20px;flex-wrap:wrap}#count{color:var(--muted);font-size:14px}@media(max-width:850px){.steps{grid-template-columns:repeat(2,1fr)}.chain{grid-template-columns:1fr}.chain div{padding:12px 16px}.chain small{display:inline;margin-left:10px}}@media(max-width:650px){main{padding:28px 16px 45px}.grid,.steps{grid-template-columns:1fr}article{padding:20px}.meta{flex-wrap:wrap}}
#J01{grid-column:1/-1}.treatments{list-style:none;padding:0;display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.treatments li{padding:16px;border:1px solid var(--line);font-size:14px}.treatments b{color:var(--gold)}@media(max-width:650px){.treatments{grid-template-columns:1fr}}
</style></head><body><main>
<div class="eyebrow">RELIC KEEPER · ОБЩИЙ ПЛАН · V2 · 29 ОКТЯБРЯ 2026</div>
<h1>Сочность — на протяжении всего забега</h1>
<p class="lead">Бой и атмосфера уже получили основу. Следующий шаг — чтобы игра убедительно отвечала на получение награды, усиление билда, завершение комнаты и переход к следующей цели.</p>
<nav><a href="PLAN.md">English handoff</a><a href="plan.json">Данные плана</a><a href="../CombatMovementPlan/STATUS.md">Что уже сделано в боёвке</a></nav>
<div class="note"><b>Рабочее направление:</b> ощутимый мир, точное управление и заметная ценность награды. Новые эффекты остаются только там, где помогают почувствовать событие. Все пункты ниже — предложения для выбора; в игру этот документ ничего не добавляет.</div>
<div class="chain"><div>Действие<small>Ввод принят</small></div><div>Последствие<small>Понятно, что случилось</small></div><div>Награда<small>Хочется забрать</small></div><div>Усиление<small>Видно, что изменилось</small></div><div>Новая цель<small>Хочется идти дальше</small></div></div>
<h2>В каком порядке двигаться</h2>
<div class="steps"><div class="step"><b>01 · Обычный забег</b><p>10–15 минут записи: бой, лут, крафт, выход, смерть. Найти реальные провалы отклика.</p></div><div class="step"><b>02 · Первый набор</b><p><a href="#J01">J01</a> бокс добычи, <a href="#J02">J02</a> улучшения, <a href="#J03">J03</a> состояния умений, <a href="#J06">J06</a> завершение комнаты.</p></div><div class="step"><b>03 · Читаемость</b><p>Работа билда, намерения врагов и темп повторных попыток. Состав комнат и боссы остаются за тобой.</p></div><div class="step"><b>04 · Выборочный пилот</b><p>Реактивный декор в одной локации. Расширять только после заметного результата в обычной игре.</p></div></div>
<div class="note"><b>Уточнение объёма.</b> J01 развивает существующий перелёт добычи в единый бокс у портала. J07 (ритм комнат) и J08 (элита/боссы) убраны из предлагаемого объёма: ими ты займёшься самостоятельно. Нумерация остальных пунктов сохранена.</div>
<h2>8 направлений для выбора</h2>
<p>Оценки субъективные: эффект, сложность создания и поддержка — от 1 до 5; выше означает больше. Дни — ориентир узкого прототипа на существующем арте, а не срок внедрения во всю игру. Складывать их в общий дедлайн нельзя.</p>
<div class="filters" aria-label="Фильтр направлений"><button data-filter="all" aria-pressed="true">Все</button><button data-filter="first" aria-pressed="false">Первый набор</button><button data-filter="second" aria-pressed="false">Следующая волна</button><button data-filter="later" aria-pressed="false">Выборочные пилоты</button></div><p id="count" aria-live="polite">Показано: 8</p>
<div class="grid">CARDS</div>
<h2>Что сохраняем из прошлых решений</h2><ul class="rules"><li>Оружие и body-follow остаются разделены; уже сделанные реакции попадания, защиты, смерти и движения не предлагаются заново.</li><li>Общий burst при попадании не возвращаем. Звук отложен. Каталог материалов целей и нарисованные позы обычных атак не входят в текущий путь.</li><li>Лут мобов хранится в одном боксе у портала; перелёт декоративный и не задерживает доступ к содержимому. Существующие опыт/валюта, level-up, cooldown и баннеры сохраняются.</li><li>Сильный отклик — для значимого редкого события. Постоянное мигание, вспышки на каждый тик и тряска окон быстро обесценивают акценты.</li><li>Декорация не задерживает получение вещи, доступ к порталу или управление. Опасность всегда читается раньше награды; цвет дополняется формой и ритмом.</li><li>Проверяем при 480×270, в EN/RU и с уменьшенными эффектами. Нынешний Reduced combat effects не охватывает автоматически все будущие эффекты интерфейса и лута.</li></ul>
<h2>Как понять, что стало лучше</h2><div class="note">Сравнивать один маршрут и один билд при обычной скорости. Без подсказки спросить: что произошло, почему и что изменилось? Оставлять улучшение, если событие понятнее или приятнее, а угрозы не теряются и обязательного ожидания не стало больше. Если существующий отклик уже работает, дополнительный эффект не нужен.</div>
<footer><p>Основа: отчёты проекта и выборочный просмотр исходников на 29.10.2026 через Graphify и прямое чтение. Новый Play Mode просмотр и замеры для этого плана не проводились. Визуальные выигрыши — дизайнерская гипотеза.</p><p>Общие принципы: SOURCES. Прочитан текст слайдов; полный просмотр выступлений не заявляется. Конкретные приоритеты и оценки — выводы для этого проекта.</p></footer>
</main><script>
const buttons=[...document.querySelectorAll('[data-filter]')];const cards=[...document.querySelectorAll('article')];
function filter(value){buttons.forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.filter===value)));cards.forEach(c=>c.hidden=value!=='all'&&c.dataset.stage!==value);document.getElementById('count').textContent='Показано: '+cards.filter(c=>!c.hidden).length;}
buttons.forEach(b=>b.addEventListener('click',()=>filter(b.dataset.filter)));
document.querySelectorAll('a[href^="#J"]').forEach(a=>a.addEventListener('click',()=>filter('all')));
</script></body></html>'''
    (OUT / 'game-juice-plan.html').write_text(page.replace('CARDS',cards).replace('SOURCES',source_links), encoding='utf-8')

if __name__ == '__main__':
    write_outputs()
    print(f'Built {len(ITEMS)} proposed workstreams, {len(EXCLUDED)} user-owned exclusions; no gameplay files written.')
