# План рефакторинга редактора дерева пассивок (PoE-подобный, SRP)

## 1. Исходное состояние (до рефакторинга)

### Два редактора в коде (исторически)
- **PassiveTreeEditorWindow** использует **PassiveSkillTreeGraphView** (Unity GraphView): ноды с портами, рёбра между портами, pan/zoom через манипуляторы. **Кластеры и орбиты не поддерживаются** — только свободные ноды и связи.
- **PassiveTreeEditorCanvas** (~857 строк) — кастомный canvas с полной моделью: кластеры, орбиты, ноды на орбитах, линии связей, контекстные меню, перетаскивание нод/кластеров. **Сейчас не используется в окне** — только GraphView.

### Вывод
Чтобы приблизиться к редактору Path of Exile (орбиты, кластеры, один большой канвас), основным редактором должен стать **Canvas**. GraphView можно оставить опционально или убрать позже.

### Проблемы текущего Canvas
- **Один огромный файл** (PassiveTreeEditorCanvas): viewport, pan, zoom, выбор, drag нод/кластеров, контекстные меню (viewport/node/cluster), создание/удаление нод и кластеров, связи, координаты, сохранение — всё в одном классе.
- Нарушение SRP: сложно поддерживать и тестировать, любое изменение затрагивает один файл.
- Нет Undo, мини-карты, поиска, Frame All / Frame Selection — как в PoE.

---

## 2. Целевая архитектура (SRP, мелкие файлы)

Один редактор на базе **Canvas**. Логика разнесена по отдельным классам с одной зоной ответственности. Окно и canvas только собирают сервисы и подписываются на события.

### 2.1 Роли и файлы

| Файл | Ответственность |
|------|-----------------|
| **PassiveTreeEditorWindow** | Окно: тулбар, split (canvas + inspector), загрузка дерева, привязка canvas к инспектору. Без логики редактирования. |
| **PassiveTreeEditorCanvas** | Только композиция: создаёт viewport/content, слои (сетка, кластеры, линии, ноды, маркеры), подписывает события на сервисы. Тонкий оркестратор. |
| **PassiveTreeViewportController** | Pan и zoom: обработка мыши (pan), колёсика (zoom к курсору), min/max zoom, преобразование viewport ↔ content. Хранит zoom и позицию content. |
| **PassiveTreeSelectionService** | Состояние выбора: выбранные ноды, выбранный кластер; Clear, SelectNode, SelectCluster; события OnSelectionChanged. Без UI. |
| **PassiveTreeEditorCommands** | Все мутации дерева: CreateNode, DeleteNode, CreateCluster, AddOrbit, DeleteCluster, Connect/Disconnect, ConvertToFree, PlaceOnOrbit. Принимает дерево и при необходимости выбор; вызывает SaveAsset. Без UI. |
| **PassiveTreeContextMenuBuilder** | Построение контекстного меню для трёх контекстов: viewport (пустое место), node, cluster. Добавляет пункты и вызывает команды из PassiveTreeEditorCommands. Один класс — три метода (BuildViewportMenu, BuildNodeMenu, BuildClusterMenu). |
| **PassiveTreeConnectionLines** | Отрисовка линий связей: построение списка пар (node A, node B), создание VisualElement-линий (прямые/дуги при необходимости). Refresh(tree, nodesContainer, linesContainer). Без выбора и меню. |
| **PassiveTreeCoordinateHelper** | Только координаты: ViewportToContent(Vector2), ContentToViewport(Vector2), учёт zoom и сдвига content. Статические или привязка к viewport controller. |
| **PassiveTreeAssetPersistence** | Сохранение ассета: SetDirty(tree), SaveAssets(). Один класс — один-два метода. |
| **PassiveTreeGridOverlay** | Уже есть. Сетка по GridSize дерева. |
| **PassiveTreeEditorNode** | Уже есть. Визуал одной ноды, события (pointer, context menu). |
| **PassiveTreeClusterView** | Уже есть. Визуал кластера (орбиты, маркер центра), события. |

### 2.2 Дополнительные фичи (как в PoE)

| Фича | Файл | Описание |
|------|------|----------|
| **Minimap** | PassiveTreeMinimap | Небольшая карта всего дерева в углу, клик — переход к месту, опционально отображение viewport rect. |
| **Search** | PassiveTreeSearchFilter | Поле поиска (имя ноды / стат), подсветка подходящих нод, остальные затемнены или скрыты. |
| **Frame All / Frame Selection** | PassiveTreeViewportController или отдельный PassiveTreeFrameCommands | Zoom/pan так, чтобы всё дерево или выбранные ноды были в видимой области. |
| **Undo** | PassiveTreeEditorCommands + UnityEditor.Undo | Обернуть каждую мутацию в Undo.RecordObject(tree, "Create Node") и т.п. |

### 2.3 Зависимости (поток данных)

- **Window** создаёт **Canvas**, передаёт ему дерево при открытии.
- **Canvas** создаёт:
  - **ViewportController** (viewport, content) — управляет pan/zoom и даёт координаты.
  - **SelectionService** — хранит выбранные ноды и кластер.
  - **EditorCommands** — дерево + persistence; при необходимости передаётся selection для Connect/Disconnect.
  - **ContextMenuBuilder** — получает tree, selection, commands, viewport (для позиции мыши).
  - **ConnectionLines** — обновляет линии при изменении дерева/выбора (по событию от canvas).
  - **GridOverlay**, контейнеры слоёв, **PassiveTreeEditorNode** / **PassiveTreeClusterView**.
- События мыши: Canvas передаёт в ViewportController (pan/zoom), в SelectionService (клик по ноде/кластеру), в ContextMenuBuilder (ПКМ). Drag ноды/кластера можно оставить в Canvas как тонкую координацию между ViewportController (координаты) и данными ноды/кластера (обновление Position/Center/OrbitAngle).
- После любой мутации дерева: EditorCommands вызывает Persistence; Canvas вызывает PopulateView или точечное обновление (RefreshLines, обновление позиций нод).

---

## 3. Этапы реализации

### Фаза 1: Разбить текущий Canvas по SRP (без новых фич)
1. Выделить **PassiveTreeViewportController** — вынести pan/zoom и преобразование координат из Canvas.
2. Выделить **PassiveTreeSelectionService** — вынести состояние выбора и методы Clear/Select.
3. Выделить **PassiveTreeAssetPersistence** и **PassiveTreeCoordinateHelper** (если координаты не остаются только во ViewportController).
4. Выделить **PassiveTreeEditorCommands** — все методы вида CreateNodeAtPosition, DeleteNode, ConnectSelected и т.д. Команды принимают tree и опционально selection; при необходимости получают позицию извне (из контекстного меню).
5. Выделить **PassiveTreeConnectionLines** — RefreshLines и CreateLineElement.
6. Выделить **PassiveTreeContextMenuBuilder** — три метода меню, вызов команд.
7. Упростить **PassiveTreeEditorCanvas** до композиции и подписки на события; drag по-прежнему может координироваться в Canvas (чтение координат из ViewportController, применение к данным — через команды или напрямую для движения).

Итог: тот же функционал, но в 7–9 маленьких файлах вместо одного большого.

### Фаза 2: Переключить окно на Canvas
8. В **PassiveTreeEditorWindow** заменить **PassiveSkillTreeGraphView** на **PassiveTreeEditorCanvas**. Подключить инспектор к выбору из SelectionService (один выбранный нод — показать PropertyField).
9. Убедиться, что открытие ассета по двойному клику загружает дерево в Canvas.

### Фаза 3: PoE-подобные улучшения
10. **Undo**: в **PassiveTreeEditorCommands** перед каждой мутацией дерева вызывать `Undo.RecordObject(_tree, "Operation Name")`.
11. **Frame All / Frame Selection**: во **PassiveTreeViewportController** (или отдельный хелпер) метод FrameAll(tree) и FrameSelection(selection); тулбар с кнопками.
12. **Minimap**: новый класс **PassiveTreeMinimap**, подписка на viewport и дерево, клик — переход.
13. **Search**: новый класс **PassiveTreeSearchFilter** (поле в тулбаре), подсветка/фильтр нод по строке.

### Фаза 4 (опционально)
14. ~~Удалить или скрыть **PassiveSkillTreeGraphView** и **PassiveSkillTreeNode**~~ — выполнено: файлы удалены, редактор только на Canvas.
15. Улучшение линий: дуги для связей на одной орбите — уже сделано в редакторе (PassiveTreeConnectionLines).

---

## 4. Порядок файлов в папке (рекомендуемый)

```
PassiveTree/
  PassiveTreeEditorWindow.cs      # Окно
  PassiveTreeEditorCanvas.cs      # Композиция canvas
  Viewport/
    PassiveTreeViewportController.cs
    PassiveTreeCoordinateHelper.cs   # при необходимости вынести
  Services/
    PassiveTreeSelectionService.cs
    PassiveTreeEditorCommands.cs
    PassiveTreeAssetPersistence.cs
  UI/
    PassiveTreeContextMenuBuilder.cs
    PassiveTreeConnectionLines.cs
  Overlays/
    PassiveTreeGridOverlay.cs
    PassiveTreeMinimap.cs          # фаза 3 (опционально)
  Nodes/
    PassiveTreeEditorNode.cs
    PassiveTreeClusterView.cs
  Search/
    PassiveTreeSearchFilter.cs     # фаза 3 (опционально)
```

Подпапки (Viewport, Services, UI, Overlays, Nodes, Search) — по желанию; минимум — плоский список с префиксами имён для группировки.

---

## 5. Итог

- **Статус: рефакторинг завершён.**
- **Сейчас**: один редактор на **Canvas** (окно использует PassiveTreeEditorCanvas). Логика разнесена по SRP: ViewportController, SelectionService, EditorCommands, ContextMenuBuilder, ConnectionLines, AssetPersistence. Реализованы Undo, Frame All / Frame Selection; дуги для связей на одной орбите. GraphView и PassiveSkillTreeNode удалены.
- **Опционально позже**: Minimap, Search Filter — по мере необходимости.
- Поддержка и новые фичи сводятся к правкам одного узкого класса или добавлению нового файла.
