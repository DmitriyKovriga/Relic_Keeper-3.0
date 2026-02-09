# Controls Editor — план и связка с настройками

## Цели

1. **Controls Editor** — редактор, в котором задаётся:
   - список действий (биндов), показываемых в окне настроек;
   - логика биндов и отображение в одном месте, без правок UXML/кода при добавлении нового инпута.

2. **Просмотр подписчиков** — видеть, какие методы в коде подписаны на какие инпуты (Player.*.performed += ...).

3. **UXML под 480×270** — окно настроек управлений переведено в pixel-perfect/ручной режим, все элементы помещаются в 480×270 и готовы к изменению из редактора.

4. **Локализация инпутов** — у каждого инпута локаль EN/RU; ключ генерируется по имени действия, например `input.Jump`, `input.MoveLeft`. Путь/таблица — MenuLabels (или отдельная таблица при желании).

---

## Компоненты

### 1. ControlsEditorConfig (ScriptableObject)

- **Путь:** `Assets/Resources/Controls/ControlsEditorConfig.asset` (или `Assets/Editor/...` если только для редактора).
- **Поля:**
  - `InputActionAsset` — ссылка на InputSystem_Actions.
  - `List<ControlEntry>`:
    - `actionName` (string) — имя действия в карте Player (например `Jump`, `OpenInventory`).
    - `displayOrder` (int) — порядок строк в окне настроек.
    - `showInSettings` (bool) — показывать ли в окне Controls.
  - Опционально: `defaultBindingPath` для подсказки дефолта (можно оставить в InputRebindSaver).
- Локализация: ключ всегда `input.{ActionName}` (EN/RU в MenuLabels). Редактор создаёт/обновляет записи при сохранении.

### 2. Controls Editor Window (Editor)

- **Меню:** например `Tools / Relic Keeper / Controls Editor`.
- **Функции:**
  - Загрузка/выбор `ControlsEditorConfig` и `InputActionAsset`.
  - Список действий из карты Player (или из конфига): для каждого — порядок, showInSettings, локали EN/RU (редактируемые поля; сохранение в MenuLabels с ключом `input.{ActionName}`).
  - Кнопка «Sync from Input Asset» — обновить список записей конфига по текущим действиям в asset (новые добавить, лишние не удалять).
  - Кнопка «Save locale» — записать EN/RU в MenuLabels для всех записей конфига.
  - Секция **Subscribers**: сканирование `.cs` в проекте на паттерны вида `InputManager.InputActions.Player.<ActionName>.performed +=` / `-=` и вывод списка: действие → тип/метод (имя класса и метода). Можно через поиск по тексту или через Roslyn при наличии.

### 3. UXML окна настроек (SettingUXML)

- Привести к отображению в 480×270 (pixel-perfect или фиксированные размеры под PixelArtPanelSettings).
- Убрать дублирование строк; оставить один шаблон строки биндинга (например одна строка с именами `BindingRow_Template`), остальное генерировать в рантайме из конфига в ControlsUI (клонирование шаблона и подстановка actionName, привязка к действию).
- Либо: редактор может «разворачивать» конфиг в UXML (генерировать N строк) по кнопке «Apply to UXML» — тогда ControlsUI продолжает искать по имени `BindingLabel_{actionName}`. Выбор: предпочтительно **генерация строк в рантайме из конфига** (один шаблон в UXML), чтобы не трогать UXML при добавлении инпутов.

### 4. ControlsUI (runtime)

- Читает `ControlsEditorConfig` (из Resources или переданный в инспекторе).
- Для каждой записи с `showInSettings == true` (отсортированной по `displayOrder`):
  - клонирует шаблон строки из UXML;
  - подписывает лейбл названия на локализацию по ключу `input.{ActionName}`;
  - находит/создаёт BindingLabel_{actionName}, ChangeButton_{actionName} и вешает ребайнд как сейчас.
- Сохранение/загрузка биндов без изменений (InputRebindSaver).

### 5. Локализация

- Ключ: `input.{ActionName}` (например `input.Jump`, `input.OpenStash`).
- Таблица: MenuLabels (en/ru). Редактор при сохранении вызывает `SetOrAddEntry(MenuLabels, "en", "input.Jump", "Jump")` и аналогично для ru.
- В рантайме: в UI использовать `LocalizedString` с таблицей MenuLabels и ключом `input.{ActionName}` (через TableEntryReference по ключу, не по Id).

### 6. InputRebindSaver

- Оставить как есть; дефолтные бинды можно по желанию в будущем читать из конфига (опционально).

---

## Порядок реализации

1. Создать `ControlEntry` (Serializable) и `ControlsEditorConfig` (ScriptableObject); создать ассет конфига с текущим списком действий.
2. Реализовать **Controls Editor Window**: список действий, порядок, showInSettings, EN/RU, сохранение в MenuLabels, кнопка Sync from Input Asset.
3. Добавить в редактор секцию **Subscribers**: поиск по коду `InputManager.InputActions.Player.*.performed` и вывод «action → class.method».
4. Переработать **SettingUXML**: под 480×270, одна шаблонная строка биндинга; убрать лишние дубли.
5. Переработать **ControlsUI**: загрузка конфига, построение строк из шаблона по конфигу, локализация по ключу `input.{ActionName}`.
6. При необходимости: опциональная связка дефолтных биндов с конфигом в InputRebindSaver.

---

## Файлы

| Файл | Назначение |
|------|------------|
| `Assets/Editor/UI/ControlsEditorConfig.cs` | Serializable ControlEntry + ControlsEditorConfig SO (или в Runtime при использовании из build). |
| `Assets/Editor/UI/ControlsEditorWindow.cs` | Окно редактора: конфиг, список, локали EN/RU, Subscribers. |
| `Assets/Resources/Controls/ControlsEditorConfig.asset` | Экземпляр конфига (или в Editor-папке). |
| `Assets/UI/SettingsUI/SettingUXML.uxml` | Обновлённый layout 480×270, одна строка-шаблон. |
| `Assets/UI/SettingsUI/ControlsUI.cs` | Построение строк из конфига, локаль по ключу. |

Локализация: ключ `input.{ActionName}`; путь к таблице не фиксируем в плане — генерируется из названия инпута (как и запрошено).

---

## Настройка в сцене

- На объекте с **ControlsUI** в инспекторе задать:
  - **Config** — ссылка на `ControlsEditorConfig` (например из `Assets/Resources/Controls/ControlsEditorConfig.asset`; создать через меню **Tools > Relic Keeper > Create Controls Editor Config**).
  - **Binding Row Template** — ссылка на `Assets/UI/SettingsUI/BindingRowTemplate.uxml`.
- Если Config не задан, используется встроенный список действий (как раньше). Если шаблон не задан, строки не создаются из шаблона (логика fallback по старым именам из корня).
