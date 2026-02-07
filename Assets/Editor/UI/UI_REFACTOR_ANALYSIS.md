# Анализ UI: проблемы и план рефакторинга

## Текущие проблемы (почему правки занимают 5-10 промптов)

### 1. **Inline styles в UXML** (самая частая проблема)
**Где:** CharacterWindow, SettingsUI, FastMenuUI, частично Inventory (слоты экипировки)

**Проблема:**
- Позиции заданы прямо в UXML: `style="position: absolute; left: 5%; top: 10%; width: 220px"`
- Чтобы подвинуть окно, нужно править UXML (не видно в коде C#, не переиспользуется)
- Смесь процентов и пикселей (`left: 5%`, `width: 220px`) — непредсказуемо на разных разрешениях

**Примеры:**
- `CharacterWindow.uxml`: `WindowPanel` имеет `left: 5%; top: 10%; bottom: 10%; width: 220px` прямо в UXML
- `SettingUXML.uxml`: `WindowPanel` имеет `top: 25px; right: 25px; left: 25px; bottom: 25px; position: absolute` в UXML
- `FastMenuUXML.uxml`: `position: absolute; left: 0; right: auto; bottom: 0; margin-left: 25px; margin-bottom: 5%` — смесь absolute и margin

### 2. **C# задаёт стили напрямую** (вторая по частоте)
**Где:** PassiveTreeUI, CharacterWindowUI, ItemTooltipController, DebugInventoryWindowUI

**Проблема:**
- В C#: `element.style.left = 0; element.style.right = 0; element.style.width = 100%`
- Стили размазаны между USS и C# — непонятно где что менять
- При изменении USS C# может перебить, при изменении C# USS может перебить

**Примеры:**
- `PassiveTreeUI.cs`: `_windowRoot.style.left = 0; _windowRoot.style.right = 0;` (строки 77-78)
- `CharacterWindowUI.cs`: `row.style.height = ROW_HEIGHT; valueLabel.style.width = 40f;` (много мест)
- `ItemTooltipController.cs`: `_itemTooltipBox.style.left = finalItemX; _itemTooltipBox.style.top = y;` (строки 422-423)
- `DebugInventoryWindowUI.cs`: `_itemListPopup.style.top = top; _itemListPopup.style.left = left;` (строки 159-160)

### 3. **Смесь подходов к позиционированию**
**Проблема:** Нет единого стандарта — где-то absolute, где-то flex с margin, где-то проценты, где-то пиксели

| Окно | Подход | Проблема |
|------|--------|----------|
| CharacterWindow | `position: absolute; left: 5%; top: 10%` (inline) | Проценты от чего? Родителя? Экрана? |
| SettingsUI | `position: absolute; top: 25px; left: 25px` (inline) | Пиксели — не масштабируется |
| FastMenuUI | `position: absolute; left: 0; bottom: 0; margin-left: 25px` (inline) | Absolute + margin — конфликт |
| Inventory | USS классы + C# переключение | Лучше, но было дублирование (исправлено) |
| PassiveTree | C# `style.left = 0; style.right = 0` | Всё в коде, нет USS |

### 4. **Нет единого стандарта структуры**
- **Inventory**: InventoryScreenRoot (full-screen) → WindowRoot → MainRow → панели
- **CharacterWindow**: WindowRoot → Overlay + WindowPanel (inline styles)
- **SettingsUI**: WindowRoot → Overlay → WindowPanel (inline styles)
- **PassiveTree**: генерируется в C#, нет UXML

### 5. **Нет документации layout-контрактов**
- Непонятно, где окно должно быть (левый верх, центр, правый низ)
- Непонятно, какие классы за что отвечают
- При правке одного окна ломается другое (если они на одном Panel)

---

## План рефакторинга: единый стандарт

### Принцип: "USS — единственный источник правды для layout"

**Правило:** 
- ✅ **USS** — все размеры, позиции, отступы
- ✅ **C#** — только переключение классов (`AddToClassList`, `RemoveFromClassList`)
- ✅ **UXML** — структура и классы, без inline `style="..."` для позиций

### Правила построения UI (нарушать только с явного разрешения)

1. **Единый фон панелей**  
   Соседние панели (склад, инвентарь, подложка окна) должны использовать один и тот же уровень затемнения фона (один и тот же `background-color` / alpha). Иначе одна панель выглядит «в два раза темнее» другой и интерфейс кажется собранным из кусков. Отклонения — только по обоснованному решению (например, акцент на активной вкладке).

2. **Зазор между соседними панелями**  
   Между двумя панелями, стоящими рядом (склад | инвентарь), обязательно должен быть визуальный зазор (margin) 4–6 px. Фон одной панели не должен заезжать под контент другой — иначе создаётся впечатление «рамки», наезжающей на слоты. Зазор задаётся в USS (например, `margin-right` у левой панели или `margin-left` у правой).

### Стандартная структура окна

```
[ScreenRoot]          ← всегда на весь экран (position: absolute; left/right/top/bottom: 0)
└── [WindowRoot]      ← само окно, позиционируется классами (.window--left, .window--right, .window--center, .window--fullscreen)
    └── [Content]     ← содержимое окна
```

**Почему ScreenRoot:**
- Unity Panel может центрировать или ограничивать дочерние UIDocument
- ScreenRoot всегда растягивается на весь экран → WindowRoot позиционируется относительно него, а не панели
- Уже работает в Inventory (InventoryScreenRoot)

### Стандартные классы позиционирования (в USS)

```uss
/* Базовый корень документа — всегда на весь экран */
.screen-root {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    width: 100%;
    height: 100%;
}

/* Окно: базовые стили */
.window {
    position: absolute;
    /* Размеры и позиция задаются модификаторами ниже */
}

/* Модификаторы позиции */
.window--left { left: 0; right: auto; }
.window--right { left: auto; right: 0; }
.window--center { left: 50%; transform: translateX(-50%); } /* или margin: auto */
.window--fullscreen { left: 0; right: 0; width: 100%; }

/* Модификаторы размера */
.window--width-small { width: 220px; }
.window--width-medium { width: 400px; }
.window--width-large { width: 600px; }
.window--width-half { width: 50%; }
.window--width-full { width: 100%; }

/* Комбинации (например, правое окно, средний размер) */
.window--right.window--width-medium { /* ... */ }
```

**Пример использования:**
- CharacterWindow: `.window.window--left.window--width-small` (слева, 220px)
- SettingsUI: `.window.window--center.window--width-medium` (центр, 400px)
- Inventory solo: `.window.window--right.window--width-half` (справа, 50%)
- Inventory + stash: `.window.window--fullscreen` (на весь экран, внутри половинки)

### Что делать с каждым окном

#### 1. CharacterWindow
- ✅ Добавить ScreenRoot в UXML
- ✅ Убрать inline styles из WindowPanel → перенести в USS классы
- ✅ Создать классы `.character-window.window.window--left.window--width-small`
- ✅ В C# убрать все `style.left/right/width` → только переключение классов при необходимости

#### 2. SettingsUI
- ✅ Добавить ScreenRoot
- ✅ Убрать inline styles → USS классы `.settings-window.window.window--center.window--width-medium`
- ✅ Overlay и WindowPanel позиционируются через классы

#### 3. FastMenuUI (PauseMenu)
- ✅ Добавить ScreenRoot
- ✅ Убрать inline styles → USS классы `.pause-menu.window.window--center.window--width-small`
- ✅ Кнопки через flex, не absolute

#### 4. PassiveTreeUI
- ✅ Создать UXML с ScreenRoot и WindowRoot
- ✅ Перенести генерацию WindowRoot из C# в UXML (или оставить генерацию, но стили в USS)
- ✅ В C# только добавление элементов, не `style.left/right`

#### 5. ItemTooltipController
- ✅ Tooltip позиционируется динамически (следует за курсором) — это нормально
- ✅ Но базовые размеры и стили — в USS, в C# только `left`/`top` для следования за курсором

#### 6. DebugInventoryWindow
- ✅ Уже использует USS классы (`.debug-root`, `.debug-panel`) — хорошо
- ✅ Попапы позиционируются в C# (`style.top`, `style.left`) — это нормально для динамических попапов
- ✅ Можно оставить как есть или вынести базовые позиции в USS

---

## Порядок рефакторинга

### Фаза 1: Стандартизация структуры (низкий риск)
1. Создать общий USS файл `Assets/UI/Common/WindowLayout.uss` с классами `.screen-root`, `.window`, `.window--left/right/center/fullscreen`, `.window--width-*`
2. Для каждого окна: добавить ScreenRoot в UXML, применить классы к WindowRoot
3. Убрать inline styles из UXML → перенести в USS

**Результат:** Все окна имеют единую структуру, позиции в USS, не в UXML

### Фаза 2: Убрать C# стили (средний риск)
1. Для каждого окна: найти все `element.style.left/right/top/bottom/width/height` в C#
2. Вынести значения в USS классы
3. В C# оставить только переключение классов

**Результат:** Layout полностью в USS, C# только логика и переключение классов

### Фаза 3: Документация (низкий риск)
1. Для каждого окна создать `[WindowName]_LAYOUT.md` (как `INVENTORY_UI_LAYOUT.md`)
2. Описать: структуру, классы, где что менять

**Результат:** При правке окна понятно, где что находится

---

## Ожидаемый эффект

**До рефакторинга:**
- "Подвинь CharacterWindow левее" → правим UXML inline style, проверяем, не сломалось ли другое, итерации
- "Окно не влезает" → ищем где задан размер (UXML? USS? C#?), правим, проверяем, итерации

**После рефакторинга:**
- "Подвинь CharacterWindow левее" → правим один класс в USS: `.character-window.window--left` → `left: 10px` вместо `left: 5%`
- "Окно не влезает" → правим `.character-window.window--width-small` → `width: 240px` вместо `220px`
- Всё в одном месте (USS), предсказуемо, быстро

---

## Риски и митигация

**Риск:** При рефакторинге что-то сломается
- **Митигация:** Делать по одному окну, тестировать после каждого. Начать с простого (CharacterWindow), потом сложное (Inventory уже сделано)

**Риск:** Старые сейвы/префабы могут ссылаться на старые классы
- **Митигация:** Классы в UXML не меняем, только добавляем новые. Старые inline styles можно оставить как fallback (но приоритет у USS)

**Риск:** PassiveTree генерируется в C# — сложно перенести в UXML
- **Митигация:** Оставить генерацию, но стили вынести в USS. Или создать базовый UXML с ScreenRoot, а дерево генерировать внутри

---

## Вопросы для обсуждения

1. **Начинаем рефакторинг сейчас?** Или сначала обсудим подход и потом сделаем всё разом?
2. **PassiveTree:** оставляем генерацию в C# или создаём UXML шаблон?
3. **Tooltip:** динамическое позиционирование (следует за курсором) — оставляем в C# или есть способ через USS?
4. **Обратная совместимость:** нужно ли поддерживать старые inline styles как fallback?
