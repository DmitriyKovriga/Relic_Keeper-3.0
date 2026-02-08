# Детальный план тестирования системы скиллов

План покрывает путь от создания нового скилла в редакторе до проверки в игре на герое и генерации скилла через пулл (предмет с SkillPool → GetRandomSkill → GrantedSkills → экипировка → каст).

---

## 1. Подготовка (один раз)

- [ ] В сцене есть игрок с компонентами: **PlayerStats**, **PlayerSkillManager**, **InventoryManager** (или аналог), инвентарь и слоты экипировки.
- [ ] На игроке висит **SkillContainer** (или PlayerSkillManager создаёт его сам) — сюда инстанцируются префабы скиллов при экипировке.
- [ ] Ввод привязан к вызову **PlayerSkillManager.UseSkill(slotIndex)** (например слот 0 — основная атака, 1 — вторичная).
- [ ] Открыть **Tools → Skill Editor** — окно открывается, в списке скиллов есть хотя бы один SkillDataSO (можно Cleave для ориентира).
- [ ] При необходимости нажать **Create default step defs** — создадутся Step_*.asset (MovementLock, WeaponWindup, SpawnVFX, DealDamageCircle и т.д.). После **Refresh** типы степов видны в левой колонке редактора.

---

## 2. Создание нового скилла (данные и рецепт)

### 2.1 SkillDataSO

- [ ] В Project: ПКМ → Create → RPG → Skills → **Skill Data**. Имя, например, `MySkill_Data`.
- [ ] Заполнить:
  - **ID** — уникальный (например `MySkill_V1`).
  - **SkillName** — отображаемое имя.
  - **IsActive** — true.
  - **Cooldown**, **ManaCost** — по желанию.
  - **AnimationTrigger** — триггер анимации (например `Attack`).
  - **SkillPrefab** — пока пусто (создадим ниже).
  - **Recipe** — пока пусто (создадим ниже).

### 2.2 SkillRecipeSO

- [ ] ПКМ → Create → RPG → Skills → **Skill Recipe**. Имя, например, `Recipe_MySkill`.
- [ ] В **Skill Data** (MySkill_Data) в поле **Recipe** перетащить этот Recipe_MySkill.

### 2.3 Сборка рецепта в Skill Editor

- [ ] В **Skill Editor** в выпадающем списке скиллов выбрать **MySkill** (или как назвали). В центре отобразится пустой список степов.
- [ ] Добавить степы по очереди (клик по типу в левой колонке):
  - **MovementLock** — выделить степ, справа в **Timing** задать Start % = 0, End % = 100.
  - **WeaponWindup** — Start % = 0, End % = 35.
  - **WeaponStrike** — Trigger at % = 35.
  - **Spawn VFX** — Trigger at % = 35. В **Step settings**: VFX Prefab (префаб из проекта), **Scale multiplier** (например 1 или 1.2), Offset X/Y, Base duration, при необходимости Attach to parent / Invert facing.
  - **DealDamageCircle** — Trigger at % = 35. Radius, **Source step index** = индекс степа SpawnVFX (обычно 3), Damage multiplier.
  - **WeaponRecovery** — Start % = 35, End % = 100.
  - **MovementUnlock** — Trigger at % = 100 (опционально, т.к. Lock уже снимается в End % MovementLock).
- [ ] Сохранить рецепт (Ctrl+S / Save Project). Проверить в центральном списке отображение тайминга: `[0%]`, `[0% – 35%]`, `[35%]` и т.д.

### 2.4 Префаб скилла с SkillStepRunner

- [ ] Скопировать существующий префаб с **SkillStepRunner** (например `Skill_Cleave_StepRunner`) или создать новый GameObject.
- [ ] На префабе должны быть:
  - **SkillStepRunner** (наследник SkillBehaviour). В инспекторе задать **Target Layer** для DealDamage (слой врагов).
  - **SkillMovementControl**, **SkillHandAnimation** (требуются SkillStepRunner).
  - При необходимости **SkillVFX** (если не используете VfxPrefab в степе).
- [ ] Сохранить префаб, например `Skill_MySkill.prefab`.
- [ ] В **Skill Data** (MySkill_Data) в поле **Skill Prefab** назначить этот префаб.

Итог: скилл с Recipe и SkillPrefab готов. Дальше — проверка в игре и через пулл.

---

## 3. Проверка скилла в игре (прямая подстановка)

Цель: убедиться, что скилл выполняется по рецепту без пулла (напрямую назначен на оружие/предмет).

- [ ] Взять предмет экипировки, который даёт скилл (например оружие). В **WeaponItemSO** (или другом EquipmentItemSO) нет пулла — скилл задаётся вручную через код/инспector только если так задумано в проекте. Обычно скиллы даются через **GrantedSkills** у экземпляра предмета (InventoryItem), а GrantedSkills заполняется при генерации из пулла. Поэтому для «прямой» проверки нужно временно задать скилл предмету:
  - Либо в коде экипировки подставить ваш SkillDataSO в слот.
  - Либо сгенерировать предмет из базы, у которой в пулле только ваш скилл (см. раздел 4).
- [ ] Экипировать предмет. В иерархии под контейнером скиллов должен появиться инстанс префаба скилла (например `Skill_MySkill_0`).
- [ ] Нажать атаку (UseSkill(0)). Ожидается:
  - Блок движения на время скилла (0–100%).
  - Замах (0–35%), удар и появление VFX на 35%, урон по кругу, возврат (35–100%), разблок.
- [ ] Проверить размер VFX: при **Scale multiplier** &gt; 1 эффект крупнее, &lt; 1 — мельче (при том же AoeScale).
- [ ] Проверить кулдаун/ману, если заданы.

---

## 4. Пул скиллов и генерация предмета

Цель: убедиться, что скилл выдаётся через SkillPoolSO при генерации предмета и попадает в GrantedSkills.

### 4.1 SkillPoolSO

- [ ] ПКМ → Create → RPG → Skills → **Skill Pool**. Имя, например, `Pool_AxeSkills`.
- [ ] В **Possible Skills** добавить элементы: ваш **MySkill_Data** и при необходимости другие SkillDataSO (например Cleave). Для каждого задать **Weight** (шанс выпадения). Например Cleave = 10, MySkill = 5.

### 4.2 Привязка пула к базе предмета

- [ ] Открыть базу оружия (WeaponItemSO) или другой **EquipmentItemSO**, с которого генерируется дроп.
- [ ] В секции **Skill Configuration**:
  - **Skill Pool** = `Pool_AxeSkills` (или ваш пул).
  - **Skill Count** = 1 (для одноручного оружия и брони — сколько скиллов роллить из пула).
- [ ] Для **двуручного** оружия: отдельно есть **Secondary Skill Pool** — пул для второго слота (вторичная атака). Генератор для 2H: первый скилл из основного SkillPool, второй из SecondarySkillPool.

### 4.3 Генерация предмета (ItemGenerator)

- [ ] В сцене есть **ItemGenerator** (или сервис генерации), вызывается при создании дропа/награды.
- [ ] Вызвать генерацию предмета для этой базы (тестовый дроп, сундук, квест и т.д.). В коде: `ItemGenerator.Instance.Generate(baseWeapon, itemLevel, rarity)` (или аналог).
- [ ] У полученного **InventoryItem** проверить **GrantedSkills**: в списке должен быть ровно один (или два для 2H) SkillDataSO, причём с вероятностью по весам может выпасть ваш MySkill_Data.
- [ ] Для стабильной проверки можно временно сделать в пулле только один скилл (ваш) с Weight = 1 — тогда он будет выпадать всегда.

### 4.4 Экипировка и каст сгенерированного предмета

- [ ] Подобрать/экипировать сгенерированный предмет. В **PlayerSkillManager** при экипировке вызывается **EquipSkill(slotIndex, skillData)** с skillData = item.GrantedSkills[0] (или [1] для 2H вторичного).
- [ ] Должен инстанцироваться **Skill Prefab** из выпавшего SkillDataSO, на нём **Initialize(stats, skillData)**, скилл попадает в _activeSkills.
- [ ] Нажать атаку — вызывается **TryCast()** у соответствующего SkillBehaviour (SkillStepRunner). Рецепт выполняется по таймингу (Start % / End %).
- [ ] Повторить с другим предметом того же пула (или перезапустить генерацию), чтобы при выпадении другого скилла из пулла (например Cleave) он тоже корректно экипировался и кастовался.

---

## 5. Чеклист по подсистемам

### Редактор (Skill Editor)

- [ ] Открытие, три колонки, Refresh, RU/EN.
- [ ] Create default step defs, Rebuild Cleave recipe.
- [ ] Добавление/удаление/перемещение степов, выбор типа, тайминг (Start % / End % / Trigger at %).
- [ ] Inspector: Timing, Step type, Step settings (в т.ч. Scale multiplier у SpawnVFX).
- [ ] ParallelGroup: подстепы, выбор подстепа, настройка полей.

### Рантайм (SkillStepRunner)

- [ ] Выполнение по времени (0–100% пайплайна), Lock 0–100%, Windup 0–35%, Strike/VFX/Damage на 35%, Recovery 35–100%.
- [ ] SpawnVFX: префаб, Scale multiplier, позиция, длительность; DealDamage с SourceStepIndex берёт масштаб из VFX.
- [ ] Движение блокируется/разблокируется, анимация руки (Windup/Strike/Recovery).
- [ ] Cancel() (если вызывается) разблокирует движение и сбрасывает анимацию.

### Пул и генерация

- [ ] **SkillPoolSO**: Possible Skills с весами, GetRandomSkill() возвращает один из скиллов по весам.
- [ ] **ItemGenerator**: для базы с SkillPool при генерации вызывается GetRandomSkill(), результат добавляется в newItem.GrantedSkills. Для 2H: первый скилл из SkillPool, второй из SecondarySkillPool.
- [ ] **PlayerSkillManager**: при экипировке по item.GrantedSkills вызывается EquipSkill; инстанцируется skillData.SkillPrefab, SkillBehaviour.Initialize(stats, skillData), UseSkill(slot) → TryCast().

### Граничные случаи

- [ ] Скилл с пустым рецептом или Recipe == null при префабе с StepRunner — предупреждение в консоль, без падения.
- [ ] Скилл без SkillPrefab в SkillDataSO — при экипировке не создаётся инстанс, слот пустой (или по логике проекта).
- [ ] Пул пустой или все веса 0 — GetRandomSkill() не падает (в текущей реализации возвращается первый скилл или null).
- [ ] Предмет без пулла (SkillPool == null) — GrantedSkills при генерации не заполняется скиллами; экипировка не даёт скилл в этот слот.

---

## 6. Краткая последовательность «от нуля до каста»

1. Создать **Skill Data** и **Recipe**, в редакторе набрать рецепт (степы + тайминг + VFX/Scale multiplier и т.д.).
2. Создать/скопировать **префаб с SkillStepRunner**, назначить его в Skill Data → Skill Prefab.
3. Создать **Skill Pool**, добавить в него этот скилл (и другие при необходимости).
4. У **базы оружия/брони** (EquipmentItemSO) задать **Skill Pool** (и для 2H — Secondary Skill Pool).
5. В игре сгенерировать предмет через **ItemGenerator** по этой базе → у предмета заполнятся **GrantedSkills** из пулла.
6. Экипировать предмет → **PlayerSkillManager** создаёт инстанс префаба скилла, инициализирует, кладёт в слот.
7. Нажать атаку → **UseSkill** → **TryCast** → выполнение рецепта по времени; проверить VFX, урон, блок движения.

После прохождения плана систему скиллов (рецепты, редактор, рантайм, пулл, генерация, экипировка, каст) можно считать проверенной.
