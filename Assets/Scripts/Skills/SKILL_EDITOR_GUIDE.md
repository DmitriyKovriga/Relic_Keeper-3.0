# Skill Editor: поддержка и добавление степов

Skill Editor открыт через `Tools → Skill Editor`.

## Разделы степов

Список степов слева разбит по смыслу:

| Раздел | Что там лежит |
|---|---|
| `Тайминг и движение` | замах оружия, удар, возврат, ожидание, толчок персонажа, блок/разблок движения |
| `Хитбоксы, VFX и снаряды` | Spawn VFX, обычные снаряды, ground wave, орбитальные снаряды, damage hitbox |
| `Цепные скиллы` | построение цепи целей, VFX цепи, урон по цепи, parallel group |
| `Статусы и статы` | наложение статусов, quick status, эффекты от значения статов |
| `Mystic Shield и особые эффекты` | генерация/поглощение Mystic Shield, бонусы, cooldown |
| `Прочее` | fallback для степов, которые еще не разложены |

Разделы сейчас захардкожены в `SkillEditorWindow.GetStepEditorCategory`.

## Как добавить новый step

1. Добавить id в `EnsureBuiltInStepDefinitions()` в `SkillEditorWindow`.
2. Добавить этот id в `GetStepEditorCategory()`.
3. Добавить обработку в `SkillStepRunner.ExecuteStepLogic()`.
4. Если нужны поля в инспекторе, добавить блок в `DrawStepTypeFields()`.
5. Если step спавнит VFX/снаряды, использовать `WorldRenderSorting.ConfigureAutoSorter`.

## Орбитальные снаряды

Step id: `SpawnOrbitProjectiles`.

Что делает:

- создает снаряды вокруг игрока;
- двигает их по окружности до конца lifetime;
- использует обычный `SkillProjectile`, поэтому работают урон, damage conversions, scoped modifiers и on-hit effects;
- по умолчанию учитывает `ProjectileCount`, если включен `Add ProjectileCount stat`.

Основные поля:

| Поле | Что делает |
|---|---|
| `Projectile prefab` | prefab снаряда |
| `Use current weapon sprite` | использовать sprite текущего оружия вместо prefab |
| `Base projectile count` | базовое число снарядов на орбите |
| `Add ProjectileCount stat` | добавляет к числу снарядов stat `ProjectileCount` |
| `Lifetime` | сколько живут снаряды |
| `Pierce targets` | если включено, снаряд не разбивается об первого врага |
| `Orbit radius` | радиус орбиты |
| `Angular speed deg/sec` | скорость вращения |
| `Clockwise` | направление вращения |
| `Center offset X/Y` | смещение центра орбиты относительно игрока |
| `Hit radius scale` | размер hitbox снаряда |
| `Damage multiplier` | множитель урона |

Если `Pierce targets` выключен, снаряд исчезает после первого врага.  
Если включен, снаряд продолжает летать, но не бьет одну и ту же цель повторно в рамках своей жизни.

## Что не нужно делать вручную

- Не выставлять sorting layer/order на VFX и снарядах, если они спавнятся степами.
- Не делать отдельный damage script для projectile, если хватает `SkillProjectile`.
- Не создавать новый раздел в UI без необходимости: сначала добавь step в существующий раздел.
