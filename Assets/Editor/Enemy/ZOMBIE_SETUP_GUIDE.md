# Как собрать первого врага и подключить его в спавнер

Документ описывает текущий рабочий путь для `Zombie` и любых следующих врагов на новой архитектуре.

## 1. Данные врага

Создай или открой `EnemyDataSO`.

Минимально заполняем:

- `ID`
- `DisplayName`
- `Prefab`
- `AIType`
- `Stats`
- `Perception`
- `Movement`
- `Attack`
- `Animation`
- `XPReward`

Что такое `Prefab`:

- это реальный runtime-префаб врага, который спавнер будет инстанцировать в комнате
- для врага из `EnemySpawner` поле `Prefab` в `EnemyDataSO` обязательно
- если поле пустое, спавнер сможет взять только legacy fallback prefab со своего объекта, но на новую систему лучше на это не рассчитывать

Для живого мили-врага важны:

- `AIType = GroundChaser`
- `MoveSpeed > 0`
- `AggroRange > 0`
- `AttackRange > 0`
- `DamagePhysical` или другой нужный тип урона в `Stats`
- `Animation.Controller` назначен

## 2. Префаб врага

Сейчас префаб может быть очень простым.

Достаточно иметь:

- `SpriteRenderer`
- `EnemyEntity`
- `EnemyStats`
- `EnemyHealth`

Остальные runtime-компоненты (`EnemyBrain`, `EnemySensor2D`, `EnemyLocomotion2D`, `EnemyAttackController`, `EnemyAnimationBridge`, `Animator`, `Rigidbody2D`) система добирает сама через `EnemyEntity.Setup()`.

Но для первого живого врага лучше мыслить так:

- `EnemyDataSO` хранит данные и поведение
- `Prefab` хранит визуал, SpriteRenderer, Collider и базовую сборку объекта
- `EnemySpawner` спавнит именно этот prefab и потом прокидывает в него `EnemyDataSO`

## 3. Анимации зомби

Для зомби в `EnemyDataSO` нужно указать:

- `Animation.Controller = Zombie_Attacks_0.controller`
- `IdleStateName = Zombie_Idle`
- `MoveStateName = Zombie_Walk`
- `AttackStateName = Zombie_Attacks`

## 4. Статы зомби

Пример минимального набора:

- `MaxHealth`
- `Armor`
- `DamagePhysical` или `DamageFire/Cold/Lightning`
- `FireResist`
- `ColdResist`
- `LightningResist`
- `PhysicalResist`

Если используется новый контракт `Stats`, старое `BaseStats` можно больше не трогать.

## 5. Подключение в комнату

Открой префаб комнаты или объект комнаты в сцене.

На `EnemySpawner`:

- добавь запись в `Enemy Entries`
- укажи нужный `EnemyDataSO`
- выставь `Weight`
- задай `Spawn Count`

Важно:

- теперь спавнер сначала берёт `Prefab` из самого `EnemyDataSO`
- старое поле `Enemy Prefab` на спавнере осталось только как fallback для legacy-случаев

## 6. Что проверить в игре

Для зомби после запуска надо проверить:

1. Он спавнится в комнате.
2. Он проигрывает `Idle`, пока игрок далеко.
3. При приближении начинает идти к игроку.
4. Не пытается зайти внутрь игрока, а останавливается на дистанции.
5. Вблизи проигрывает атаку.
6. Атака реально снимает HP у игрока.
7. Не падает с края платформы, если `CanFallFromPlatform = false`.

## 7. Переходы между платформами

Для `AgileJumper` и `KitingRanged` теперь можно использовать `EnemyJumpLink`.

Как это работает:

- поставь объект `EnemyJumpLink` на точке старта прыжка
- укажи `Exit Point` там, куда враг должен выпрыгнуть
- при необходимости включи `Bidirectional`, если link должен работать в обе стороны

Практика:

- link лучше ставить у края платформы
- `Exit Point` лучше ставить на безопасной площадке после прыжка
- для первого зомби это не требуется, но для быстрых прыгунов и кайтеров это уже рабочая основа

## 8. Ограничения текущей версии

На текущем этапе:

- полностью рабочим считается `GroundChaser`
- `AgileJumper`, `StaticCaster`, `KitingRanged` уже заведены в контракт и имеют базовую рантайм-логику, но дальше их ещё стоит развивать как отдельные боевые архетипы
- автоматического pathfinding по платформам пока нет
- переходы между платформами сейчас лучше строить через ручные `EnemyJumpLink`
