# Детальный план реализации системы данжей

Пошаговый план с учётом всех компонентов.

**Гайд для первой комнаты и плейтеста:** см. `FIRST_ROOM_AND_DUNGEON_GUIDE.md`

---

## Этап 1: Базовые компоненты

### Шаг 1.1 — IInteractable и PlayerSpawnPoint

**Файлы:**
- `Assets/Scripts/Dungeon/IInteractable.cs` (новый)
- `Assets/Scripts/Dungeon/PlayerSpawnPoint.cs` (новый)

**IInteractable.cs:**
```csharp
public interface IInteractable
{
    string GetPrompt();       // "Выйти" / "В город"
    void Interact();          // Вызывается при нажатии Interact
    bool CanInteract();       // Доступен ли (для портала после босса)
}
```

**PlayerSpawnPoint.cs:**
- Пустой `MonoBehaviour` — маркер.
- Опционально: `[ExecuteInEditMode]` для визуализации в редакторе (Gizmos — иконка или сфера).

---

### Шаг 1.2 — EnemySpawnerEntry (структура для настройки)

**Файл:** `Assets/Scripts/Dungeon/EnemySpawnerEntry.cs` (новый)

```csharp
[System.Serializable]
public class EnemySpawnerEntry
{
    public EnemyDataSO EnemyData;
    [Min(1)] public int Weight = 1;  // Для взвешенного случайного выбора
}
```

---

### Шаг 1.3 — EnemySpawner

**Файл:** `Assets/Scripts/Dungeon/EnemySpawner.cs` (новый)

**Поля:**
- `List<EnemySpawnerEntry> EnemyEntries`
- `int SpawnCount` (сколько врагов)
- `bool SpawnOnRoomEnter` — если true, не спавнит в Awake/Start, ждёт вызова `Spawn()`
- `EnemyEntity EnemyPrefab` — префаб врага (универсальный с EnemyEntity.Setup)

**Методы:**
- `public void Spawn(int level)` — выбирает EnemyDataSO по весам, инстанцирует prefab, вызывает `Setup(data, level)`
- Родитель для spawned — `transform` (спавнер) или специальный контейнер в комнате

**Важно:** Нужен универсальный префаб врага. Можно использовать `DummyEnemy` или создать `GenericEnemyPF` — префаб с `EnemyEntity` (с `_defaultData = null`), `EnemyStats`, `EnemyHealth`. Спавнер всегда вызывает `Setup(data, level)`.

**Путь к префабу:** добавить в ProjectPaths или `[SerializeField]` в спавнере.

---

### Шаг 1.4 — RoomController

**Файл:** `Assets/Scripts/Dungeon/RoomController.cs` (новый)

**Поля:**
- `int RoomLevel` (1–10, 10–20 и т.д.)
- `PlayerSpawnPoint PlayerSpawnPoint` (или Transform)
- Кэш: `EnemySpawner[]`, `DungeonPortal[]` — собираются в `Awake` через `GetComponentsInChildren`

**Методы:**
- `public void OnRoomEntered(Transform playerTransform)` — телепорт игрока в PlayerSpawnPoint, вызов `EnemySpawner.Spawn(RoomLevel)` у всех спавнеров с `SpawnOnRoomEnter`

**Зависимости:** EnemySpawner, PlayerSpawnPoint, DungeonPortal (пока без логики портала)

---

### Шаг 1.5 — DungeonPortal и IInteractable

**Файл:** `Assets/Scripts/Dungeon/DungeonPortal.cs` (новый)

**Поля:**
- `PortalType Type` (enum: NextRoom, ReturnToHub)
- `string InteractPrompt`
- `bool IsActive` — для портала «В город», который включается после босса
- `Collider2D` (Trigger) — для проверки «игрок в зоне»

**Реализация IInteractable:**
- `GetPrompt()` → InteractPrompt
- `CanInteract()` → IsActive
- `Interact()` → вызов события/статического события `OnPortalInteracted(DungeonPortal)` или обращение к DungeonController

**Связь с DungeonController:** через статическое событие `DungeonController.OnPortalUsed` или `FindObjectOfType<DungeonController>()` в момент Interact.

---

### Шаг 1.6 — PlayerInteractController

**Файл:** `Assets/Scripts/Dungeon/PlayerInteractController.cs` (новый)

**Привязка:** вешается на Player (рядом с PlayerMovement).

**Логика:**
- В `Update`: ищем `IInteractable` в радиусе (например `Physics2D.OverlapCircle` или `OverlapBox` от позиции игрока, потом `GetComponent<IInteractable>()`).
- Радиус: 1.5–2 units.
- При нажатии `Interact.performed` — если есть IInteractable и `CanInteract()` — вызываем `Interact()`.
- Опционально: показываем подсказку (UI текст «Нажми E — Выйти»).

**Input:** `InputManager.InputActions.Player.Interact.performed`

---

### Шаг 1.7 — DungeonDataSO

**Файл:** `Assets/Scripts/Dungeon/DungeonDataSO.cs` (новый)

**Создать:** `[CreateAssetMenu(menuName = "RPG/Dungeons/Dungeon Data")]`

**Поля:**
- `string ID`
- `string DisplayName`
- `int MinLevel`, `int MaxLevel`
- `int RoomCount` (например 10)
- `GameObject[] NormalRoomPrefabs`
- `GameObject BossRoomPrefab`

---

### Шаг 1.8 — Универсальный префаб врага

**Задача:** Создать префаб для спавнера.

**Варианты:**
1. Использовать существующий `DummyEnemyPF` — он имеет `_defaultData`. Спавнер будет вызывать `Setup(otherData, level)` и перезаписывать.
2. Создать `GenericEnemyPF` — копия Dummy, но `_defaultData = null` — только Setup из кода.

**Действие:** Создать `Assets/Prefabs/Enemy/GenericEnemyPF.prefab` на основе DummyEnemy. Добавить путь в ProjectPaths: `ResourcesEnemyGeneric` или передавать префаб в EnemySpawner через SerializeField.

---

## Этап 2: DungeonController и DungeonContainer

### Шаг 2.1 — DungeonController

**Файл:** `Assets/Scripts/Dungeon/DungeonController.cs` (новый)

**Поля:**
- `DungeonDataSO CurrentDungeon`
- `Transform DungeonContainer` — родитель для комнат
- `Transform HubWorld` — объект(ы) Hub для скрытия/показа (или отдельные списки)
- `Transform HubSpawnPoint`
- `Transform PlayerTransform`
- `List<GameObject> _roomSequence` — заготовленная последовательность комнат
- `int _currentRoomIndex`
- `GameObject _currentRoomInstance`

**Методы:**
- `public void EnterDungeon(DungeonDataSO dungeon)` — скрыть Hub, shuffle комнат, LoadRoom(0)
- `public void LoadRoom(int index)` — Destroy текущая, Instantiate _roomSequence[index], телепорт, RoomController.OnRoomEntered
- `public void OnPortalUsed(DungeonPortal portal)` — подписан на событие от DungeonPortal; если NextRoom → LoadRoom(index+1), если ReturnToHub → ReturnToHub()
- `public void ReturnToHub()` — Destroy комнату, показать Hub, телепорт в HubSpawnPoint

**Событие:** `DungeonPortal` при Interact вызывает `DungeonController.Instance.OnPortalUsed(this)` или через статическое событие.

---

### Шаг 2.2 — Fisher-Yates Shuffle

**Где:** В DungeonController или отдельный статический класс `ListExtensions`.

```csharp
public static void Shuffle<T>(this IList<T> list)
{
    for (int i = list.Count - 1; i > 0; i--)
    {
        int j = Random.Range(0, i + 1);
        (list[i], list[j]) = (list[j], list[i]);
    }
}
```

**Использование:** Создать список индексов [0..N-1] для NormalRoomPrefabs, Shuffle, взять первые (RoomCount-1), добавить BossRoomPrefab.

---

### Шаг 2.3 — Интеграция в HubScene

**В сцене Hub:**
1. Создать пустой объект `DungeonSystem`:
   - DungeonController (скрипт)
   - Дочерний объект `DungeonContainer` (пустой, в начале скрыт или пуст)
2. Объект `HubWorld` — родитель для Layer_Ground, Layer_Background, Layer_Foreground (всё, что скрываем при входе в данж). Если структура сцены не позволяет — использовать `SetActive` на каждом слое.
3. `HubSpawnPoint` — пустой объект с Transform (позиция возврата в город).
4. В DungeonController задать ссылки: HubWorld, DungeonContainer, HubSpawnPoint, Player.

---

### Шаг 2.4 — Скрытие/показ Hub

**Реализация:**
- `HubWorld.SetActive(false)` при EnterDungeon
- `HubWorld.SetActive(true)` при ReturnToHub
- `DungeonContainer.SetActive(true)` при EnterDungeon (если был скрыт)

**Альтернатива:** Если HubWorld — не один объект, а несколько (Layer_Ground, Layer_Back, Layer_Fore), добавить в DungeonController массив `GameObject[] HubLayers` и переключать их.

---

## Этап 3: Тестовая комната и портал

### Шаг 3.1 — Префаб тестовой комнаты

**Создать:** `Assets/Prefabs/Dungeon/Room_Test.prefab`

**Структура:**
```
Room_Test (root)
├── RoomController (RoomLevel = 1)
├── PlayerSpawnPoint (пустой объект с компонентом)
├── Floor/Visuals (Tilemap или спрайт — платформа)
├── Spawners (пустой родитель)
│   └── SpawnPoint_1 (EnemySpawner)
└── Portal_Next (DungeonPortal, Collider2D trigger)
```

**EnemySpawner:** 1 запись — DummyEnemy или существующий EnemyDataSO, SpawnCount = 2, SpawnOnRoomEnter = true.

**DungeonPortal:** PortalType = NextRoom, InteractPrompt = "Выйти", IsActive = true, Collider2D (BoxCollider2D, Is Trigger).

---

### Шаг 3.2 — BossRoomController и портал «В город»

**Файл:** `Assets/Scripts/Dungeon/BossRoomController.cs` (новый)

**Поля:**
- `EnemyHealth BossEntity` (или EnemyEntity)
- `DungeonPortal PortalToHub`

**Логика:** В Start подписаться на `BossEntity.OnDeath`. В обработчике — `PortalToHub.IsActive = true` (нужен сеттер в DungeonPortal) или `PortalToHub.gameObject.SetActive(true)`.

**Добавить в DungeonPortal:** `public void SetActive(bool value)` для программного включения.

---

### Шаг 3.3 — Тестовый данж DungeonDataSO

**Создать:** `Assets/Resources/Dungeons/Dungeon_01_Test.asset`

- ID: "dungeon_01"
- DisplayName: "Тестовый данж"
- MinLevel 1, MaxLevel 10
- RoomCount: 2 (для теста — одна обычная + босс)
- NormalRoomPrefabs: [Room_Test]
- BossRoomPrefab: пока заглушка или копия Room_Test с BossRoomController

---

### Шаг 3.4 — DungeonSelectUI (минимальный)

**Для теста:** Кнопка в сцене Hub, по нажатию — `FindObjectOfType<DungeonController>().EnterDungeon(dungeonData)`.

Можно временно: на отдельном Canvas кнопка «Войти в данж», SerializeField DungeonDataSO, onClick → EnterDungeon.

---

## Этап 4: Доработки и интеграция

### Шаг 4.1 — Порядок выполнения при входе в комнату

1. Instantiate room prefab под DungeonContainer
2. Получить RoomController из инстанса
3. Телепортировать Player в RoomController.PlayerSpawnPoint.position
4. Вызвать RoomController.OnRoomEntered(playerTransform) — внутри вызов Spawn у всех EnemySpawner

---

### Шаг 4.2 — Layer для врагов и триггеров

Убедиться, что:
- Враги на слое, с которым игрок может взаимодействовать (collision)
- Портал на Layer, который видит Player (для OverlapCircle/OverlapBox в PlayerInteractController)

Можно использовать слой Default или специальный `DungeonInteractables`.

---

### Шаг 4.3 — Камера и границы

При входе в данж комната может быть другой формы. Проверить:
- LevelBounds / Cinemachine — не конфликтуют ли с комнатой
- При необходимости обновлять границы камеры при смене комнаты (позже)

---

### Шаг 4.4 — DUNGEON_EXTENSIBILITY.md

Создать `Assets/Editor/DungeonSystem/DUNGEON_EXTENSIBILITY.md` с правилами из раздела 8 плана.

---

## Чеклист перед плейтестом

- [ ] IInteractable, PlayerSpawnPoint
- [ ] EnemySpawner, EnemySpawnerEntry
- [ ] RoomController
- [ ] DungeonPortal (IInteractable)
- [ ] PlayerInteractController (на Player)
- [ ] DungeonController, DungeonContainer
- [ ] DungeonDataSO, Editor CreateAssetMenu
- [ ] GenericEnemyPF или использование DummyEnemy
- [ ] HubWorld, HubSpawnPoint настроены в сцене
- [ ] Room_Test префаб
- [ ] Кнопка «Войти в данж» в Hub
- [ ] BossRoomController (если есть босс-комната)
- [ ] Путь к префабу врага в Resources или SerializeField

---

# Гайд: Создание первой комнаты и данжа + плейтест

## Часть 1: Подготовка

### 1.1 Структура папок

```
Assets/
├── Scripts/Dungeon/          — все скрипты данжей
├── Prefabs/
│   ├── Dungeon/              — комнаты, порталы
│   └── Enemy/                — GenericEnemyPF
├── Resources/
│   └── Dungeons/             — DungeonDataSO
```

---

## Часть 2: Создание первой комнаты

### Шаг 2.1 — Новая сцена для редактирования (опционально)

Создай временную сцену `DungeonRoomEditor` или редактируй префаб в режиме Prefab Edit.

### Шаг 2.2 — Создание префаба комнаты

1. **Создать пустой GameObject** `Room_MyFirst`.
2. **Добавить RoomController:**
   - Add Component → RoomController
   - Room Level = 1
3. **PlayerSpawnPoint:**
   - Create Empty дочерний объект, назвать `PlayerSpawnPoint`
   - Add Component → PlayerSpawnPoint
   - Поставь позицию, куда должен появиться игрок (например левый край комнаты)
4. **Визуал:**
   - Добавь Tilemap или Sprite для пола/стен
   - Сделай платформу, по которой игрок может ходить
5. **Enemy Spawner:**
   - Create Empty `Spawners/SpawnPoint_1`
   - Add Component → EnemySpawner
   - Enemy Entries: добавь один элемент, выбери EnemyDataSO (например Dummy из Resources)
   - Spawn Count = 2
   - Spawn On Room Enter = true
   - Enemy Prefab = перетащи GenericEnemyPF (или DummyEnemyPF)
6. **Портал:**
   - Create Empty `Portal_Next`
   - Поставь справа от комнаты (куда игрок пойдёт к выходу)
   - Add Component → DungeonPortal
   - Portal Type = NextRoom
   - Interact Prompt = "Выйти"
   - Is Active = true
   - Add Component → Box Collider 2D: Is Trigger = true, размер чтобы игрок мог встать в зону
   - Опционально: Add Component → Sprite Renderer с иконкой портала
7. **Сохранить как префаб:** Перетащи Room_MyFirst в `Assets/Prefabs/Dungeon/`.

---

## Часть 3: Создание данжа

### Шаг 3.1 — DungeonDataSO

1. ПКМ в Project → Create → RPG → Dungeons → Dungeon Data
2. Назови `Dungeon_01_Levels1-10`
3. Сохрани в `Assets/Resources/Dungeons/`
4. Настрой:
   - ID: `dungeon_01`
   - Display Name: `Данж 1–10 ур.`
   - Min Level: 1, Max Level: 10
   - Room Count: 2 (для теста — одна комната + босс, или 2 обычные)
   - Normal Room Prefabs: перетащи `Room_MyFirst` (и другие, если есть)
   - Boss Room Prefab: пока перетащи ту же комнату или создай отдельную с BossRoomController

---

## Часть 4: Интеграция в Hub

### Шаг 4.1 — DungeonSystem в сцене

1. Открой `HubScene`
2. Создай пустой объект `DungeonSystem`
3. Add Component → DungeonController
4. Создай дочерний объект `DungeonContainer` (пустой Transform)
5. Создай `HubSpawnPoint` — пустой объект, поставь в точку появления в городе
6. Найди объекты, составляющие «мир» Hub (земля, фон, здания)
7. Если они под одним родителем — назови его `HubWorld` и перетащи в DungeonController → Hub World
8. Если нет — создай родителя `HubWorld`, помести под него эти объекты, перетащи в DungeonController
9. В DungeonController задай:
   - Dungeon Container = DungeonContainer
   - Hub World = HubWorld
   - Hub Spawn Point = HubSpawnPoint
   - Player = Player (из сцены)

### Шаг 4.2 — Кнопка входа

1. Создай Canvas (если нет) или используй существующий
2. Добавь кнопку «Войти в данж»
3. В On Click добавь вызов: создать скрипт-мостик `DungeonEntryButton` с методом `EnterDungeon()`, который вызывает `DungeonController.Instance.EnterDungeon(_dungeonData)`
4. В Inspector кнопки привяжи DungeonDataSO

### Шаг 4.3 — PlayerInteractController

1. Выбери объект Player
2. Add Component → PlayerInteractController
3. Проверь радиус взаимодействия (2 units по умолчанию)

---

## Часть 5: Плейтест

### Чеклист перед запуском

- [ ] HubScene открыта
- [ ] DungeonController настроен (все ссылки)
- [ ] PlayerInteractController на Player
- [ ] DungeonDataSO содержит Room_MyFirst
- [ ] Кнопка «Войти в данж» привязана
- [ ] В Room_MyFirst: RoomController, PlayerSpawnPoint, EnemySpawner, DungeonPortal
- [ ] Interact — клавиша E (проверь в настройках Input)

### Сценарий теста 1 — Вход в данж

1. Запусти игру из Hub
2. Нажми «Войти в данж»
3. Ожидается: Hub скрылся, появилась комната, игрок стоит в PlayerSpawnPoint
4. Ожидается: заспавнились 2 врага (по настройке спавнера)

### Сценарий теста 2 — Выход в след. комнату

1. Подойди к порталу (спрайт/коллайдер)
2. Нажми E (Interact)
3. Ожидается: текущая комната уничтожилась, загрузилась следующая (или та же, если в пуле одна)
4. Игрок телепортирован в новую комнату, враги заспавнились

### Сценарий теста 3 — Возврат в город

1. В последней комнате (или босс-комнате) портал «В город»
2. Нажми Interact на нём
3. Ожидается: комната исчезла, Hub показался, игрок в HubSpawnPoint

### Частые проблемы

| Проблема | Решение |
|----------|---------|
| Interact не срабатывает | Проверить PlayerInteractController, радиус, что игрок в Collider портала |
| Враги не спавнятся | Проверить EnemyPrefab в спавнере, EnemyDataSO, SpawnOnRoomEnter и вызов RoomController.OnRoomEntered |
| Hub не скрывается | Проверить ссылку HubWorld в DungeonController |
| Телепорт в неправильную точку | Проверить позицию PlayerSpawnPoint и HubSpawnPoint |
| Комната не загружается | Проверить DungeonDataSO, что префабы назначены |
| NullReference в DungeonController | Заполнить все SerializeField (Player, Container, HubWorld, HubSpawnPoint) |

---

## Часть 6: Создание босс-комнаты

1. Дублируй Room_MyFirst → `Room_Boss`
2. Замени портал на Portal_ToHub (Portal Type = ReturnToHub, Interact Prompt = "В город")
3. Добавь босса — перетащи DummyEnemy или другой префаб, настрой уровень
4. Добавь BossRoomController:
   - Boss Entity = ссылка на EnemyHealth босса
   - Portal To Hub = Portal_ToHub
5. В BossRoomController: Portal_ToHub изначально `SetActive(false)` или через начальное состояние
6. В DungeonDataSO укажи Boss Room Prefab = Room_Boss
7. При смерти босса портал активируется, игрок может нажать E и вернуться

---

## Итоговый чеклист готовности

- [ ] Все скрипты созданы и скомпилируются
- [ ] Room_MyFirst префаб создан и настроен
- [ ] DungeonDataSO создан и ссылается на комнаты
- [ ] HubScene содержит DungeonSystem, DungeonController, кнопку входа
- [ ] Player имеет PlayerInteractController
- [ ] Плейтест: вход, смена комнаты, возврат — работают
- [ ] Босс-комната (опционально): портал после смерти босса работает
