# Character System & Tavern Setup

## Обзор изменений

### 1. Character Editor (Tools → Character Editor)
- Создание и редактирование персонажей
- Локализация: NameKey, DescriptionKey (MenuLabels)
- Fallback: DisplayName, Description
- Портрет (Sprite) для UI
- Starting Stats (модификаторы от базы)
- Привязка к Passive Skill Tree
- Кнопка «Open Passive Tree Editor» — переход к редактированию дерева
- Секция «Tree Modifier Totals» — суммы модификаторов по всем нодам (для балансировки)

### 2. Сохранение и загрузка (v2)
- **Персонаж + инвентарь** сохраняются вместе (каждый персонаж имеет свой инвентарь и экипировку)
- **Склад** — общий для всех персонажей
- Миграция с v1: старые сейвы автоматически конвертируются

### 3. Character Party Manager
- Отслеживает активного персонажа и хостел
- Методы: SwapToCharacter, AddCharacterToParty, SaveCurrentToParty, LoadCharacterIntoGame

### 4. Tavern UI
- Окно найма: 3 случайных героя, кнопка Hire, View Tree, Reroll
- Хостел: список неактивных персонажей, кнопка «Swap to Active»
- Новая игра: при отсутствии сейва открывается окно найма (выбор 1 из 3)
- Открытие по клавише **H** (TavernWindowToggle)

## Настройка сцены

### Обязательные объекты
1. **CharacterPartyManager** — добавь пустой GameObject с компонентом `CharacterPartyManager`
2. **CharacterDatabaseSO** — должен быть в сцене или загружаться (GameSaveManager ссылается на него)
3. **TavernUI** — GameObject с UIDocument и компонентом TavernUI
4. **TavernWindowToggle** — для открытия по клавише H (опционально)

### Связи в GameSaveManager
- **Tavern UI For New Game** — перетащи TavernUI, чтобы при новой игре открывалось окно найма

### Связи в TavernUI
- **Character DB** — CharacterDatabaseSO (например Heroes/CharactersDataBase)
- **Item Database** — ItemDatabaseSO
- **Window View** — для интеграции с WindowManager (Escape, порядок окон). Если пусто — подтягивается `GetComponent<WindowView>()`. Компонент WindowView уже добавлен на TavernUI в сцене.

### Character Database
- Персонажи из базы автоматически доступны для найма
- Новые персонажи, созданные в Character Editor, добавляются в базу при создании (если база найдена)

## Локализация персонажей

Ключи в MenuLabels:
- `character.{ID}.name` — имя
- `character.{ID}.description` — описание

Задаются в Character Editor в секции Localization.
