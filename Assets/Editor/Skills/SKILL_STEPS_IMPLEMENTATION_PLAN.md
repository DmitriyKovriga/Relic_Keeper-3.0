# План реализации: система степов для скиллов

## Фазы

### Фаза 1. Модель данных
- **StepDefinitionSO** — ScriptableObject: Id, NameEn, NameRu, иконка, описание (EN/RU), дефолтные параметры (универсальная структура или подтипы). Папка `Resources/Skills/StepDefinitions/`.
- **StepEntry** — [Serializable] класс: ссылка на StepDefinitionSO (или StepTypeId), оверрайды (сериализуемый словарь или конкретные поля по типу), TriggerAtStepIndex, TriggerAtPercent; для ParallelGroup — List<StepEntry> SubSteps.
- **SkillRecipeSO** — расширение или замена части SkillDataSO: список Steps (List<StepEntry>), IsChanneling, ChannelLoopSteps (индексы или вложенный список), ChannelMaxDuration, BaseDuration или использование 1/APS. Связь: SkillDataSO ссылается на SkillRecipeSO ИЛИ рецепт встроен в SkillDataSO.
- Решение: SkillDataSO оставляем (ID, имя, иконка, кулдаун, мана); добавляем поле **SkillRecipeSO Recipe** (опционально). Если Recipe == null — работаем по-старому (префаб с CleaveSkill). Если Recipe задан — рантайм использует StepRunner. Так миграция без поломки существующих скиллов.

### Фаза 2. Базовый рантайм (SkillStepRunner)
- **SkillStepRunner** — компонент на префабе скилла (или один общий префаб для всех рецептов). Наследник SkillBehaviour. Initialize(stats, data); Execute() запускает корутину по рецепту.
- Контекст выполнения: totalDuration (из статов), кэш результатов степов (позиция/scale VFX по индексу), флаг отмены (для доджа).
- Очередь выполнения: основные степы по порядку; при старте степа с длительностью регистрировать отложенные (TriggerAtStepIndex/Percent) и в момент достижения % запускать привязанные степы (параллельно). ParallelGroup — запуск всех подстепов, ожидание максимума длительностей.
- Заглушки исполнения: пока каждый тип степа — заглушка (логирование или вызов существующих модулей через GetComponent). Реальную логику степов — в фазе 3.

### Фаза 3. Реализация степов (по приоритету)
- MovementLock, MovementUnlock — вызов SkillMovementControl.
- WeaponWindup, WeaponStrike, WeaponRecovery — вызов SkillHandAnimation, скрытие оружия на Strike.
- SpawnVFX — вызов аналога SkillVFX (или общего сервиса спавна VFX с параметрами из контекста).
- DealDamageCircle, DealDamageRectangle — хитбокс + DamageCalculator, опция «from step index» из кэша.
- Wait — yield по DurationPercent.
- Остальные типы (CharacterDisplace, CharacterBuff, AffectEnemies, Projectile, ParallelGroup) — по мере необходимости; ParallelGroup в рантайме уже обработан в фазе 2.

### Фаза 4. Миграция Cleave
- Создать SkillRecipeSO для Cleave: последовательность MovementLock → WeaponWindup(35%) → WeaponStrike → SpawnVFX + DealDamageCircle (from VFX) → WeaponRecovery(65%) → MovementUnlock.
- Создать префаб скилла с SkillStepRunner (или переключить существующий префаб Cleave на использование рецепта). SkillDataSO Cleave — ссылка на этот рецепт и префаб с StepRunner.
- Убедиться, что поведение совпадает (тайминги, урон, VFX).

### Фаза 5. Редактор скиллов (Skill Editor)
- Окно Tools → Skill Editor. Список скиллов (все SkillDataSO с рецептами или все рецепты). Выбор скилла — справа список степов.
- Для каждого степа: выбор типа (dropdown по StepDefinitionSO из папки), отображение оверрайдов (поля зависят от типа; универсальный редактор по полям из определения или по известным типам). Переключатель **RU/EN** сверху — отображать NameRu или NameEn из StepDefinitionSO в списке.
- Добавить/удалить степ, изменить порядок (перетаскивание или кнопки). Поддержка ParallelGroup: один степ типа ParallelGroup, при выборе — вложенный список подстепов с теми же возможностями.
- Секция рецепта: IsChanneling, ChannelLoopSteps (подсписок индексов или отдельный список степов), ChannelMaxDuration.

### Фаза 6. Додж и отмена
- В SkillStepRunner: флаг _cancelled; метод Cancel(). При Cancel() выставить флаг, в корутине проверять и выходить, вызывать Cleanup (Unlock, сброс анимации).
- PlayerSkillManager или отдельный DodgeController: при додже вызывать Cancel() у текущего активного скилла (если есть).

### Фаза 7. Ченнелинг
- В рантайме: если рецепт IsChanneling, после «начальных» степов войти в цикл: выполнить ChannelLoopSteps (с тем же totalDuration или tick duration), проверить «кнопка зажата» и «не макс. время», повторить или выйти и выполнить финальные степы.
- Требуется доступ к вводу «кнопка скилла зажата» (Input System hold). Если сейчас только tap — расширить.

### Фаза 8. Документация
- Создать **STEPS_DOCUMENTATION.md** (или в папке Editor/Skills): описание каждого типа степа, как настраивать, примеры (один удар, дабл страйк, урон+смещение на 50% VFX, параллельные VFX, ченнелинг). Как использовать отложенные триггеры и ParallelGroup.

---

## Порядок файлов (создание/изменение)

1. `Scripts/Skills/Steps/StepDefinitionSO.cs` — новый.
2. `Scripts/Skills/Steps/StepEntry.cs` — новый (или в одном файле с рецептом).
3. `Scripts/Skills/Steps/SkillRecipeSO.cs` — новый.
4. Расширить `SkillDataSO.cs` — поле Recipe (опционально).
5. `Scripts/Skills/Logic/SkillStepRunner.cs` — новый (наследник SkillBehaviour).
6. Реализация исполнителей степов (отдельные классы или один StepExecutor с switch по типу) в `Scripts/Skills/Steps/Execution/`.
7. Создать несколько StepDefinitionSO ассетов в `Resources/Skills/StepDefinitions/` (MovementLock, MovementUnlock, WeaponWindup, WeaponStrike, WeaponRecovery, SpawnVFX, DealDamageCircle, Wait, ParallelGroup).
8. `Editor/Skills/SkillEditorWindow.cs` — новое окно.
9. Рецепт Cleave + префаб/переключение Cleave на StepRunner.
10. Додж: Cancel в StepRunner, вызов из доджа.
11. Ченнелинг: цикл в StepRunner, ввод «hold».
12. `Editor/Skills/STEPS_DOCUMENTATION.md` — финальная документация.
