# Настройка отображения: Game view Scale 1 и рамка Cinemachine

Чтобы при **Scale 1x** в панели Game картинка совпадала с зелёной рамкой Cinemachine и выглядела корректно, должна быть настроена связка **Pixel Perfect Camera + Cinemachine Pixel Perfect**.

## Что уже есть в проекте

- **Main Camera**: компонент **Pixel Perfect Camera** (Ref Resolution 480×270, Assets PPU 24).
- **CinemachineCamera**: расширение **Cinemachine Pixel Perfect** уже добавлено (компонент на том же объекте).
- **Player Settings**: Default resolution 1920×1080.

Расширение Cinemachine подхватывает настройки Pixel Perfect Camera и пересчитывает orthographic size виртуальной камеры так, чтобы и пиксель-арт был чётким, и кадрирование Cinemachine сохранялось.

## Что проверить, если отображение «не то»

### 1. Какой у тебя Pixel Perfect Camera

- **URP-версия** (из пакета Universal RP): в инспекторе есть выпадающие списки **Crop Frame** и **Grid Snapping**, поля **Reference Resolution X/Y**, **Current Pixel Ratio**. Поля **Run In Edit Mode нет** — это нормально: у URP-компонента стоит `[ExecuteInEditMode]`, он и так работает в редакторе без Play. Ничего включать не нужно.
- **2D-версия** (из пакета 2D Pixel Perfect): при использовании URP в инспекторе может показываться предупреждение и кнопка «Upgrade». После апгрейда получится URP-компонент (см. выше).

### 2. Разрешение в панели Game

Для точного соответствия «один логический пиксель = один/несколько экранных» в выпадающем списке разрешений Game выбери одно из:

- **480×270** — 1:1 с референсным разрешением;
- **960×540** — 2×;
- **1920×1080** — 4× (Full HD).

Масштаб окна (0.5x, 1x, 2x) оставь таким, при котором тебе удобно — главное, чтобы было включено **Run In Edit Mode** и при необходимости нужное разрешение из списка выше.

### 3. Расширение Cinemachine Pixel Perfect

Если расширение когда-то снимали, его нужно вернуть:

- Выбери объект **CinemachineCamera** в сцене.
- В Inspector у **Cinemachine Virtual Camera** нажми **Add Extension** → **Cinemachine Pixel Perfect**.

Без этого расширения Cinemachine и Pixel Perfect Camera одновременно меняют orthographic size и «дерутся» — картинка или рамка будут неправильными.

### 4. Разрешение в билде

При запуске билда в **1920×1080** (как в Player Settings) Pixel Perfect Camera масштабирует 480×270 в 4× на весь экран — поведение такое же, как в редакторе при выбранном разрешении 1920×1080 и Scale 1x.

---

## Краткий чеклист

| Проверка | Где | Действие |
|----------|-----|----------|
| Pixel Perfect Camera | Main Camera | URP-версия: Run In Edit Mode нет — компонент уже работает в Edit Mode. 2D-версия: при URP лучше апгрейд до URP-компонента. |
| Разрешение Game | Панель Game → выпадающий список | 480×270 / 960×540 / 1920×1080 по желанию |
| Cinemachine Pixel Perfect | CinemachineCamera → Add Extension | Должно быть добавлено |
| Ref Resolution | Main Camera → Pixel Perfect Camera | 480×270 (уже стоит) |
| Current Pixel Ratio | Там же | 3:1 при подходящем разрешении Game — нормально (масштаб 3× от 480×270) |

Если после этого при Scale 1x картинка всё ещё не совпадает с рамкой или «плывёт», опиши, что именно видишь (обрезано, растянуто, смещено), и можно будет сузить причину.
