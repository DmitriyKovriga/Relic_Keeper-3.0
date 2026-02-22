# План системы данжей Relic Keeper

## Обзор

Данный документ описывает архитектуру и этапы реализации системы данжей с комнатами, спавнерами врагов и переходами Hub ↔ Dungeon.

---

## 1. Ключевые архитектурные решения

### 1.1 Одна сцена Hub — телепортация вместо смены сцен

**Рекомендация: оставаться в HubScene, менять только комнату.**

| Подход | Плюсы | Минусы |
|-------|-------|--------|
| **Одна сцена — смена комнат** | Нет передачи данных, все менеджеры и Player на месте, проще отладка, игра компактная | Hub + комната в памяти одновременно (но комната выгружается при смене) |
| Отдельная DungeonScene | Чистое разделение, экономия памяти | Сложная передача состояния, риски «потерять» сущности |

**Почему одна сцена:**
- Игра компактная — данж занимает мало места
- Не нужно передавать данные между сценами — всё уже в Hub
- Player, Camera, все менеджеры (GameSaveManager, CharacterPartyManager, InventoryManager, StashManager, PoolManager, FloatingTextManager, ItemGenerator, WindowManager, InputManager) остаются в сцене
- При смене комнаты: уничтожаем текущую комнату (prefab instance), инстанцируем следующую, телепортируем Player

---

### 1.2 Данные и сохранения — как это работает

**Сцена не меняется → данные никуда не передаём.**

Текущая система сохранения:
- `GameSaveManager` — в Start проверяет файл сейва, вызывает `LoadGame()` или `StartNewGame()`
- `LoadGame()` — читает JSON, вызывает `CharacterPartyManager.LoadFromSave()`, затем `LoadCharacterIntoGame()` для активного персонажа
- `LoadCharacterIntoGame()` — `PlayerStats.Initialize()` + `ApplyLoadedState()`, `InventoryManager.LoadState()`, `PassiveTreeManager.LoadState()`
- Сейв пишет в файл только по K, загрузка — при старте сцены

**При переходе Hub → Dungeon (смена комнаты в той же сцене):**
- Ничего не меняется. Player на месте, все менеджеры на месте, HP/Mana/Inventory — всё живёт в памяти
- Сохранение в файл по-прежнему по K (и при выходе, если добавим)

**При переходе Room → Room (внутри данжа):**
- Уничтожаем текущую комнату (враги, триггеры), инстанцируем следующую
- Телепортируем Player в `PlayerSpawnPoint` новой комнаты
- Состояние игрока не трогаем

**При возврате Dungeon → Hub:**
- Уничтожаем босс-комнату, включаем визуал Hub
- Телепортируем Player в точку спавна в городе
- Состояние уже актуально (XP, луты подобраны в данже)

---

### 1.3 Поток переходов (одна сцена)

```
Hub (сцена, всё загружено)
    │
    │ Игрок нажимает "Войти в данж"
    │ → DungeonController.EnterDungeon(dungeonId)
    │   — Скрыть Hub-мир (Layer_Ground, фон и т.д. или родитель HubWorld)
    │   — Shuffle комнат, инстанцировать Room 1 в DungeonContainer
    │   — Телепорт Player в Room 1.PlayerSpawnPoint
    │   — Вызвать EnemySpawner.Spawn() в комнате
    │
    │ Игрок идёт по комнатам 1..10
    │ → Подходит к порталу, нажимает Interact
    │ → DungeonController.OnPortalInteract(portal)
    │   — Destroy текущая комната
    │   — Инстанцировать следующую комнату
    │   — Телепорт Player в PlayerSpawnPoint
    │   — Spawn врагов
    │
    │ Комната 10 = босс
    │ → Портал «В город» неактивен до смерти босса
    │ → BossRoomController.OnBossDeath() → активировать портал «В город»
    │
    │ Игрок подходит к порталу «В город», Interact
    │ → DungeonController.ReturnToHub()
    │   — Destroy DungeonContainer (комната)
    │   — Показать Hub-мир
    │   — Телепорт Player в HubSpawnPoint
```

---

## 2. Структура данных

### 2.1 ScriptableObjects

```
DungeonDataSO
├── ID, DisplayName
├── MinLevel, MaxLevel (для первого данжа 1–10, второго 10–20 и т.д.)
├── RoomCount (например 10)
├── NormalRoomPrefabs[] — пул обычных комнат (10–30 префабов)
├── BossRoomPrefab — одна босс-комната
└── [позже] ScalingRules для 4-го данжа (бесконечный)
```

---

### 2.2 Уровень локации (комнаты)

**Уровень врагов задаётся комнатой, а не спавнером.**

- У каждой комнаты префаба есть поле **Room Level** (1–10 для первого данжа, 10–20 для второго и т.д.)
- `EnemySpawner` получает уровень через `RoomController` или родительский объект комнаты
- Монстры используют базовые статы из `EnemyDataSO` + скейл от уровня комнаты

```
RoomController (на корне префаба комнаты)
├── RoomLevel (int) — уровень локации (1..10, 10..20, …)
├── PlayerSpawnPoint (Transform)
├── EnemySpawners[] — собираются в Start или через GetComponentsInChildren
└── Портал(ы) — дочерние объекты с DungeonPortal
```

---

### 2.3 Компоненты

#### EnemySpawner (MonoBehaviour)

Вешается на пустой GameObject в комнате. **Уровень берёт из RoomController родителя.**

```
EnemySpawner
├── EnemyEntries[]: { EnemyDataSO, Weight? } — какие мобы могут спавниться
├── SpawnCount (int) — сколько заспавнить
├── SpawnOnRoomEnter (bool) — при входе в комнату или в Start
├── Уровень — НЕ настраивается здесь, берётся из RoomController.RoomLevel
└── Spawn(int level) — создаёт экземпляры, EnemyEntity.Setup(data, level)
```

#### PlayerSpawnPoint (MonoBehaviour)

Маркер Transform для размещения игрока при входе в комнату.

```
PlayerSpawnPoint
└── Transform — куда ставить игрока
```

#### DungeonPortal (MonoBehaviour) — портал с Interact

Универсальный спрайт портала. Переход **по нажатию Interact**, а не при входе в зону.

```
DungeonPortal
├── Collider2D (Trigger) — зона, в которой можно нажать Interact
├── PortalType: NextRoom | ReturnToHub
├── [опционально] SpriteRenderer — визуал портала
├── InteractPrompt (string) — «Выйти» / «В город» и т.д.
└── При нажатии Interact в зоне → DungeonController.OnPortalUsed(this)
```

**Механика Interact:**
- В проекте уже есть `GameInput.Player.Interact`
- Нужен `PlayerInteractController` — в Update проверяет, есть ли в радиусе объект с `IInteractable`
- При нажатии Interact — вызывает `IInteractable.Interact()`
- `DungeonPortal` реализует `IInteractable`

#### BossRoomController (MonoBehaviour)

```
BossRoomController
├── BossEntity (ссылка на босса)
├── PortalToHub (DungeonPortal) — изначально неактивен/недоступен
└── OnBossDeath() → активирует портал «В город»
```

---

### 2.4 Префабы

| Префаб | Содержит |
|--------|----------|
| **EnemySpawnerMarker** | Пустой объект + `EnemySpawner` (настраивается в инспекторе) |
| **Room_XXX** | `RoomController` (RoomLevel), tilemap, `PlayerSpawnPoint`, спавнеры, `DungeonPortal` |
| **BossRoom** | То же + босс, `BossRoomController`, портал «В город» |
| **Player** | См. раздел «Player как префаб» ниже |

---

## 3. Жизненный цикл комнаты

1. **DungeonController.EnterDungeon(dungeonId):**
   - Скрыть Hub-мир, показать DungeonContainer
   - Shuffle комнат (Fisher–Yates), последняя — босс
   - Инстанцировать Room 1 в DungeonContainer
   - Телепорт Player в `PlayerSpawnPoint`, вызвать `EnemySpawner.Spawn(roomLevel)` у всех спавнеров

2. **При нажатии Interact на портале «Следующая комната»:**
   - Destroy текущая комната
   - Инстанцировать следующую
   - Телепорт Player, Spawn врагов

3. **В босс-комнате:**
   - Портал «В город» неактивен до смерти босса
   - `BossRoomController.OnBossDeath()` → активирует портал

4. **При Interact на портале «В город»:**
   - Destroy DungeonContainer, показать Hub, телепорт в HubSpawnPoint

5. **Выбор комнат без повторов:**
   - Shuffle индексов `[0..N-1]`, берём первые 9 обычных + босс

---

## 4. Чеклист сущностей — что должно работать в данже

Поскольку используем **одну сцену**, все объекты Hub остаются в игре. При входе в данж меняется только содержимое DungeonContainer и видимость Hub-мира.

| Сущность | Где живёт | В данже |
|----------|-----------|---------|
| Player | Hub (GameObject в сцене) | Работает, телепортируется |
| Main Camera | Hub | Следует за Player |
| InputManager | Hub (или отдельный объект) | Работает (статический GameInput) |
| GameSaveManager | Hub | Работает, сейв по K |
| CharacterPartyManager | Hub | Работает |
| InventoryManager | Hub (UI) | Работает |
| StashManager | Hub | Работает |
| PoolManager | Hub | Работает (VFX, damage numbers) |
| FloatingTextManager | Hub | Работает |
| ItemGenerator | Hub | Работает |
| WindowManager | Hub | Работает |
| ItemTooltipController | Hub | Работает |

**Важно:** При добавлении новых синглтонов или менеджеров — они должны быть в Hub-сцене и не завязываться на объекты, которые существуют только в Hub (например, на конкретные NPC). Тогда они автоматически будут работать и в данже.

---

## 5. Player как префаб

**Сейчас:** Player — объект в сцене Hub, не префаб.

**Нужен ли префаб?** Для текущей схемы (одна сцена) — **не обязательно**. Player остаётся в сцене при телепортации. Префаб полезен, если:
- понадобится респавн после смерти;
- захочется дублировать настройки Player в тестовых сценах.

**Рекомендация:** Сделать префаб **опционально** на этапе стабилизации. На первом этапе можно оставить Player в сцене. Если позже решим перейти на отдельные сцены — тогда префаб станет нужен.

---

## 6. Этапы реализации (пошагово)

### Этап 1: Инструментарий

| # | Задача | Описание |
|---|--------|----------|
| 1.1 | `RoomController` | Компонент на корне комнаты: RoomLevel, сборка спавнеров |
| 1.2 | `EnemySpawner` | Спавн по EnemyDataSO, уровень из RoomController |
| 1.3 | `PlayerSpawnPoint` | Маркер Transform |
| 1.4 | `DungeonPortal` + `IInteractable` | Портал с Interact, не авто-зона |
| 1.5 | `PlayerInteractController` | Поиск IInteractable в радиусе, вызов при Interact |
| 1.6 | `DungeonDataSO` | ScriptableObject данжа |
| 1.7 | Editor-утилита | Меню для создания `DungeonDataSO` |

### Этап 2: Прототип комнаты в Hub

| # | Задача | Описание |
|---|--------|----------|
| 2.1 | Тестовая комната | Префаб с RoomController, спавнером, порталом |
| 2.2 | DungeonContainer + DungeonController | Контейнер для комнат, логика смены |
| 2.3 | Проверка | Вход в данж, смена комнаты по Interact, возврат |

### Этап 3: Полный цикл данжа

| # | Задача | Описание |
|---|--------|----------|
| 3.1 | Скрытие/показ Hub | При входе в данж — скрыть Hub-мир, при выходе — показать |
| 3.2 | Shuffle комнат | Выбор комнат без повторов |
| 3.3 | Интеграция босс-комнаты | BossRoomController, портал «В город» |

### Этап 4: UI и доработки

| # | Задача | Описание |
|---|--------|----------|
| 4.1 | Окно выбора данжа в Hub | 4 кнопки (3 рабочих + заглушка) |
| 4.2 | Подсказка Interact | «Нажми E для выхода» и т.п. |

### Этап 5: Инструкция для будущих изменений

| # | Задача | Описание |
|---|--------|----------|
| 5.1 | Документ `DUNGEON_EXTENSIBILITY.md` | Правила добавления компонентов, чтобы они работали в данже (см. раздел 8) |

---

## 7. Диаграмма зависимостей (одна сцена)

```
HubScene
├── Player, Camera, InputManager
├── GameSaveManager, CharacterPartyManager
├── InventoryManager, StashManager, PoolManager
├── FloatingTextManager, WindowManager, ...
│
├── HubWorld (Layer_Ground, фон, здания) — скрывается при входе в данж
├── HubSpawnPoint — куда телепортировать при возврате
│
├── DungeonController
│   ├── DungeonContainer — родитель для инстанцируемых комнат
│   ├── EnterDungeon(dungeonId) — скрыть Hub, загрузить Room 1
│   ├── OnPortalInteract(portal) — смена комнаты или возврат
│   └── ReturnToHub() — показать Hub, уничтожить комнату
│
├── PlayerInteractController — ищет IInteractable, вызывает при Interact
│
└── DungeonSelectUI → EnterDungeon(1..4)

Room (prefab instance, в DungeonContainer)
├── RoomController (RoomLevel)
├── PlayerSpawnPoint
├── EnemySpawner[] → Spawn(roomLevel)
└── DungeonPortal (IInteractable) → NextRoom | ReturnToHub
```

---

## 8. Инструкция для будущих изменений (Этап 5.1)

Чтобы новые компоненты и классы работали в данже, следуй правилам:

### Правило 1: Расположение менеджеров

Все синглтоны и менеджеры должны находиться **в HubScene** на объектах, которые не скрываются при входе в данж (т.е. не внутри HubWorld).

### Правило 2: Не привязывайся к Hub-специфичным объектам

Если менеджер через `FindObjectOfType` или `[SerializeField]` ищет только объекты Hub (NPC, лавки и т.д.) — в данже они не найдутся. Используй:
- интерфейсы (`IInteractable`, `IDamageable`),
- события,
- или проверку `if (obj != null)` перед использованием.

### Правило 3: Данные для комнат — в префабах

Данные уровня комнаты, спавнеров и порталов хранятся в **префабах комнат**. Не храни критичные данные в сцене Hub для объектов, которые будут уничтожены при смене комнаты.

### Правило 4: Чеклист при добавлении нового компонента

- [ ] Компонент живёт в Hub (не в комнате) или в префабе комнаты?
- [ ] Если в Hub — он не зависит от объектов, уничтожаемых при смене комнаты?
- [ ] Если в комнате — он получает RoomLevel через RoomController или родителя?
- [ ] Обновить `DUNGEON_SYSTEM_PLAN.md` или `DUNGEON_EXTENSIBILITY.md`, если добавляешь новый тип сущности.

Создать отдельный файл `DUNGEON_EXTENSIBILITY.md` с этим разделом и чеклистом сущностей — чтобы при добавлении новых систем не забывать об этих правилах.

---

## 9. Следующий шаг

Начать с **Этапа 1**: `RoomController`, `EnemySpawner`, `PlayerSpawnPoint`, `DungeonPortal`, `IInteractable`, `PlayerInteractController`, `DungeonDataSO`.
