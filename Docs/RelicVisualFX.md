# Relic Sprite FX и Relic Atmosphere

Оригинальный ShaderLab/HLSL MVP для Unity 6000.3.11f1, URP 17.3, Renderer2D и RenderGraph.
Код эффектов написан для проекта; используются только стандартные API/include Unity и собственная процедурная noise-текстура.

## Подключение за минуту

1. `Tools → Relic Keeper → Visual FX → Install neutral assets and Renderer Feature` создаёт отсутствующие базовые ассеты и один `Relic Atmosphere` в **активном default Renderer2D**. Повторный вызов не дублирует feature и не сбрасывает ассеты. В текущем проекте feature уже установлен в `Assets/Settings/Renderer2D.asset`.
2. Выберите конкретные SpriteRenderer и вызовите `Apply shared Sprite FX to selected sprites`. Назначается общий `Assets/Visuals/RelicFX/RelicSpriteFX.mat` и добавляется `SpriteFxController`. Действие поддерживает Undo. Материалы остальных объектов не меняются.
3. Для атмосферы выберите настоящую ортографическую Camera (в Hub это Main Camera с Cinemachine Brain, **не** CinemachineCamera) и вызовите `Add atmosphere to selected camera`.
4. Дублируйте `Assets/Visuals/RelicFX/AtmosphereNeutral.asset`, назначьте копию в `AtmosphereController.Profile`. В ней начните с Darkness `0.2`, Fog Opacity `0.12`, Vignette `0.1`. Исходный нейтральный профиль оставьте нейтральным.
5. Назначьте Player в инспекторе либо вызовите `BindPlayer(player.transform)` после спавна. На спрайте игрока отдельно включите emission; атмосфера сама материал игрока не меняет.

HubScene и MainMenuScene не редактируются. Нет контроллера или видимых параметров профиля — нет атмосферного прохода.

## Sprite FX: враг, игрок, предмет, VFX

Материал группирует параметры в Color, Flash, Outline / inner shadow, Emission, Dissolve, Alpha / quality. Все эффекты изначально нейтральны. Для постоянного вида отдельного типа врага/предмета дублируйте материал как asset и используйте его совместно. Дополнительный SO для sprite-пресетов не нужен: пресетом служит материал.

```csharp
using Scripts.Visuals.SpriteFX;
using Scripts.Visuals.Atmosphere;

// Кэшировать ссылки при создании объекта, не искать каждый кадр.
fx.Flash(Color.red, 0.12f);
fx.TemporaryTint(new Color(0.5f, 0.8f, 1f), 0.5f);
fx.SetEmission(new Color(0.2f, 0.7f, 1f), 0.35f);
fx.DissolveTo(1f, 0.6f);
fx.SetDissolve(0f);
fx.SetFade(0.5f);
fx.ResetEffects();
atmosphere.BindPlayer(player.transform);
```

Runtime tint умножается на material tint; runtime flash применяется после material flash; emission складывается; dissolve берёт максимум material/runtime значений; fade перемножается. Поэтому runtime dissolve `0` не отменяет dissolve, уже заданный на материале. Новый временный flash/tint заменяет предыдущий. Таймеры используют scaled game time. Disable сбрасывает временные эффекты для pooling.

| Параметры материала | Назначение |
|---|---|
| Tint, Brightness, Contrast, Desaturate | Множитель RGBA, яркость 0–2, контраст вокруг 0.5, доля обесцвечивания |
| Flash / Flash Color | Перекраска RGB без изменения силуэта |
| Outline / Color / Width | Наружная обводка в прозрачном отступе, 1–4 **исходных texel**, не экранных пикселя |
| Inner Shadow / Color | Однопиксельное затемнение внутреннего края по альфе; без blur |
| Emission / Color | Добавка HDR RGB внутри силуэта. Не освещает соседние объекты |
| Dissolve / Noise / Scale | Порог 0–1 по статической noise-текстуре; 0 полностью видим, 1 полностью скрыт |
| Edge Width / Color | Цветная кромка на оставшейся стороне dissolve |
| Fade / Clip | Общая альфа и необязательный порог отсечения |
| Quality | Число соседних выборок outline; shader keywords не переключаются |

**Atlas и геометрия.** Нейтральный шейдер, цвет, flash, emission и fade поддерживают SpriteRenderer и атласы. Контроллер обновляет UV-границы при смене Sprite/texture, сохраняя sorting layer/order и flip Unity. Для полного внешнего outline используйте `Mesh Type = Full Rect`, прозрачный отступ внутри sprite rect не меньше Width, point filtering, atlas `Tight Packing = Off`, `Allow Rotation = Off`. Отступ только между элементами атласа не расширяет геометрию спрайта. Tight-packed atlas автоматически отключает соседние выборки, чтобы не захватить другой спрайт. На tight mesh outline может обрезаться геометрией. Автоматического расширения меша, изменения import settings и дополнительных renderer нет.

**Fade VFX.** Шейдер оставляет `_RendererColor` как legacy alias для альфы и использует `min(SpriteRenderer alpha, legacy alpha)`. `AutoDestroyVFX` пишет один fade одновременно в renderer.color и MPB: это не должно возводить альфу в квадрат. RGB alias намеренно не используется, для цвета есть Renderer.color и FxTint. Контроллер читает существующий MPB перед записью своих `_Fx*` свойств и не трогает legacy alias/чужие значения. Другие владельцы MPB тоже должны делать read-modify-write.

## Атмосфера и UI

Проход `BeforeRenderingPostProcessing` выполняется после мира и до Overlay UI/апскейла Pixel Perfect. Он рисует один fullscreen triangle с premultiplied blending, без чтения scene color, blit, depth texture и дополнительных RT. Параметры берутся от контроллера **конкретной камеры**, статического глобального player/profile нет. Scene View, reflection, preview cameras, perspective и overlay cameras пропускаются.

В Hub: Pixel Perfect Camera — 24 PPU, 480×270; Cinemachine управляет основной камерой; uGUI Canvas — Screen Space Overlay; UI Toolkit PanelSettings без target texture — экранный overlay. Эти UI не попадают под эффект. MainMenu имеет Screen Space Camera Canvas, но на его камере нет контроллера, поэтому атмосфера там выключена.

**Обязательное правило UI:** при включённой атмосфере uGUI должен быть Screen Space Overlay, UI Toolkit — экранный overlay без Target Texture. Camera/World Space UI уже находится в world color buffer и не может быть отделён этим проходом. Меню подключения отказывается включать атмосферу для камеры с таким Canvas, а инспектор показывает ошибку. При самостоятельном создании контроллеров/Canvas в runtime соблюдайте то же правило. Дополнительная камера не нужна. Инструмент не переводит чужие Canvas в другой режим автоматически.

| Профиль | Назначение / единицы |
|---|---|
| Intensity | Общая сила, 0 отключает проход |
| Darkness / Fog Opacity / Fog Tint | Затемнение и цветная завеса; альфа Fog Tint не используется |
| Vignette | Мягкое затемнение краёв на логической сетке |
| Reveal Radius | Полностью чистое ядро в world units |
| Reveal Softness | Ширина внешнего smoothstep-перехода в world units |
| Reveal Intensity | Насколько зона убирает fog/darkness/vignette |
| Noise Amount / Scale | Неоднородность завесы и пространственная частота |
| Drift Speed | XY world units/second, движение квантуется по PPU |
| Pulse Amount / Frequency | Дополнительная модуляция силы, частота Hz; по умолчанию 0 |
| Pixels Per Unit | World grid, по умолчанию 24; согласовать со спрайтами |
| Reference Resolution | Логическая сетка vignette, 480×270 |
| Dither | Сила статического ordered dither; нет смены случайного паттерна каждый кадр |
| Quality | Low без noise/dither, Medium один noise octave, High два |

Controller World Plane Z — плоскость XY мира, обычно 0. World position восстанавливается через актуальные view/projection matrices камеры, включая Pixel Perfect и вращение вокруг Z. Параметры player читаются при записи графа после обновления Cinemachine. Player отсутствует/уничтожен — reveal выключается, а атмосфера остаётся. Gizmos выбранного контроллера показывают ядро и внешнюю границу.

## Preview

`Tools → Relic Keeper → Visual FX → Open safe preview` открывает отдельное окно с настоящим рендером 480×270 и ползунками darkness, fog, reveal и позиции игрока. Все изменения в окне временные. Текущая игровая сцена не сохраняется и не заменяется. Шесть собственных процедурных силуэтов показывают neutral, flash, outline, inner shadow, emission и dissolve. Reveal привязан к Emission; подписи показывает само окно Editor.

`Create preview assets` создаёт `Assets/Visuals/RelicFX/Preview/RelicFXPreview.prefab` с отдельным профилем и материалами; повторный вызов выделяет существующий ассет. Prefab также можно поместить в **пустую тестовую сцену**: там есть своя Camera и Overlay Canvas. В изолированном окне Canvas не захватывается render request; интерфейс окна рисуется отдельно. В игровые сцены этот демонстрационный prefab добавлять не нужно.

## Производительность и ограничения

- Атмосфера: один draw, без полноэкранной копии и без RT на объект; нейтральный профиль — ноль draw. Low не считает procedural noise; Medium/High добавляют 1/2 octave. Не включайте blur/TAA/bloom, если нужна строгая пиксельная чёткость.
- Sprite FX: нейтральный вариант — одна выборка спрайта. Low outline — 4 соседа шириной 1 texel; Medium — до 16; High — до 32, до 4 texel. Dissolve добавляет одну noise-выборку. Ветви динамические; нет комбинаторных shader variants эффектов.
- `SpriteFxController` не создаёт material instances, корутин или управляемых коллекций в Update/LateUpdate. MPB создаётся один раз. Постоянный emission и неподвижный спрайт не требуют записей MPB каждый кадр. MPB может исключать renderer из SRP Batcher; это осознанная цена индивидуальных параметров.
- Unlit MVP не реагирует на Light2D. Emission — яркий пиксельный цвет, **не bloom/световой ореол за геометрией**; внешний чёткий aura можно получить outline с отступом. Глобальная атмосфера всё равно влияет на игрока в степени, заданной reveal. Lit-ветка, SpriteSkin, ETC1 split alpha, 9-sliced/tiled SpriteRenderer, XR и compatibility mode не входят в MVP. Используйте Simple SpriteRenderer и включённый RenderGraph.
- World-space pixel grid предотвращает субпиксельное движение шума. Движение самой камеры/спрайтов должно оставаться согласованным с Pixel Perfect Camera; шейдер не исправляет дробный transform scale и нецелый display upscale. Квантованные края/reveal при движении меняются дискретно на пиксель — это ожидаемо.
- Atmosphere fog и dissolve noise независимы. Outline растворяется вместе со спрайтом; inner shadow вычисляется по исходному силуэту, не по новым dissolve-отверстиям. Palette swap отложен.

## Отключение и проверка

Выключите AtmosphereController, установите Intensity = 0 либо отключите Relic Atmosphere в Renderer2D. Верните обычный sprite material, чтобы отключить Sprite FX; `ResetEffects()` сбрасывает только runtime значения. Удаление feature не требует изменения сцен.

```powershell
agents-unity-bridge refresh
agents-unity-bridge compile
agents-unity-bridge run-tests --mode EditMode --filter "RelicKeeper.Tests.EditMode.RelicVisualFxTests;RelicKeeper.Tests.EditMode.RelicVisualFxRenderTests" --timeout 120
agents-unity-bridge get-console-logs --limit 20 --filter Error
```

Render-тестам нужен GPU (не `-nographics`). Они используют отдельные editor preview scenes и временный RT, проверяют реальные пиксели при 480×270. В runtime система не создаёт RT; тесты и окно Editor используют один собственный RT для readback/показа. Кадр reveal сохраняется в игнорируемый `Logs/RelicFX/`.

Основа API: установленный исходный код URP 17.3 в Library/PackageCache; [официальное описание RenderGraph](https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/customizing-urp/render-graph/write-render-pass). Следующий этап — визуальная настройка пресетов на игровых спрайтах и измерение GPU на целевом устройстве; затем при необходимости отдельная Lit2D-ветка.

## Состав изменений

Все пути ниже относительно корня репозитория; Unity `.meta` добавлены вместе с новыми файлами/папками.

| Файлы | Содержание |
|---|---|
| `Assets/Shaders/RelicFX/RelicSpriteFX.shader`, `RelicAtmosphere.shader` | Два оригинальных шейдера |
| `Assets/Scripts/Visuals/SpriteFX/SpriteFxController.cs` | Индивидуальные эффекты через MPB |
| `Assets/Scripts/Visuals/Atmosphere/AtmosphereProfileSO.cs`, `AtmosphereController.cs`, `RelicAtmosphereFeature.cs` | Профиль, player/camera binding, RenderGraph pass |
| `Assets/Scripts/Visuals/Editor/RelicSpriteFxShaderGUI.cs`, `RelicVisualTools.cs`, `RelicFxPreviewWindow.cs` | Инспектор, установка/назначение, изолированное preview |
| `Assets/Visuals/RelicFX/RelicSpriteFX.mat`, `DissolveNoise.asset`, `AtmosphereNeutral.asset` | Базовые нейтральные ассеты |
| `Assets/Visuals/RelicFX/Preview/RelicFXPreview.prefab`, `AtmospherePreview.asset`, `RelicSilhouette.asset` | Демонстрация без правок Hub |
| `Assets/Visuals/RelicFX/Preview/Neutral.mat`, `Flash.mat`, `Outline.mat`, `Innershadow.mat`, `Emission.mat`, `Dissolve.mat` | Шесть демонстрационных материалов |
| `Assets/Settings/Renderer2D.asset` | Единственный изменённый существующий asset: ссылка на feature и её subasset |
| `Assets/Tests/EditMode/Editor/RelicVisualFxTests.cs`, `RelicVisualFxRenderTests.cs` | 8 тестов логики/интеграции и 5 GPU-тестов |
| `Docs/RelicVisualFX.md` | Эта инструкция |

Проверка реализации: Unity compile через bridge — успешно; оба класса EditMode — **13 passed / 0 failed / 0 skipped**. GPU-тесты проверяют neutral vs URP, renderer color/flip, fade alias, outline/dissolve, atlas subrect, режимы качества, камеры и world-space reveal. Полный build игры и профилирование целевого устройства не выполнялись. Проверка UI основана на текущем порядке Renderer2D/Overlay и тесте запрета Camera Space Canvas при подключении; GPU-снимок Overlay UI из игрового режима не заявляется.
