# Гайд: Первая комната, данж и плейтест

Краткая пошаговая инструкция после установки системы данжей.

---

## 1. Подготовка папок

```
Assets/Prefabs/Dungeon/     — комнаты
Assets/Resources/Dungeons/  — DungeonDataSO
```

---

## 2. Создание первой комнаты

### Шаг 1 — Префаб комнаты

1. **Create Empty** → назови `Room_Test`
2. **Add Component → RoomController**
   - Room Level = 1
3. **Create Empty** дочерний → `PlayerSpawnPoint`
   - Add Component → PlayerSpawnPoint
   - Поставь позицию (левый край комнаты)
4. **Визуал:** добавь Tilemap или Sprite для пола
5. **Create Empty** `Spawners/SpawnPoint_1`
   - Add Component → EnemySpawner
   - Enemy Entries: + Add, выбери EnemyDataSO (Dummy из Resources/Enemies)
   - Spawn Count = 2
   - Spawn On Room Enter = ✓
   - Enemy Prefab = перетащи `DummyEnemyPF` из Prefabs/Enemy/
6. **Create Empty** `Portal_Next`
   - Поставь справа от комнаты
   - Add Component → DungeonPortal
   - Portal Type = NextRoom
   - Interact Prompt = "Выйти"
   - Is Active = ✓
   - Add Component → Box Collider 2D: Is Trigger = ✓, Size (2, 3)
7. **Сохрани как префаб:** перетащи в `Assets/Prefabs/Dungeon/`

---

## 3. Создание данжа

1. **Префабы комнат должны быть в папке Resources** (иначе не загрузятся)
   - Например: `Assets/Resources/Prefabs/Dungeon/MineRoom_001.prefab`
2. **ПКМ в Project** → Create → RPG → Dungeons → Dungeon Data
3. Назови `Dungeon_01_Test`, сохрани в `Assets/Resources/Dungeons/`
4. Настрой:
   - ID: `dungeon_01`
   - Display Name: `Тест`
   - Min Level 1, Max Level 10
   - Room Count: 2
   - **Normal Room Prefab Paths**: добавь путь без .prefab, например `Prefabs/Dungeon/MineDungeon/MineRoom_001`
   - **Boss Room Prefab Path**: путь к босс-комнате
5. Кнопка «Найти префабы комнат в Resources» в Inspector покажет доступные пути

---

## 4. Настройка HubScene

### DungeonSystem

1. Открой HubScene
2. **Create Empty** `DungeonSystem`
3. Add Component → DungeonController
4. **Create Empty** дочерний `DungeonContainer`
5. **Create Empty** `HubSpawnPoint` — поставь в точке появления в городе
6. Собери под одним родителем `HubWorld` всё, что должно скрываться (земля, фон, здания)
7. В DungeonController задай:
   - Dungeon Container = DungeonContainer
   - Hub World = HubWorld
   - Hub Spawn Point = HubSpawnPoint
   - Player = Player

### Кнопка входа

1. Добавь кнопку «Войти в данж» на Canvas
2. Add Component → DungeonEntryButton
3. Dungeon Data = Dungeon_01_Test
4. On Click () → DungeonEntryButton.EnterDungeon

### PlayerInteractController

1. Выбери Player
2. Add Component → PlayerInteractController
3. Interact Radius = 2

---

## 5. Плейтест

### Чеклист

- [ ] DungeonController — все ссылки заполнены
- [ ] PlayerInteractController на Player
- [ ] DungeonDataSO с Room_Test
- [ ] Enemy Prefab в EnemySpawner = DummyEnemyPF
- [ ] Interact = E (настройки Input)

### Сценарий 1 — Вход

1. Play
2. Нажми «Войти в данж»
3. Hub скрылся, комната, игрок в точке, 2 врага

### Сценарий 2 — Следующая комната

1. Подойди к порталу
2. Нажми E
3. Новая комната, телепорт, враги

### Сценарий 3 — Возврат

1. В последней комнате портал «В город» (ReturnToHub)
2. Interact
3. Hub, игрок в HubSpawnPoint

---

## 6. Босс-комната

1. Дублируй Room_Test → Room_Boss
2. Замени портал на Portal_ToHub (Type = ReturnToHub, Prompt = "В город")
3. Добавь босса (DummyEnemy prefab)
4. Add Component → BossRoomController
   - Boss Entity = EnemyHealth босса
   - Portal To Hub = Portal_ToHub
5. Portal_ToHub: в префабе **изначально выключен** (GameObject inactive) или BossRoomController в Start вызовет SetActive(false)
6. DungeonDataSO → Boss Room Prefab = Room_Boss

---

## Частые проблемы

| Проблема | Решение |
|----------|---------|
| Interact не работает | Радиус, коллайдер портала (Is Trigger), PlayerInteractController на Player |
| Враги не спавнятся | Enemy Prefab, EnemyDataSO, Spawn On Room Enter |
| Hub не скрывается | Ссылка Hub World в DungeonController |
| NullReference | Заполнить все SerializeField в DungeonController |
